using Chess.Core;
using UnityEngine;

namespace Chess.Unity
{
    /// <summary>
    /// Maps board indices to world space: +X = files a→h, +Z toward Black (ranks 1→8).
    /// <see cref="origin"/> is the bottom corner of square a1 (min X, min Z of that cell). Piece and square
    /// centers use half-step offsets in X/Z. Light squares follow FIDE layout: h1 is light → (file + rank) odd.
    /// </summary>
    public class ChessBoardView : MonoBehaviour
    {
        const string BoardSquaresRootName = "BoardSquares";

        [SerializeField] float squareSize = 1f;
        [SerializeField] Vector3 origin = Vector3.zero;
        [Tooltip("Vertical thickness of each square cube.")]
        [SerializeField] float tileThickness = 0.06f;
        [Tooltip("Offset above the top of the tiles for piece pivots.")]
        [SerializeField] float pieceYOffset = 0.3f;
        [SerializeField] LayerMask boardLayerMask = ~0;

        [Header("Checker materials — light = h1 (sum file+rank odd)")]
        [SerializeField] Material lightSquareMaterial;
        [SerializeField] Material darkSquareMaterial;

        public float SquareSize => squareSize;
        public Vector3 Origin => origin;

        /// <summary>Local Y of the top face of square tiles (use for highlights).</summary>
        public float BoardTopLocalY => origin.y + tileThickness;

        public Vector3 GetWorldPositionForSquare(int squareIndex, float localY)
        {
            int f = Square.File(squareIndex);
            int r = Square.Rank(squareIndex);
            Vector3 local = new Vector3(
                origin.x + (f + 0.5f) * squareSize,
                localY,
                origin.z + (r + 0.5f) * squareSize);
            return transform.TransformPoint(local);
        }

        public Vector3 GetWorldPositionForPiece(int squareIndex) =>
            GetWorldPositionForSquare(squareIndex, BoardTopLocalY + pieceYOffset);

        public bool TryRaycastToSquare(Ray ray, out int squareIndex)
        {
            squareIndex = Square.Invalid;
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, boardLayerMask, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 p = transform.InverseTransformPoint(hit.point);
            float fx = (p.x - origin.x) / squareSize;
            float fz = (p.z - origin.z) / squareSize;
            int file = Mathf.Clamp(Mathf.FloorToInt(fx), 0, 7);
            int rank = Mathf.Clamp(Mathf.FloorToInt(fz), 0, 7);
            squareIndex = Square.Index(file, rank);
            return true;
        }

        /// <summary>Builds 64 checker cubes. Safe to call repeatedly — replaces previous <see cref="BoardSquaresRootName"/> tree.</summary>
        public void BuildDefaultVisualsAndCollider()
        {
            Transform existing = transform.Find(BoardSquaresRootName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(BoardSquaresRootName);
            root.transform.SetParent(transform, false);

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    bool isLight = ((file + rank) & 1) == 1;
                    Material mat = isLight ? lightSquareMaterial : darkSquareMaterial;

                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Sq_{Square.ToAlgebraic(Square.Index(file, rank))}";
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.localPosition = new Vector3(
                        origin.x + (file + 0.5f) * squareSize,
                        origin.y + tileThickness * 0.5f,
                        origin.z + (rank + 0.5f) * squareSize);
                    tile.transform.localScale = new Vector3(squareSize, tileThickness, squareSize);

                    var mr = tile.GetComponent<MeshRenderer>();
                    if (mat != null && mr != null)
                        mr.sharedMaterial = mat;
                }
            }
        }
    }
}
