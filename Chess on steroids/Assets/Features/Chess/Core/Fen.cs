using System;
using System.Collections.Generic;
using System.Text;

namespace Chess.Core
{
    public static class Fen
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static Board Parse(string fen)
        {
            var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) throw new ArgumentException("Invalid FEN", nameof(fen));

            var board = new Board();
            string[] ranks = parts[0].Split('/');
            if (ranks.Length != 8) throw new ArgumentException("Invalid FEN board", nameof(fen));

            for (int r = 0; r < 8; r++)
            {
                string rankStr = ranks[r];
                int chessRank = 7 - r;
                int file = 0;
                foreach (char ch in rankStr)
                {
                    if (char.IsDigit(ch))
                    {
                        file += ch - '0';
                        continue;
                    }
                    if (file > 7) throw new ArgumentException("Invalid FEN rank overflow", nameof(fen));
                    board.Squares[Square.Index(file, chessRank)] = Piece.FromChar(ch);
                    file++;
                }
                if (file != 8) throw new ArgumentException("Invalid FEN rank width", nameof(fen));
            }

            if (parts.Length >= 2)
                board.SideToMove = parts[1] == "b" ? Color.Black : Color.White;

            board.CastleWhiteKing = board.CastleWhiteQueen = board.CastleBlackKing = board.CastleBlackQueen = false;
            if (parts.Length >= 3 && parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    switch (c)
                    {
                        case 'K': board.CastleWhiteKing = true; break;
                        case 'Q': board.CastleWhiteQueen = true; break;
                        case 'k': board.CastleBlackKing = true; break;
                        case 'q': board.CastleBlackQueen = true; break;
                    }
                }
            }

            board.EnPassantTarget = Square.Invalid;
            if (parts.Length >= 4 && parts[3] != "-")
                board.EnPassantTarget = Square.FromAlgebraic(parts[3]);

            if (parts.Length >= 5 && int.TryParse(parts[4], out int h))
                board.HalfmoveClock = h;

            if (parts.Length >= 6 && int.TryParse(parts[5], out int f))
                board.FullmoveNumber = f;

            for (int i = 0; i < 64; i++)
            {
                var p = board.Squares[i];
                board.MovementCapabilitiesOnSquare[i] =
                    p.IsEmpty ? PieceMovementCapability.None : PieceMovementCapabilityDefaults.For(p.Type);
            }

            return board;
        }

        public static string ToFen(Board b)
        {
            var sb = new StringBuilder();
            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int f = 0; f < 8; f++)
                {
                    var p = b.Squares[Square.Index(f, r)];
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

            sb.Append(' ').Append(b.SideToMove == Color.White ? 'w' : 'b').Append(' ');

            bool anyCastle = b.CastleWhiteKing | b.CastleWhiteQueen | b.CastleBlackKing | b.CastleBlackQueen;
            if (!anyCastle) sb.Append('-');
            else
            {
                if (b.CastleWhiteKing) sb.Append('K');
                if (b.CastleWhiteQueen) sb.Append('Q');
                if (b.CastleBlackKing) sb.Append('k');
                if (b.CastleBlackQueen) sb.Append('q');
            }

            sb.Append(' ');
            if (b.EnPassantTarget == Square.Invalid) sb.Append('-');
            else sb.Append(Square.ToAlgebraic(b.EnPassantTarget));

            sb.Append(' ').Append(b.HalfmoveClock).Append(' ').Append(b.FullmoveNumber);
            return sb.ToString();
        }
    }

    public enum GameResult
    {
        InProgress,
        WhiteWinsCheckmate,
        BlackWinsCheckmate,
        Stalemate
    }

    public static class GameRules
    {
        public static GameResult Evaluate(Board board, IReadOnlyList<Move> legalMovesForSideToMove)
        {
            if (legalMovesForSideToMove.Count > 0) return GameResult.InProgress;
            if (board.IsInCheck(board.SideToMove))
                return board.SideToMove == Color.White ? GameResult.BlackWinsCheckmate : GameResult.WhiteWinsCheckmate;
            return GameResult.Stalemate;
        }
    }
}
