using Chess.Core;
using UnityEngine;

namespace Chess.Unity
{
    /// <summary>Optional helper: assign a target in the Inspector and click Context → Snap To Look At Target.</summary>
    public sealed class ChessCameraRig : MonoBehaviour
    {
        [SerializeField] Transform lookTarget;

        public void SnapToLookAt()
        {
            if (lookTarget == null) return;
            transform.LookAt(lookTarget.position);
        }

#if UNITY_EDITOR
        [ContextMenu("Snap To Look At Target")]
        void EditorSnap() => SnapToLookAt();
#endif
    }
}
