using System.Collections.Generic;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Core.Tests
{
    public sealed class CapabilityMovementTests
    {
        [Test]
        public void KingStep_OnRook_MergesWithRookSlides()
        {
            var b = Fen.Parse("8/8/8/3r4/8/8/8/4K2k b - - 0 1");
            int rookSq = Square.FromAlgebraic("d5");
            Assert.AreEqual(PieceType.Rook, b.Squares[rookSq].Type);

            b.AddCapability(rookSq, PieceMovementCapability.KingStepLike);
            Assert.AreEqual(
                PieceMovementCapability.RookLike | PieceMovementCapability.KingStepLike,
                b.MovementCapabilitiesOnSquare[rookSq]);

            var legal = MoveGenerator.GenerateLegalMoves(b);
            Assert.That(legal, Is.Not.Empty);
            CollectionAssert.Contains(ExtractTos(rookSq, legal), Square.FromAlgebraic("d6"));
        }

        [Test]
        public void StripAllCapabilitiesFromPawn_HasNoLegalMovesFromThatPiece()
        {
            var b = Fen.Parse("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1");
            int pawnSq = Square.FromAlgebraic("e2");

            var movesBefore = MoveGenerator.GenerateLegalMoves(b);
            Assert.That(movesBefore.Exists(m => m.From == pawnSq), Is.True);

            b.RemoveCapability(pawnSq, PieceMovementCapability.PawnLike);

            var movesAfter = MoveGenerator.GenerateLegalMoves(b);
            foreach (Move m in movesAfter)
                Assert.AreNotEqual(pawnSq, m.From);
        }

        [Test]
        public void StartingRook_WithoutRookCapability_CannotMoveThatRook()
        {
            Board b = Board.StartingPosition();
            int rookSq = Square.FromAlgebraic("a1");
            Assert.AreEqual(PieceType.Rook, b.Squares[rookSq].Type);

            b.RemoveCapability(rookSq, PieceMovementCapability.RookLike);

            foreach (Move m in MoveGenerator.GenerateLegalMoves(b))
                Assert.AreNotEqual(rookSq, m.From);
        }
        [Test]
        public void Pawn_WithRookCapability_CanSlideOrthogonally()
        {
            var b = Fen.Parse("4k3/8/8/8/3P4/8/8/4K3 w - - 0 1");
            int pawnSq = Square.FromAlgebraic("d4");

            var before = MoveGenerator.GenerateLegalMoves(b);
            CollectionAssert.Contains(ExtractTos(pawnSq, before), Square.FromAlgebraic("d5"));

            b.AddCapability(pawnSq, PieceMovementCapability.RookLike);
            var after = MoveGenerator.GenerateLegalMoves(b);
            CollectionAssert.Contains(ExtractTos(pawnSq, after), Square.FromAlgebraic("d8"));
        }

        [Test]
        public void Promotion_CarriesKingStep_OnQueen_WhenPawnHadIt()
        {
            var b = Fen.Parse("8/P7/8/8/8/8/8/4K2k w - - 0 1");
            int pawnSq = Square.FromAlgebraic("a7");
            b.AddCapability(pawnSq, PieceMovementCapability.KingStepLike);
            var move = new Move(pawnSq, Square.FromAlgebraic("a8"), PieceType.Queen);
            b.ApplyMove(move);

            int queenSq = Square.FromAlgebraic("a8");
            Assert.AreEqual(PieceType.Queen, b.Squares[queenSq].Type);
            Assert.That((b.MovementCapabilitiesOnSquare[queenSq] & PieceMovementCapability.KingStepLike) != 0);
        }

        [Test]
        public void King_RemoveKingStep_IsIgnored_KingKeepsKingStepAndSomeLegalMove()
        {
            var b = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            int kingSq = Square.FromAlgebraic("e1");
            Assert.AreEqual(PieceType.King, b.Squares[kingSq].Type);

            b.RemoveCapability(kingSq, PieceMovementCapability.KingStepLike);
            Assert.That((b.MovementCapabilitiesOnSquare[kingSq] & PieceMovementCapability.KingStepLike) != 0);
            Assert.That(MoveGenerator.GenerateLegalMoves(b).Exists(m => m.From == kingSq), Is.True);
        }

        [Test]
        public void NonKing_CanStillRemoveKingStep()
        {
            var b = Fen.Parse("8/8/8/3r4/8/8/8/4K2k b - - 0 1");
            int rookSq = Square.FromAlgebraic("d5");
            b.AddCapability(rookSq, PieceMovementCapability.KingStepLike);
            Assert.That((b.MovementCapabilitiesOnSquare[rookSq] & PieceMovementCapability.KingStepLike) != 0);

            b.RemoveCapability(rookSq, PieceMovementCapability.KingStepLike);
            Assert.That((b.MovementCapabilitiesOnSquare[rookSq] & PieceMovementCapability.KingStepLike), Is.EqualTo(0));
        }

        [Test]
        public void NonKing_WithKingStep_LetsIsSquareAttackedSeeOrthogonalAdjacentKing()
        {
            var b = Fen.Parse("8/8/8/4K3/4b3/8/8/8 w - - 0 1");
            int kingSq = Square.FromAlgebraic("e5");
            int bishopSq = Square.FromAlgebraic("e4");
            Assert.AreEqual(Color.White, b.Squares[kingSq].Color);
            Assert.AreEqual(Color.Black, b.Squares[bishopSq].Color);
            Assert.AreEqual(PieceType.Bishop, b.Squares[bishopSq].Type);

            Assert.IsFalse(b.IsSquareAttacked(kingSq, Color.Black));

            b.AddCapability(bishopSq, PieceMovementCapability.KingStepLike);
            Assert.IsTrue(b.IsSquareAttacked(kingSq, Color.Black));
            Assert.IsTrue(b.IsInCheck(Color.White));
        }

        static List<int> ExtractTos(int from, List<Move> moves)
        {
            var r = new List<int>();
            foreach (Move m in moves)
            {
                if (m.From == from) r.Add(m.To);
            }

            return r;
        }
    }
}
