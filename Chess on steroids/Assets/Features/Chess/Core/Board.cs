using System;
using System.Text;

namespace Chess.Core
{
    public sealed class Board
    {
        public Piece[] Squares { get; } = new Piece[64];
        /// <summary>Movement families enabled for each square index. Empty squares must remain <see cref="PieceMovementCapability.None"/>.</summary>
        public PieceMovementCapability[] MovementCapabilitiesOnSquare { get; } = new PieceMovementCapability[64];
        public Color SideToMove { get; set; }
        public bool CastleWhiteKing { get; set; }
        public bool CastleWhiteQueen { get; set; }
        public bool CastleBlackKing { get; set; }
        public bool CastleBlackQueen { get; set; }
        public int EnPassantTarget { get; set; } = Square.Invalid;
        public int HalfmoveClock { get; set; }
        public int FullmoveNumber { get; set; } = 1;

        public Board Clone()
        {
            var c = new Board
            {
                SideToMove = SideToMove,
                CastleWhiteKing = CastleWhiteKing,
                CastleWhiteQueen = CastleWhiteQueen,
                CastleBlackKing = CastleBlackKing,
                CastleBlackQueen = CastleBlackQueen,
                EnPassantTarget = EnPassantTarget,
                HalfmoveClock = HalfmoveClock,
                FullmoveNumber = FullmoveNumber
            };
            Array.Copy(Squares, c.Squares, 64);
            Array.Copy(MovementCapabilitiesOnSquare, c.MovementCapabilitiesOnSquare, 64);
            return c;
        }

        public static Board StartingPosition() => Fen.Parse(Fen.StartFen);

        public int FindKing(Color color)
        {
            for (int i = 0; i < 64; i++)
            {
                var p = Squares[i];
                if (!p.IsEmpty && p.Type == PieceType.King && p.Color == color) return i;
            }
            return Square.Invalid;
        }

        public bool IsInCheck(Color kingColor)
        {
            int ksq = FindKing(kingColor);
            if (ksq == Square.Invalid) return false;
            return IsSquareAttacked(ksq, kingColor.Opponent());
        }

        public static bool TryStep(int from, int delta, out int to)
        {
            to = from + delta;
            if (to is < 0 or > 63) return false;
            int ff = Square.File(from), fr = Square.Rank(from);
            int tf = Square.File(to), tr = Square.Rank(to);
            switch (delta)
            {
                case 1: return ff < 7 && tr == fr && tf == ff + 1;
                case -1: return ff > 0 && tr == fr && tf == ff - 1;
                case 8: return fr < 7 && tf == ff && tr == fr + 1;
                case -8: return fr > 0 && tf == ff && tr == fr - 1;
                case 9: return ff < 7 && fr < 7 && tf == ff + 1 && tr == fr + 1;
                case 7: return ff > 0 && fr < 7 && tf == ff - 1 && tr == fr + 1;
                case -7: return ff < 7 && fr > 0 && tf == ff + 1 && tr == fr - 1;
                case -9: return ff > 0 && fr > 0 && tf == ff - 1 && tr == fr - 1;
                default: return false;
            }
        }

