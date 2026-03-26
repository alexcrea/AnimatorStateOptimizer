using UnityEngine;

namespace alexcrea.animator_state_optimizer
{

    public class AnimatorOptimizer : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        public bool isEnabled = true;
        public bool removeDefaultStateExitTransition = true;
        public bool changeOtherDefaultTransitions = true;
    }
}