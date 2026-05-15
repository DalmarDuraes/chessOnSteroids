using System;

namespace Chess.Core
{
    /// <summary>
    /// Which movement generators may run for a piece on this square. Orthogonal chess <see cref="PieceType"/> is unchanged (royalty is still only <see cref="PieceType.King"/>).
    /// <see cref="KingStepLike"/> is king-adjacency geometry only; augments may add it on any piece; castling still requires <see cref="PieceType.King"/>.
    /// </summary>
    [Flags]
    public enum PieceMovementCapability : ushort
    {
        None = 0,
        PawnLike = 1 << 0,
        KnightLike = 1 << 1,
        BishopLike = 1 << 2,
        RookLike = 1 << 3,
        KingStepLike = 1 << 4,

        /// <summary>Bishop + rook sliders (queen sliders). Separate from pawn/knight/king-step.</summary>
        QueenSlides = BishopLike | RookLike
    }

    public static class PieceMovementCapabilityDefaults
    {
        public static PieceMovementCapability For(PieceType type) =>
            type switch
            {
                PieceType.None => PieceMovementCapability.None,
                PieceType.Pawn => PieceMovementCapability.PawnLike,
                PieceType.Knight => PieceMovementCapability.KnightLike,
                PieceType.Bishop => PieceMovementCapability.BishopLike,
                PieceType.Rook => PieceMovementCapability.RookLike,
                PieceType.Queen => PieceMovementCapability.QueenSlides,
                PieceType.King => PieceMovementCapability.KingStepLike,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
    }

    /// <summary>Validation rules for bitmask grants/sanitization.</summary>
    public static class PieceMovementCapabilityRules
    {
        /// <summary>Normalizes capability state for <paramref name="type"/> (e.g. royal always keeps <see cref="PieceMovementCapability.KingStepLike"/>).</summary>
        public static PieceMovementCapability SanitizeForPieceType(PieceType type, PieceMovementCapability caps)
        {
            if (type == PieceType.King)
                caps |= PieceMovementCapability.KingStepLike;
            return caps;
        }

        public static PieceMovementCapability FilterGrantMask(PieceType type, PieceMovementCapability requested) =>
            SanitizeForPieceType(type, requested);
    }
}