        public bool IsSquareAttacked(int targetSquare, Color byAttacker)
        {
            int tf = Square.File(targetSquare);
            int tr = Square.Rank(targetSquare);

            if (byAttacker == Color.White)
            {
                if (tr >= 1)
                {
                    if (tf > 0)
                    {
                        int sq = Square.Index(tf - 1, tr - 1);
                        var pc = Squares[sq];
                        if (pc.Color == Color.White &&
                            (MovementCapabilitiesOnSquare[sq] & PieceMovementCapability.PawnLike) != 0)
                            return true;
                    }
                    if (tf < 7)
                    {
                        int sq = Square.Index(tf + 1, tr - 1);
                        var pc = Squares[sq];
                        if (pc.Color == Color.White &&
                            (MovementCapabilitiesOnSquare[sq] & PieceMovementCapability.PawnLike) != 0)
                            return true;
                    }
                }
            }
            else
            {
                if (tr <= 6)
                {
                    if (tf > 0)
                    {
                        int sq = Square.Index(tf - 1, tr + 1);
                        var pc = Squares[sq];
                        if (pc.Color == Color.Black &&
                            (MovementCapabilitiesOnSquare[sq] & PieceMovementCapability.PawnLike) != 0)
                            return true;
                    }
                    if (tf < 7)
                    {
                        int sq = Square.Index(tf + 1, tr + 1);
                        var pc = Squares[sq];
                        if (pc.Color == Color.Black &&
                            (MovementCapabilitiesOnSquare[sq] & PieceMovementCapability.PawnLike) != 0)
                            return true;
                    }
                }
            }

            ReadOnlySpan<int> knightDeltas = stackalloc int[] { 17, 15, 10, 6, -6, -10, -15, -17 };
            foreach (int d in knightDeltas)
            {
                int s = targetSquare + d;
                if (!Square.IsValid(s)) continue;
                int df = Math.Abs(Square.File(s) - tf);
                int dr = Math.Abs(Square.Rank(s) - tr);
                if ((df != 1 || dr != 2) && (df != 2 || dr != 1)) continue;
                var pc = Squares[s];
                if (pc.Color == byAttacker &&
                    (MovementCapabilitiesOnSquare[s] & PieceMovementCapability.KnightLike) != 0)
                    return true;
            }

            ReadOnlySpan<int> kingDeltas = stackalloc int[] { -9, -8, -7, -1, 1, 7, 8, 9 };
            foreach (int d in kingDeltas)
            {
                if (!TryStep(targetSquare, d, out int s)) continue;
                var pc = Squares[s];
                if (pc.Color == byAttacker &&
                    (MovementCapabilitiesOnSquare[s] & PieceMovementCapability.KingStepLike) != 0)
                    return true;
            }

            ReadOnlySpan<int> rookDeltas = stackalloc int[] { -8, -1, 1, 8 };
            foreach (int d in rookDeltas)
            {
                int s = targetSquare;
                while (TryStep(s, d, out int next))
                {
                    s = next;
                    var pc = Squares[s];
                    if (pc.IsEmpty) continue;
                    if (pc.Color == byAttacker &&
                        (MovementCapabilitiesOnSquare[s] & PieceMovementCapability.RookLike) != 0)
                        return true;
                    break;
                }
            }

            ReadOnlySpan<int> bishopDeltas = stackalloc int[] { -9, -7, 7, 9 };
            foreach (int d in bishopDeltas)
            {
                int s = targetSquare;
                while (TryStep(s, d, out int next))
                {
                    s = next;
                    var pc = Squares[s];
                    if (pc.IsEmpty) continue;
                    if (pc.Color == byAttacker &&
                        (MovementCapabilitiesOnSquare[s] & PieceMovementCapability.BishopLike) != 0)
                        return true;
                    break;
                }
            }

            return false;
        }

        /// <summary>OR-s <paramref name="mask"/> onto capabilities at <paramref name="square"/> after validation (empty square rejected).</summary>
        public void AddCapability(int square, PieceMovementCapability mask)
        {
            if (!Square.IsValid(square)) return;
            if (Squares[square].IsEmpty) return;
            mask = PieceMovementCapabilityRules.FilterGrantMask(Squares[square].Type, mask);
            MovementCapabilitiesOnSquare[square] |= mask;
        }

        /// <summary>Clears each set bit of <paramref name="mask"/> at <paramref name="square"/>. <see cref="PieceType.King"/> never loses <see cref="PieceMovementCapability.KingStepLike"/>.</summary>
        public void RemoveCapability(int square, PieceMovementCapability mask)
        {
            if (!Square.IsValid(square)) return;
            if (Squares[square].Type == PieceType.King)
                mask &= ~PieceMovementCapability.KingStepLike;
            MovementCapabilitiesOnSquare[square] &= ~mask;
        }

