using Chess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Chess.Unity
{
    /// <summary>
    /// Attached to augment panel root (<c>AugmentPanel</c> prefab instance). Wires AddMovement_* / RemoveMovement_* child buttons by name.
    /// </summary>
    public sealed class AugmentPanelView : MonoBehaviour
    {
        ChessGameController _host;
        bool _listenersWired;

        public void Initialize(ChessGameController host)
        {
            _host = host;
            gameObject.SetActive(false);

            if (!_listenersWired)
            {
                foreach (Button b in GetComponentsInChildren<Button>(true))
                {
                    if (!TryParseButtonName(b.gameObject.name, out bool isAdd, out PieceMovementCapability cap))
                        continue;

                    PieceMovementCapability mask = cap;
                    bool add = isAdd;
                    b.onClick.AddListener(() =>
                    {
                        if (add) _host?.GrantCapability(mask);
                        else _host?.RevokeCapability(mask);
                    });
                }

                _listenersWired = true;
            }
        }

        public void ShowSelection(Piece piece, PieceMovementCapability caps)
        {
            gameObject.SetActive(true);

            foreach (Button b in GetComponentsInChildren<Button>(true))
            {
                if (!TryParseButtonName(b.gameObject.name, out bool isAdd, out PieceMovementCapability cap))
                    continue;

                ApplyInteractable(b, cap, piece, caps, isAdd);
            }
        }

        public void HidePanel() => gameObject.SetActive(false);

        static void ApplyInteractable(Button b, PieceMovementCapability mask,
            Piece piece, PieceMovementCapability caps, bool isAdd)
        {
            bool isRemove = !isAdd;

            if (mask == PieceMovementCapability.KingStepLike && isRemove && piece.Type == PieceType.King)
            {
                b.interactable = false;
                return;
            }

            if (mask == PieceMovementCapability.QueenSlides)
            {
                bool hasQueen = (caps & PieceMovementCapability.BishopLike) != 0 &&
                               (caps & PieceMovementCapability.RookLike) != 0;
                b.interactable = isRemove ? hasQueen : !hasQueen;
                return;
            }

            if (isRemove)
                b.interactable = (caps & mask) != 0;
            else
                b.interactable = (caps & mask) == 0;
        }

        static bool TryParseButtonName(string name, out bool isAdd, out PieceMovementCapability cap)
        {
            isAdd = false;
            cap = PieceMovementCapability.None;
            const string addP = "AddMovement_";
            const string remP = "RemoveMovement_";
            string key;
            if (name.StartsWith(addP))
            {
                isAdd = true;
                key = name.Substring(addP.Length);
            }
            else if (name.StartsWith(remP))
            {
                isAdd = false;
                key = name.Substring(remP.Length);
            }
            else return false;

            return MapKey(key, out cap);
        }

        static bool MapKey(string key, out PieceMovementCapability cap)
        {
            switch (key.ToLowerInvariant())
            {
                case "pawn": cap = PieceMovementCapability.PawnLike; return true;
                case "knight": cap = PieceMovementCapability.KnightLike; return true;
                case "bishop": cap = PieceMovementCapability.BishopLike; return true;
                case "rook": cap = PieceMovementCapability.RookLike; return true;
                case "queen": cap = PieceMovementCapability.QueenSlides; return true;
                case "king": cap = PieceMovementCapability.KingStepLike; return true;
                default:
                    cap = PieceMovementCapability.None;
                    return false;
            }
        }
    }
}
