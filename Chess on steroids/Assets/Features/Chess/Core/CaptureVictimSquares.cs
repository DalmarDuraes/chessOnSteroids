using System.Collections.Generic;

namespace Chess.Core
{
    /// <summary>
    /// Victim squares for captures from <paramref name="from"/> given current <paramref name="board"/> and <paramref name="legal"/> moves.
    /// </summary>
    public static class CaptureVictimSquares
    {
        public static void AddVictims(Board board, int from, IReadOnlyList<Move> legal, HashSet<int> dest)
        {
            dest.Clear();
            Color us = board.SideToMove;
            for (int i = 0; i < legal.Count; i++)
            {
                Move m = legal[i];
                if (m.From != from) continue;

                if ((m.Flags & MoveFlag.EnPassant) != 0)
                {
                    int victimSq = Square.Index(Square.File(m.To), Square.Rank(from));
                    dest.Add(victimSq);
                    continue;
                }

                Piece target = board.Squares[m.To];
                if (!target.IsEmpty && target.Color != us)
                    dest.Add(m.To);
            }
        }
    }
}
