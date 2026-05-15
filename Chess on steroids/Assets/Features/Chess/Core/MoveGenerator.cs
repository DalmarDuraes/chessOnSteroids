using System;
using System.Collections.Generic;

namespace Chess.Core
{
    public static class MoveGenerator
    {
        public static List<Move> GenerateLegalMoves(Board board)
        {
            var pseudo = new List<Move>(48);
            GeneratePseudoLegalMoves(board, pseudo);
            var legal = new List<Move>(pseudo.Count);
            Color us = board.SideToMove;
            foreach (var m in pseudo)
            {
                Board copy = board.Clone();
                copy.ApplyMove(m);
                if (!copy.IsInCheck(us)) legal.Add(m);
            }
            return legal;
        }

        public static void GeneratePseudoLegalMoves(Board board, List<Move> dest)
        {
            dest.Clear();
            Color us = board.SideToMove;
            for (int sq = 0; sq < 64; sq++)
            {
                var pc = board.Squares[sq];
                if (pc.IsEmpty || pc.Color != us) continue;

                PieceMovementCapability caps = board.MovementCapabilitiesOnSquare[sq];
                if (caps == PieceMovementCapability.None) continue;

                if ((caps & PieceMovementCapability.PawnLike) != 0)
                    AddPawnMoves(board, sq, us, dest);
                if ((caps & PieceMovementCapability.KnightLike) != 0)
                    AddKnightMoves(board, sq, us, dest);
                if ((caps & PieceMovementCapability.BishopLike) != 0)
                    AddSliderMoves(board, sq, us, dest, DiagonalDeltas);
                if ((caps & PieceMovementCapability.RookLike) != 0)
                    AddSliderMoves(board, sq, us, dest, OrthoDeltas);
                if ((caps & PieceMovementCapability.KingStepLike) != 0)
                    AddKingMoves(board, sq, us, dest);
                if (pc.Type == PieceType.King)
                    AddCastling(board, sq, us, dest);
            }

            DedupeMoves(dest);
        }

        static readonly int[] DiagonalDeltas = { -9, -7, 7, 9 };
        static readonly int[] OrthoDeltas = { -8, -1, 1, 8 };

        static void DedupeMoves(List<Move> moves)
        {
            if (moves.Count <= 1) return;
            var seen = new HashSet<Move>();
            int write = 0;
            for (int read = 0; read < moves.Count; read++)
            {
                Move m = moves[read];
                if (seen.Add(m))
                    moves[write++] = m;
            }

            moves.RemoveRange(write, moves.Count - write);
        }

        static void AddPawnMoves(Board board, int from, Color us, List<Move> dest)
        {
            int r = Square.Rank(from);
            int forward = us == Color.White ? 8 : -8;
            int startRank = us == Color.White ? 1 : 6;
            int promoRank = us == Color.White ? 7 : 0;

            int one = from + forward;
            if (Square.IsValid(one) && board.Squares[one].IsEmpty)
            {
                if (Square.Rank(one) == promoRank)
                    AddPromotionMoves(dest, from, one);
                else
                {
                    dest.Add(new Move(from, one));
                    if (r == startRank)
                    {
                        int two = from + forward * 2;
                        if (board.Squares[two].IsEmpty)
                            dest.Add(new Move(from, two));
                    }
                }
            }

            foreach (int d in us == Color.White ? new[] { 7, 9 } : new[] { -9, -7 })
            {
                if (!Board.TryStep(from, d, out int to)) continue;
                var target = board.Squares[to];
                if (board.EnPassantTarget == to && target.IsEmpty)
                {
                    int victimSq = Square.Index(Square.File(to), Square.Rank(from));
                    var victim = board.Squares[victimSq];
                    if (victim.Type == PieceType.Pawn && victim.Color != us)
                        dest.Add(new Move(from, to, PieceType.None, MoveFlag.EnPassant));
                    continue;
                }

                if (!target.IsEmpty && target.Color != us)
                {
                    if (Square.Rank(to) == promoRank)
                        AddPromotionMoves(dest, from, to);
                    else
                        dest.Add(new Move(from, to));
                }
            }
        }

        static void AddPromotionMoves(List<Move> dest, int from, int to)
        {
            dest.Add(new Move(from, to, PieceType.Queen));
            dest.Add(new Move(from, to, PieceType.Rook));
            dest.Add(new Move(from, to, PieceType.Bishop));
            dest.Add(new Move(from, to, PieceType.Knight));
        }