        public void ApplyMove(Move m, bool switchTurn = true)
        {
            Piece piece = Squares[m.From];
            PieceMovementCapability fromCaps = MovementCapabilitiesOnSquare[m.From];
            Color us = piece.Color;
            bool captureOrEp = false;

            if ((m.Flags & MoveFlag.EnPassant) != 0)
            {
                int victimSq = Square.Index(Square.File(m.To), Square.Rank(m.From));
                Squares[victimSq] = Piece.Empty;
                MovementCapabilitiesOnSquare[victimSq] = PieceMovementCapability.None;
                captureOrEp = true;
            }
            else if (!Squares[m.To].IsEmpty && Squares[m.To].Color != us)
                captureOrEp = true;

            Squares[m.From] = Piece.Empty;
            MovementCapabilitiesOnSquare[m.From] = PieceMovementCapability.None;

            if ((m.Flags & MoveFlag.CastleKing) != 0)
            {
                if (us == Color.White)
                {
                    int rh = Square.Index(7, 0), fk = Square.Index(5, 0), tg = Square.Index(6, 0);
                    PieceMovementCapability rookCaps = MovementCapabilitiesOnSquare[rh];
                    Squares[rh] = Piece.Empty;
                    MovementCapabilitiesOnSquare[rh] = PieceMovementCapability.None;
                    Squares[fk] = new Piece(PieceType.Rook, Color.White);
                    MovementCapabilitiesOnSquare[fk] = rookCaps;
                    Squares[tg] = new Piece(PieceType.King, Color.White);
                    MovementCapabilitiesOnSquare[tg] = fromCaps;
                }
                else
                {
                    int rh = Square.Index(7, 7), fk = Square.Index(5, 7), tg = Square.Index(6, 7);
                    PieceMovementCapability rookCaps = MovementCapabilitiesOnSquare[rh];
                    Squares[rh] = Piece.Empty;
                    MovementCapabilitiesOnSquare[rh] = PieceMovementCapability.None;
                    Squares[fk] = new Piece(PieceType.Rook, Color.Black);
                    MovementCapabilitiesOnSquare[fk] = rookCaps;
                    Squares[tg] = new Piece(PieceType.King, Color.Black);
                    MovementCapabilitiesOnSquare[tg] = fromCaps;
                }

                ClearCastlingRightsFor(us);
                FinishMove(us, piece, m, false, switchTurn);
                return;
            }

            if ((m.Flags & MoveFlag.CastleQueen) != 0)
            {
                if (us == Color.White)
                {
                    int ra = Square.Index(0, 0), dk = Square.Index(3, 0), tg = Square.Index(2, 0);
                    PieceMovementCapability rookCaps = MovementCapabilitiesOnSquare[ra];
                    Squares[ra] = Piece.Empty;
                    MovementCapabilitiesOnSquare[ra] = PieceMovementCapability.None;
                    Squares[dk] = new Piece(PieceType.Rook, Color.White);
                    MovementCapabilitiesOnSquare[dk] = rookCaps;
                    Squares[tg] = new Piece(PieceType.King, Color.White);
                    MovementCapabilitiesOnSquare[tg] = fromCaps;
                }
                else
                {
                    int ra = Square.Index(0, 7), dk = Square.Index(3, 7), tg = Square.Index(2, 7);
                    PieceMovementCapability rookCaps = MovementCapabilitiesOnSquare[ra];
                    Squares[ra] = Piece.Empty;
                    MovementCapabilitiesOnSquare[ra] = PieceMovementCapability.None;
                    Squares[dk] = new Piece(PieceType.Rook, Color.Black);
                    MovementCapabilitiesOnSquare[dk] = rookCaps;
                    Squares[tg] = new Piece(PieceType.King, Color.Black);
                    MovementCapabilitiesOnSquare[tg] = fromCaps;
                }

                ClearCastlingRightsFor(us);
                FinishMove(us, piece, m, false, switchTurn);
                return;
            }

            Piece placed = piece;
            if (piece.Type == PieceType.Pawn && (Square.Rank(m.To) is 0 or 7))
                placed = new Piece(m.Promotion == PieceType.None ? PieceType.Queen : m.Promotion, piece.Color);
            else if (m.Promotion != PieceType.None)
                placed = new Piece(m.Promotion, piece.Color);

            PieceMovementCapability capsTo = PieceMovementCapabilityRules.SanitizeForPieceType(placed.Type, fromCaps);
            Squares[m.To] = placed;
            MovementCapabilitiesOnSquare[m.To] = capsTo;

            UpdateCastlingRightsAfterNormalMove(m, piece, us);
            FinishMove(us, piece, m, captureOrEp, switchTurn);
        }

        void ClearCastlingRightsFor(Color us)
        {
            if (us == Color.White) { CastleWhiteKing = false; CastleWhiteQueen = false; }
            else { CastleBlackKing = false; CastleBlackQueen = false; }
        }

        void UpdateCastlingRightsAfterNormalMove(Move m, Piece moved, Color us)
        {
            if (moved.Type == PieceType.King)
                ClearCastlingRightsFor(us);

            if (moved.Type == PieceType.Rook)
            {
                if (m.From == Square.Index(0, 0)) CastleWhiteQueen = false;
                if (m.From == Square.Index(7, 0)) CastleWhiteKing = false;
                if (m.From == Square.Index(0, 7)) CastleBlackQueen = false;
                if (m.From == Square.Index(7, 7)) CastleBlackKing = false;
            }

            if (m.To == Square.Index(0, 0)) CastleWhiteQueen = false;
            if (m.To == Square.Index(7, 0)) CastleWhiteKing = false;
            if (m.To == Square.Index(0, 7)) CastleBlackQueen = false;
            if (m.To == Square.Index(7, 7)) CastleBlackKing = false;
        }

        void FinishMove(Color us, Piece piece, Move m, bool captureOrEp, bool switchTurn)
        {
            if (piece.Type == PieceType.Pawn && Square.File(m.From) == Square.File(m.To) && Math.Abs(m.To - m.From) == 16)
                EnPassantTarget = (m.From + m.To) >> 1;
            else
                EnPassantTarget = Square.Invalid;

            if (piece.Type == PieceType.Pawn || captureOrEp)
                HalfmoveClock = 0;
            else
                HalfmoveClock++;

            if (!switchTurn)
                return;

            if (us == Color.Black)
                FullmoveNumber++;

            SideToMove = SideToMove.Opponent();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int f = 0; f < 8; f++)
                {
                    var p = Squares[Square.Index(f, r)];
                    if (p.IsEmpty) empty++;
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        sb.Append(p.ToString());
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (r > 0) sb.Append('/');
            }
            return sb.ToString();
        }
    }
}
