using System;

namespace Chess.Core
{
    public enum MoveFlag : byte
    {
        None = 0,
        EnPassant = 1,
        CastleKing = 2,
        CastleQueen = 4
    }

    public readonly struct Move : IEquatable<Move>
    {
        public int From { get; }
        public int To { get; }
        public PieceType Promotion { get; }
        public MoveFlag Flags { get; }

        public Move(int from, int to, PieceType promotion = PieceType.None, MoveFlag flags = MoveFlag.None)
        {
            From = from;
            To = to;
            Promotion = promotion;
            Flags = flags;
        }

        public bool Equals(Move other) =>
            From == other.From && To == other.To &&
            Promotion == other.Promotion && Flags == other.Flags;

        public override bool Equals(object obj) => obj is Move m && Equals(m);

        public override int GetHashCode() => HashCode.Combine(From, To, Promotion, Flags);

        public static bool operator ==(Move a, Move b) => a.Equals(b);

        public static bool operator !=(Move a, Move b) => !a.Equals(b);

        public override string ToString()
        {
            string s = $"{Square.ToAlgebraic(From)}{Square.ToAlgebraic(To)}";
            if (Promotion != PieceType.None) s += char.ToLowerInvariant(Promotion switch
            {
                PieceType.Queen => 'q',
                PieceType.Rook => 'r',
                PieceType.Bishop => 'b',
                PieceType.Knight => 'n',
                _ => '?'
            });
            return s;
        }
    }
}
