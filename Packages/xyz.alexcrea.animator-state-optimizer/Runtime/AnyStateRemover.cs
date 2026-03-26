using UnityEngine;

namespace alexcrea.AnyRemover
{

    public class AnyStateRemover : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        public bool isEnabled = true;
        public bool removeDefaultStateExitTransition = true;
        public bool changeOtherDefaultTransitions = true;
    }
}