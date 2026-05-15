using Chess.Core;
using UnityEngine;

namespace Chess.Unity
{
    [CreateAssetMenu(menuName = "Chess/Piece Prefab Library", fileName = "PiecePrefabLibrary")]
    public sealed class PiecePrefabLibrary : ScriptableObject
    {
        [Header("White")]
        public GameObject whitePawn;
        public GameObject whiteRook;
        public GameObject whiteKnight;
        public GameObject whiteBishop;
        public GameObject whiteQueen;
        public GameObject whiteKing;

        [Header("Black")]
        public GameObject blackPawn;
        public GameObject blackKnight;
        public GameObject blackBishop;
        public GameObject blackRook;
        public GameObject blackQueen;
        public GameObject blackKing;

        public GameObject GetPrefab(Piece piece)
        {
            bool w = piece.Color == Chess.Core.Color.White;
            return piece.Type switch
            {
                PieceType.Pawn => w ? whitePawn : blackPawn,
                PieceType.Knight => w ? whiteKnight : blackKnight,
                PieceType.Bishop => w ? whiteBishop : blackBishop,
                PieceType.Rook => w ? whiteRook : blackRook,
                PieceType.Queen => w ? whiteQueen : blackQueen,
                PieceType.King => w ? whiteKing : blackKing,
                _ => null
            };
        }
    }
}
