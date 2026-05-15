using Chess.Core;
using NUnit.Framework;

namespace Chess.Core.Tests
{
    /// <summary>Turn bookkeeping for <see cref="Board.ApplyMove"/> when <c>switchTurn: false</c> (pawn gimmick half-ply).</summary>
    public sealed class DoublePawnGimmickBoardTests
    {
        [Test]
        public void White_Pawn_Move_Without_Ending_Turn_Keeps_Side_To_Move_And_Fullmove_Number()
        {
            var b = Board.StartingPosition();
            Assert.AreEqual(Color.White, b.SideToMove);
            Assert.AreEqual(1, b.FullmoveNumber);

            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"));
            b.ApplyMove(move, switchTurn: false);

            Assert.AreEqual(Color.White, b.SideToMove);
            Assert.AreEqual(1, b.FullmoveNumber);
        }

        [Test]
        public void Black_Pawn_Move_Without_Ending_Turn_Keeps_Side_And_Fullmove()
        {
            var b = Board.StartingPosition();
            b.ApplyMove(new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4")));

            Assert.AreEqual(Color.Black, b.SideToMove);
            Assert.AreEqual(1, b.FullmoveNumber);

            b.ApplyMove(new Move(Square.FromAlgebraic("e7"), Square.FromAlgebraic("e5")), switchTurn: false);

            Assert.AreEqual(Color.Black, b.SideToMove);
            Assert.AreEqual(1, b.FullmoveNumber);
        }

        [Test]
        public void Black_Pawn_With_SwitchTurn_True_Ends_Turn_And_Increments_Fullmove()
        {
            var b = Board.StartingPosition();
            b.ApplyMove(new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4")));

            b.ApplyMove(new Move(Square.FromAlgebraic("e7"), Square.FromAlgebraic("e5")), switchTurn: true);

            Assert.AreEqual(Color.White, b.SideToMove);
            Assert.AreEqual(2, b.FullmoveNumber);
        }
    }
}
