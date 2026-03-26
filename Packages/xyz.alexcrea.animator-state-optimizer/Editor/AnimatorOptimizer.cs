using alexcrea.AnyRemover;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;
using VRC;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(AnimatorOptimizer))]
namespace alexcrea.AnyRemover
{
    
    public class AnimatorOptimizer : Plugin<AnimatorOptimizer>
    {
        
        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing).Run("Animator State Optimizer", StartRemover);
        }
        
        [CanBeNull]
        private static AnimatorController getControllerForLayers(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            var controller = layer.animatorController;
            while (controller is AnimatorOverrideController ov) controller = ov.runtimeAnimatorController;
            return controller as AnimatorController;
        }
        
        private static void StartRemover([NotNull] BuildContext ctx)
        {
            // Try to see if any state remover script somewhere
            var component = ctx.AvatarRootObject.GetComponentInChildren<AnyStateRemover>();
            if ((object) component == null) return;
            if (!component.isEnabled)
            {
                Debug.Log("Found Any state remover but is disabled");
                return;
            } 

            Optimize(ctx, component);
        }
        
        private static void Optimize([NotNull] BuildContext ctx, AnyStateRemover component)
        {
            var descriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();

            foreach (var layers in descriptor.baseAnimationLayers)
            {
                var controller = getControllerForLayers(layers);
                if((object)controller == null) continue;
                OptimizeController(controller, component);
            }
        }
        
        private static void OptimizeController([NotNull] AnimatorController controller, AnyStateRemover component)
        {
            var dirty = false;
            foreach (var layer in controller.layers)
            {
                dirty|= OptimizeLayer(layer, component);
            }
            
            // Not really usefull seems to be for saving purpose so
            if(dirty) controller.MarkDirty();
        }
        
        private static bool OptimizeLayer([CanBeNull] AnimatorControllerLayer layer, AnyStateRemover component)
        {
            if (layer == null) return false;
            return TryOptimizeStateMachine(layer.stateMachine, component);
        }
        
        private static bool TryOptimizeStateMachine([CanBeNull] AnimatorStateMachine stateMachine, AnyStateRemover component)
        {
            if ((object)stateMachine == null) return false;
            if (stateMachine.stateMachines.Length > 0) return false;
            if (stateMachine.entryTransitions.Length > 0 && !component.changeOtherDefaultTransitions) return false;

            var defState = stateMachine.defaultState;
            if ((object)defState == null) return false;
            
            var state2 = FindSecondState(stateMachine, defState);

            if ((object) state2 == null || (object)state2 == defState) return false;

            if (!CheckAllToState(stateMachine.entryTransitions, state2, false, defState)) return false;
            if (!CheckAllToState(stateMachine.anyStateTransitions, state2, false)) return false;
            if (!CheckAllToState(defState, state2, component.removeDefaultStateExitTransition)) return false;
            if (!CheckAllToState(state2, defState, true)) return false;
            
            // Finally we can work on optimizing: 
            foreach (var trans in stateMachine.entryTransitions)
            {
                var dest = trans.destinationState;
                if((object) dest == state2) CloneTransition(trans, defState, state2);
                stateMachine.RemoveEntryTransition(trans);
            }
            
            // Cloning the state to state transitions
            CloneTransitions(defState, state2, false);
            CloneTransitions(state2, defState, true);
            
            // Now clone the any transitions
            foreach (var trans in stateMachine.anyStateTransitions)
            {
                CloneTransition(trans, defState, state2);
                stateMachine.RemoveAnyStateTransition(trans);
            }
            
            return true;
        }

        private static AnimatorState FindSecondState(AnimatorStateMachine machine, AnimatorState defstate)
        {
            foreach (var trans in machine.anyStateTransitions)
            {
                if(trans.isExit) continue;
                if((object)trans.destinationState == defstate) continue;
                return trans.destinationState;
            }
            
            foreach (var trans in defstate.transitions)
            {
                if(trans.isExit) continue;
                return trans.destinationState;
            }
            
            foreach (var trans in machine.entryTransitions)
            {
                if((object)trans.destinationState == defstate) continue;
                return trans.destinationState;
            }
            
            return null;
        }
        
        private static bool CheckAllToState(AnimatorTransitionBase[] transitions, AnimatorState to, bool allowExit, AnimatorState other = null)
        {
            foreach (var trans in transitions)
            {
                if (trans.isExit && allowExit) continue;
                var dest = trans.destinationState;
                if ((object) dest != to && (object) dest != other) return false;
            }

            return true;
        }
        
        private static bool CheckAllToState(AnimatorState from, AnimatorState to, bool allowExit)
        {
            return CheckAllToState(from.transitions, to, allowExit);
        }
        
        private static void CloneTransitions(AnimatorState from, AnimatorState to, bool allowExit)
        {
            foreach (var trans in from.transitions)
            {
                if(!trans.isExit || allowExit) 
                    CloneTransition(trans, from, to);
                from.RemoveTransition(trans);
            }
        }
        
        private static void CloneTransition(AnimatorStateTransition transition, AnimatorState from, AnimatorState to)
        {
            var newtrans = from.AddTransition(to);
            CloneTransitionProperties(transition, newtrans);
        }
        
        private static void CloneTransition(AnimatorTransition transition, AnimatorState from, AnimatorState to)
        {
            var newtrans = from.AddTransition(to);
            CloneTransitionProperties(transition, newtrans);
        }
        
        private static void CloneTransitionProperties(AnimatorStateTransition from, AnimatorStateTransition to)
        {
            to.canTransitionToSelf = from.canTransitionToSelf;
            to.duration = from.duration;
            to.exitTime = from.exitTime;
            to.hasExitTime = from.hasExitTime;
            to.hasFixedDuration = from.hasFixedDuration;
            to.interruptionSource = from.interruptionSource;
            to.offset = from.offset;
            to.orderedInterruption = from.orderedInterruption;
            to.conditions = from.conditions;
            to.name = from.name;
            to.hideFlags = from.hideFlags;
            to.mute = from.mute;
            to.solo = from.solo;
        }
        
        private static void CloneTransitionProperties(AnimatorTransition from, AnimatorStateTransition to)
        {
            to.canTransitionToSelf = false;
            to.duration = 0;
            to.exitTime = 0;
            to.hasExitTime = false;
            to.hasFixedDuration = true;
            to.conditions = from.conditions;
            to.name = from.name;
            to.hideFlags = from.hideFlags;
            to.mute = from.mute;
            to.solo = from.solo;
        }
    }
    
}
