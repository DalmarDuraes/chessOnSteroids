using System.Collections.Generic;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Core.Tests
{
    public class CaptureVictimSquaresTests
    {
        [Test]
        public void NormalCapture_IncludesOccupiedDestinationSquare()
        {
            var b = Fen.Parse("4k3/8/8/3p4/4B3/8/8/4K3 w - - 0 1");
            var moves = MoveGenerator.GenerateLegalMoves(b);
            int from = Square.FromAlgebraic("e4");
            var victims = new HashSet<int>();
            CaptureVictimSquares.AddVictims(b, from, moves, victims);
            Assert.IsTrue(victims.Contains(Square.FromAlgebraic("d5")));
        }

        [Test]
        public void EnPassant_IncludesVictimPawnSquareNotEpTargetSquare()
        {
            var b = Fen.Parse("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
            var moves = MoveGenerator.GenerateLegalMoves(b);
            int from = Square.FromAlgebraic("e5");
            var victims = new HashSet<int>();
            CaptureVictimSquares.AddVictims(b, from, moves, victims);
            Assert.IsTrue(victims.Contains(Square.FromAlgebraic("d5")));
        }
    }
}
