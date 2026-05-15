using System.Collections.Generic;
using Chess.Core;
using UnityEngine;

namespace Chess.Unity
{
    public sealed class LegalMoveHighlight : MonoBehaviour
    {
        [SerializeField] ChessBoardView board;
        [SerializeField] Material legalMaterial;
        [SerializeField] Material selectedMaterial;
        [SerializeField] float yOffset = 0.02f;

        readonly List<GameObject> _pool = new();

        void EnsurePool(int count)
        {
            while (_pool.Count < count)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Highlight";
                Object.Destroy(quad.GetComponent<MeshCollider>());
                quad.transform.SetParent(transform, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var mr = quad.GetComponent<MeshRenderer>();
                if (mr != null && legalMaterial != null)
                    mr.sharedMaterial = legalMaterial;
                quad.SetActive(false);
                _pool.Add(quad);
            }
        }

        void HideAll()
        {
            foreach (var g in _pool) g.SetActive(false);
        }

        void PlaceMarker(int poolIndex, int square, Material mat, float scaleFactor)
        {
            var go = _pool[poolIndex];
            go.SetActive(true);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            Vector3 p = board.GetWorldPositionForSquare(square, board.BoardTopLocalY + yOffset);
            go.transform.position = p;
            float s = board.SquareSize * scaleFactor;
            go.transform.localScale = Vector3.one * s;
        }

        public void Refresh(int? selectedSquare, IReadOnlyList<Move> legalMovesForSideToMove)
        {
            HideAll();
            if (board == null) return;

            HashSet<int> legalDestinations = null;
            if (legalMovesForSideToMove != null && selectedSquare.HasValue)
            {
                legalDestinations = new HashSet<int>();
                foreach (var m in legalMovesForSideToMove)
                {
                    if (m.From == selectedSquare.Value)
                        legalDestinations.Add(m.To);
                }
            }

            int need = 0;
            if (selectedSquare.HasValue) need++;
            if (legalDestinations != null) need += legalDestinations.Count;

            if (need == 0) return;
            EnsurePool(need);

            int idx = 0;
            if (selectedSquare.HasValue && selectedMaterial != null)
            {
                PlaceMarker(idx++, selectedSquare.Value, selectedMaterial, 0.45f);
            }

            if (legalDestinations != null && legalMaterial != null)
            {
                foreach (int to in legalDestinations)
                    PlaceMarker(idx++, to, legalMaterial, 0.35f);
            }
        }
    }
}
