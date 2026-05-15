namespace Chess.Core
{
    public readonly struct Piece
    {
        public PieceType Type { get; }
        public Color Color { get; }

        public Piece(PieceType type, Color color)
        {
            Type = type;
            Color = color;
        }

        public bool IsEmpty => Type == PieceType.None;

        public static Piece Empty => new Piece(PieceType.None, Color.White);

        public static Piece FromChar(char c)
        {
            Color color = char.IsUpper(c) ? Color.White : Color.Black;
            switch (char.ToLowerInvariant(c))
            {
                case 'p': return new Piece(PieceType.Pawn, color);
                case 'n': return new Piece(PieceType.Knight, color);
                case 'b': return new Piece(PieceType.Bishop, color);
                case 'r': return new Piece(PieceType.Rook, color);
                case 'q': return new Piece(PieceType.Queen, color);
                case 'k': return new Piece(PieceType.King, color);
                default: return Empty;
            }
        }

        public override string ToString() =>
            IsEmpty
                ? "."
                : $"{(Color == Color.White ? char.ToUpperInvariant(TypeLetter()) : char.ToLowerInvariant(TypeLetter()))}";

        char TypeLetter()
        {
            switch (Type)
            {
                case PieceType.Pawn: return 'p';
                case PieceType.Knight: return 'n';
                case PieceType.Bishop: return 'b';
                case PieceType.Rook: return 'r';
                case PieceType.Queen: return 'q';
                case PieceType.King: return 'k';
                default: return '?';
            }
        }
    }
}
