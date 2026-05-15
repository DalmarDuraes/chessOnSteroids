using Chess.Core;
using NUnit.Framework;

namespace Chess.Core.Tests
{
    public class MoveGeneratorTests
    {
        [Test]
        public void StartingPosition_HasTwentyLegalMoves()
        {
            var b = Board.StartingPosition();
            var moves = MoveGenerator.GenerateLegalMoves(b);
            Assert.AreEqual(20, moves.Count);
        }

        [Test]
        public void EnPassant_IsOnlyAvailableImmediately()
        {
            var b = Fen.Parse("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
            var moves = MoveGenerator.GenerateLegalMoves(b);
            bool hasEp = false;
            foreach (var m in moves)
            {
                if ((m.Flags & MoveFlag.EnPassant) != 0 && m.From == Square.FromAlgebraic("e5"))
                    hasEp = true;
            }
            Assert.IsTrue(hasEp);
        }

        [Test]
        public void CastleKingSide_WhenRightsAndPathClear()
        {
            var b = Fen.Parse("r3k2r/pppppppp/8/8/8/8/PPPPPPPP/R3K2R w KQkq - 0 1");
            var moves = MoveGenerator.GenerateLegalMoves(b);
            bool has = false;
            foreach (var m in moves)
            {
                if ((m.Flags & MoveFlag.CastleKing) != 0) has = true;
            }
            Assert.IsTrue(has);
        }

        [Test]
        public void PawnAdvance_ToBackRank_OffersFourPromotionMoves()
        {
            var b = Fen.Parse("8/P7/8/8/8/8/8/4K2k w - - 0 1");
            var moves = MoveGenerator.GenerateLegalMoves(b);
            int from = Square.FromAlgebraic("a7");
            int to = Square.FromAlgebraic("a8");
            var promotions = new System.Collections.Generic.HashSet<PieceType>();
            foreach (var m in moves)
            {
                if (m.From == from && m.To == to)
                    promotions.Add(m.Promotion);
            }

            Assert.AreEqual(4, promotions.Count);
            Assert.IsTrue(promotions.Contains(PieceType.Queen));
            Assert.IsTrue(promotions.Contains(PieceType.Rook));
            Assert.IsTrue(promotions.Contains(PieceType.Bishop));
            Assert.IsTrue(promotions.Contains(PieceType.Knight));
        }
    }
}
