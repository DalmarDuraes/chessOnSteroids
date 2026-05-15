using Chess.Core;
using UnityEngine;

namespace Chess.Unity
{
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] PieceType pieceType;
        [SerializeField] Chess.Core.Color pieceSide;
        [SerializeField] PieceMovementCapability movementCapabilities;

        Renderer[] _renderers;
        Material[][] _baselineShared;

        public PieceType Type => pieceType;
        public Chess.Core.Color Side => pieceSide;
        public PieceMovementCapability MovementCapabilities => movementCapabilities;

        public void Init(PieceType type, Chess.Core.Color side, PieceMovementCapability movementCaps = default)
        {
            pieceType = type;
            pieceSide = side;
            movementCapabilities = movementCaps;
            CacheBaselines();
        }

        void CacheBaselines()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baselineShared = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material[] sm = _renderers[i].sharedMaterials;
                _baselineShared[i] = new Material[sm.Length];
                for (int j = 0; j < sm.Length; j++)
                    _baselineShared[i][j] = sm[j];
            }
        }

        /// <summary>
        /// Swap all renderer slots to <paramref name="highlightMaterial"/> or restore prefab baseline.
        /// </summary>
        public void SetCaptureTargetHighlight(Material highlightMaterial, bool enabled)
        {
            if (_renderers == null || _baselineShared == null)
                CacheBaselines();

            if (_renderers.Length == 0)
                return;

            if (!enabled || highlightMaterial == null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var restore = new Material[_baselineShared[i].Length];
                    for (int j = 0; j < _baselineShared[i].Length; j++)
                        restore[j] = _baselineShared[i][j];
                    _renderers[i].sharedMaterials = restore;
                }

                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                int n = _baselineShared[i].Length;
                var mats = new Material[n];
                for (int j = 0; j < n; j++)
                    mats[j] = highlightMaterial;
                _renderers[i].sharedMaterials = mats;
            }
        }
    }
}
