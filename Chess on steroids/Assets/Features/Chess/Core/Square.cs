namespace Chess.Core
{
    /// <summary>
    /// Board coordinates: file a–h → 0–7, chess rank 1–8 → 0–7 (rank 0 is White’s back rank).
    /// Index = rank * 8 + file. White pieces start on ranks 0–1; Black on 6–7.
    /// World mapping in Unity: increasing file → +X, increasing rank (1→8) → +Z (see ChessBoardView).
    /// </summary>
    public static class Square
    {
        public const int Invalid = -1;

        public static int Index(int file, int rank) => rank * 8 + file;

        public static int File(int square) => square & 7;

        public static int Rank(int square) => square >> 3;

        public static bool IsValid(int square) => square >= 0 && square < 64;

        public static string ToAlgebraic(int square)
        {
            if (!IsValid(square)) return "?";
            return $"{(char)('a' + File(square))}{Rank(square) + 1}";
        }

        public static int FromAlgebraic(string algebraic)
        {
            if (string.IsNullOrEmpty(algebraic) || algebraic.Length < 2) return Invalid;
            int file = char.ToLowerInvariant(algebraic[0]) - 'a';
            int rank = algebraic[1] - '1';
            if (file is < 0 or > 7 || rank is < 0 or > 7) return Invalid;
            return Index(file, rank);
        }
    }
}