        static void AddKnightMoves(Board board, int from, Color us, List<Move> dest)
        {
            ReadOnlySpan<int> deltas = stackalloc int[] { 17, 15, 10, 6, -6, -10, -15, -17 };
            int fr = Square.File(from), rr = Square.Rank(from);
            foreach (int d in deltas)
            {
                int to = from + d;
                if (!Square.IsValid(to)) continue;
                int tf = Square.File(to), tr = Square.Rank(to);
                if ((Abs(tf - fr) == 1 && Abs(tr - rr) == 2) || (Abs(tf - fr) == 2 && Abs(tr - rr) == 1))
                {
                    var t = board.Squares[to];
                    if (t.IsEmpty || t.Color != us)
                        dest.Add(new Move(from, to));
                }
            }
        }

        static int Abs(int v) => v < 0 ? -v : v;

        static void AddSliderMoves(Board board, int from, Color us, List<Move> dest, int[] deltas)
        {
            foreach (int d in deltas)
            {
                int s = from;
                while (Board.TryStep(s, d, out int next))
                {
                    s = next;
                    var t = board.Squares[s];
                    if (t.IsEmpty) dest.Add(new Move(from, s));
                    else
                    {
                        if (t.Color != us) dest.Add(new Move(from, s));
                        break;
                    }
                }
            }
        }

        static void AddKingMoves(Board board, int from, Color us, List<Move> dest)
        {
            ReadOnlySpan<int> deltas = stackalloc int[] { -9, -8, -7, -1, 1, 7, 8, 9 };
            foreach (int d in deltas)
            {
                if (!Board.TryStep(from, d, out int to)) continue;
                var t = board.Squares[to];
                if (t.IsEmpty || t.Color != us)
                    dest.Add(new Move(from, to));
            }
        }

        static void AddCastling(Board board, int from, Color us, List<Move> dest)
        {
            int kingHome = us == Color.White ? Square.Index(4, 0) : Square.Index(4, 7);
            if (from != kingHome || board.Squares[from].Type != PieceType.King) return;
            if (board.IsInCheck(us)) return;

            if (us == Color.White && board.CastleWhiteKing)
            {
                int f1 = Square.Index(5, 0), g1 = Square.Index(6, 0);
                if (board.Squares[f1].IsEmpty && board.Squares[g1].IsEmpty
                    && !board.IsSquareAttacked(f1, Color.Black)
                    && !board.IsSquareAttacked(g1, Color.Black))
                    dest.Add(new Move(from, g1, PieceType.None, MoveFlag.CastleKing));
            }
            else if (us == Color.Black && board.CastleBlackKing)
            {
                int f8 = Square.Index(5, 7), g8 = Square.Index(6, 7);
                if (board.Squares[f8].IsEmpty && board.Squares[g8].IsEmpty
                    && !board.IsSquareAttacked(f8, Color.White)
                    && !board.IsSquareAttacked(g8, Color.White))
                    dest.Add(new Move(from, g8, PieceType.None, MoveFlag.CastleKing));
            }

            if (us == Color.White && board.CastleWhiteQueen)
            {
                int d1 = Square.Index(3, 0), c1 = Square.Index(2, 0), b1 = Square.Index(1, 0);
                if (board.Squares[d1].IsEmpty && board.Squares[c1].IsEmpty && board.Squares[b1].IsEmpty
                    && !board.IsSquareAttacked(d1, Color.Black)
                    && !board.IsSquareAttacked(c1, Color.Black))
                    dest.Add(new Move(from, c1, PieceType.None, MoveFlag.CastleQueen));
            }
            else if (us == Color.Black && board.CastleBlackQueen)
            {
                int d8 = Square.Index(3, 7), c8 = Square.Index(2, 7), b8 = Square.Index(1, 7);
                if (board.Squares[d8].IsEmpty && board.Squares[c8].IsEmpty && board.Squares[b8].IsEmpty
                    && !board.IsSquareAttacked(d8, Color.White)
                    && !board.IsSquareAttacked(c8, Color.White))
                    dest.Add(new Move(from, c8, PieceType.None, MoveFlag.CastleQueen));
            }
        }
    }
}
