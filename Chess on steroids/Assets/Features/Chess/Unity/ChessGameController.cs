using System.Collections.Generic;
using Chess.Core;
using Chess.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chess.Unity
{
    public sealed class ChessGameController : MonoBehaviour
    {
        const string GimmickArmedFeedback = "Extra pawn move ready — move a pawn next.";
        const string GimmickIdleFeedback = "";

        [SerializeField] ChessBoardView boardView;
        [SerializeField] PiecePrefabLibrary piecePrefabs;
        [SerializeField] LegalMoveHighlight highlights;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] Camera gameCamera;
        [SerializeField] Transform pieceParent;
        [SerializeField] Button newGameButton;

        [Header("Promotion (hierarchy instance under Canvas)")]
        [SerializeField] GameObject promotionPanelRoot;
        [SerializeField] Button queenPromotionButton;
        [SerializeField] Button rookPromotionButton;
        [SerializeField] Button bishopPromotionButton;
        [SerializeField] Button knightPromotionButton;

        [Header("Capture feedback")]
        [SerializeField] Material captureTargetMaterial;

        [Header("Movement capabilities (AugmentPanel)")]
        [SerializeField] AugmentPanelView augmentPanel;

        [Header("Gimmicks (GimmicksPanel)")]
        [SerializeField] Button gainPawnGimmickButton;
        [SerializeField] TextMeshProUGUI gimmickArmedFeedbackText;

        Board _board;
        readonly Dictionary<int, PieceView> _bySquare = new();
        readonly List<Move> _fromToBuffer = new(8);
        readonly HashSet<int> _captureVictims = new();
        int? _selected;
        List<Move> _legal = new();
        GameResult _result = GameResult.InProgress;
        int? _pendingPromotionFrom;
        int? _pendingPromotionTo;
        bool _extraPawnGimmickArmed;
        bool _multiplayerGameOverDialogShown;

        void Awake()
        {
            _board = Board.StartingPosition();
            _legal = MoveGenerator.GenerateLegalMoves(_board);
        }

        void Start()
        {
            TryBindPromotionHierarchy();
            if (promotionPanelRoot != null)
                promotionPanelRoot.SetActive(false);

            if (pieceParent == null && boardView != null) pieceParent = boardView.transform;
            if (gameCamera == null) gameCamera = Camera.main;
            if (boardView != null)
                boardView.BuildDefaultVisualsAndCollider();

            WirePromotionButtons();
            TryBindAugmentPanel();
            TryBindGimmicksPanel();
            RebuildPieces();
            UpdateHighlights();
            RefreshStatus();

            if (newGameButton != null)
                newGameButton.onClick.AddListener(RestartGame);

            MultiplayerGameplayHud.EnsureBackToLobbyButton(this);
        }

        /// <summary>
        /// Fills missing promotion references from hierarchy: first Canvas → child "PromotionPanel",
        /// then buttons by name. Assign serialized fields in the Inspector to override.
        /// </summary>
        void TryBindPromotionHierarchy()
        {
            if (promotionPanelRoot == null)
            {
                var canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    var t = canvas.transform.Find("PromotionPanel");
                    if (t != null)
                        promotionPanelRoot = t.gameObject;
                }
            }

            if (promotionPanelRoot == null) return;

            Transform root = promotionPanelRoot.transform;
            if (queenPromotionButton == null)
                queenPromotionButton = root.Find("QueenPromotionButton")?.GetComponent<Button>();
            if (rookPromotionButton == null)
                rookPromotionButton = root.Find("RookPromotionButton")?.GetComponent<Button>();
            if (bishopPromotionButton == null)
                bishopPromotionButton = root.Find("BishopPromotionButton")?.GetComponent<Button>();
            if (knightPromotionButton == null)
                knightPromotionButton = root.Find("KnightPromotionButton")?.GetComponent<Button>();
        }

        void TryBindAugmentPanel()
        {
            if (augmentPanel == null)
            {
                augmentPanel = Object.FindAnyObjectByType<AugmentPanelView>(FindObjectsInactive.Include);
                if (augmentPanel == null)
                {
                    var canvas = Object.FindAnyObjectByType<Canvas>();
                    Transform tm = canvas != null ? FindDeep(canvas.transform, "AugmentPanel") : null;
                    var go = tm != null ? tm.gameObject : null;
                    if (go != null)
                        augmentPanel = go.GetComponent<AugmentPanelView>() ?? go.AddComponent<AugmentPanelView>();
                }
            }

            augmentPanel?.Initialize(this);
        }

        void TryBindGimmicksPanel()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            Transform gimmickRoot = canvas != null ? FindDeep(canvas.transform, "GimmicksPanel") : null;
            if (gimmickRoot == null && canvas != null)
                gimmickRoot = FindDeep(canvas.transform, "GainPawnMovementBTN")?.parent;

            if (gainPawnGimmickButton == null && gimmickRoot != null)
                gainPawnGimmickButton = gimmickRoot.Find("GainPawnMovementBTN")?.GetComponent<Button>();
            if (gimmickArmedFeedbackText == null && gimmickRoot != null)
            {
                Transform t = gimmickRoot.Find("Gimmickfeedback") ?? FindDeep(gimmickRoot, "Gimmickfeedback");
                gimmickArmedFeedbackText = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            }

            if (gainPawnGimmickButton != null)
                gainPawnGimmickButton.onClick.AddListener(OnGainPawnGimmickClicked);
            RefreshGimmickArmedFeedback();
            RefreshGimmickButtonState();
        }

        void OnGainPawnGimmickClicked()
        {
            if (!ComputeCanArmExtraPawnGimmick())
                return;
            _extraPawnGimmickArmed = true;
            RefreshGimmickArmedFeedback();
            RefreshGimmickButtonState();
        }

        static int CountPawns(Board board, Chess.Core.Color color)
        {
            int n = 0;
            for (int i = 0; i < 64; i++)
            {
                var p = board.Squares[i];
                if (!p.IsEmpty && p.Color == color && p.Type == PieceType.Pawn)
                    n++;
            }

            return n;
        }

        bool ComputeCanArmExtraPawnGimmick() =>
            _result == GameResult.InProgress &&
            !_pendingPromotionFrom.HasValue &&
            !_board.IsInCheck(_board.SideToMove) &&
            CountPawns(_board, _board.SideToMove) > 0;

        void RefreshGimmickButtonState()
        {
            if (gainPawnGimmickButton == null) return;
            gainPawnGimmickButton.interactable = ComputeCanArmExtraPawnGimmick();
        }

        void RefreshGimmickArmedFeedback()
        {
            if (gimmickArmedFeedbackText == null) return;
            gimmickArmedFeedbackText.text = _extraPawnGimmickArmed ? GimmickArmedFeedback : GimmickIdleFeedback;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform r = FindDeep(child, name);
                if (r != null)
                    return r;
            }

            return null;
        }

        /// <summary>Called from augment panel Add buttons (<see cref="AugmentPanelView"/>).</summary>
        public void GrantCapability(PieceMovementCapability mask)
        {
            if (_result != GameResult.InProgress || !_selected.HasValue) return;
            int sq = _selected.Value;
            if (_board.Squares[sq].IsEmpty) return;
            _board.AddCapability(sq, mask);
            RefreshAfterCapabilityEdit();
        }

        /// <summary>Called from augment panel Remove buttons (<see cref="AugmentPanelView"/>).</summary>
        public void RevokeCapability(PieceMovementCapability mask)
        {
            if (_result != GameResult.InProgress || !_selected.HasValue) return;
            _board.RemoveCapability(_selected.Value, mask);
            RefreshAfterCapabilityEdit();
        }

        void RefreshAfterCapabilityEdit()
        {
            _legal = MoveGenerator.GenerateLegalMoves(_board);
            _result = GameRules.Evaluate(_board, _legal);
            RebuildPieces();
            UpdateHighlights();
            RefreshStatus();
        }

        void RefreshAugmentPanel()
        {
            if (augmentPanel == null) return;
            if (_pendingPromotionFrom.HasValue || _result != GameResult.InProgress)
            {
                augmentPanel.HidePanel();
                return;
            }

            if (_selected is int sel && Square.IsValid(sel))
            {
                Piece p = _board.Squares[sel];
                if (!p.IsEmpty && p.Color == _board.SideToMove)
                {
                    augmentPanel.ShowSelection(p, _board.MovementCapabilitiesOnSquare[sel]);
                    return;
                }
            }

            augmentPanel.HidePanel();
        }

        void WirePromotionButtons()
        {
            if (queenPromotionButton != null)
                queenPromotionButton.onClick.AddListener(() => OnPromotionChosen(PieceType.Queen));
            if (rookPromotionButton != null)
                rookPromotionButton.onClick.AddListener(() => OnPromotionChosen(PieceType.Rook));
            if (bishopPromotionButton != null)
                bishopPromotionButton.onClick.AddListener(() => OnPromotionChosen(PieceType.Bishop));
            if (knightPromotionButton != null)
                knightPromotionButton.onClick.AddListener(() => OnPromotionChosen(PieceType.Knight));
        }

        void Update()
        {
            if (_result != GameResult.InProgress) return;
            if (_pendingPromotionFrom.HasValue) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (gameCamera == null || boardView == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            var ray = gameCamera.ScreenPointToRay(screenPos);
            if (!boardView.TryRaycastToSquare(ray, out int sq)) return;
            TryHandleClick(sq);
        }

        public void RestartGame()
        {
            ClearPromotionPending();
            _extraPawnGimmickArmed = false;
            _multiplayerGameOverDialogShown = false;
            _board = Board.StartingPosition();
            _selected = null;
            _legal = MoveGenerator.GenerateLegalMoves(_board);
            _result = GameResult.InProgress;
            RebuildPieces();
            UpdateHighlights();
            RefreshStatus();
        }

        void ClearPromotionPending()
        {
            _pendingPromotionFrom = null;
            _pendingPromotionTo = null;
            if (promotionPanelRoot != null)
                promotionPanelRoot.SetActive(false);
        }

        void TryHandleClick(int sq)
        {
            if (_pendingPromotionFrom.HasValue) return;

            Piece clicked = _board.Squares[sq];

            if (_selected == null)
            {
                if (clicked.IsEmpty || clicked.Color != _board.SideToMove) return;
                _selected = sq;
                UpdateHighlights();
                return;
            }

            int from = _selected.Value;
            CollectMovesFromTo(from, sq, _fromToBuffer);
            if (_fromToBuffer.Count == 0)
            {
                if (!clicked.IsEmpty && clicked.Color == _board.SideToMove)
                {
                    _selected = sq;
                    UpdateHighlights();
                }
                else
                {
                    _selected = null;
                    UpdateHighlights();
                }
                return;
            }

            if (_fromToBuffer.Count == 1)
            {
                ApplyMoveComplete(_fromToBuffer[0]);
                return;
            }

            if (promotionPanelRoot == null)
            {
                foreach (var m in _fromToBuffer)
                {
                    if (m.Promotion == PieceType.Queen)
                    {
                        ApplyMoveComplete(m);
                        return;
                    }
                }

                ApplyMoveComplete(_fromToBuffer[0]);
                return;
            }

            _pendingPromotionFrom = from;
            _pendingPromotionTo = sq;
            promotionPanelRoot.SetActive(true);
            RefreshStatus();
        }

        void OnPromotionChosen(PieceType promotion)
        {
            if (!_pendingPromotionFrom.HasValue || !_pendingPromotionTo.HasValue) return;

            int from = _pendingPromotionFrom.Value;
            int to = _pendingPromotionTo.Value;
            foreach (var m in _legal)
            {
                if (m.From == from && m.To == to && m.Promotion == promotion)
                {
                    ClearPromotionPending();
                    _selected = null;
                    ApplyMoveComplete(m);
                    return;
                }
            }
        }

        void CollectMovesFromTo(int from, int to, List<Move> into)
        {
            into.Clear();
            foreach (var m in _legal)
            {
                if (m.From == from && m.To == to)
                    into.Add(m);
            }
        }

        void ApplyMoveComplete(Move move)
        {
            Piece mover = _board.Squares[move.From];
            bool wasArmed = _extraPawnGimmickArmed;
            bool pawnGimmickHalfPly = wasArmed && mover.Type == PieceType.Pawn;

            if (pawnGimmickHalfPly)
                _board.ApplyMove(move, switchTurn: false);
            else
                _board.ApplyMove(move);

            if (wasArmed)
                _extraPawnGimmickArmed = false;

            _selected = null;
            RebuildPieces();
            _legal = MoveGenerator.GenerateLegalMoves(_board);
            _result = GameRules.Evaluate(_board, _legal);
            UpdateHighlights();
            RefreshStatus();
        }

        void RebuildPieces()
        {
            foreach (var kv in _bySquare)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }

            _bySquare.Clear();
            if (boardView == null) return;

            for (int i = 0; i < 64; i++)
            {
                Piece p = _board.Squares[i];
                if (p.IsEmpty) continue;

                GameObject prefab = piecePrefabs != null ? piecePrefabs.GetPrefab(p) : null;
                GameObject go;
                if (prefab != null)
                {
                    go = Instantiate(prefab, pieceParent);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(pieceParent, false);
                    go.transform.localScale = new Vector3(0.35f, 0.4f, 0.35f);
                }

                var pv = go.GetComponent<PieceView>();
                if (pv == null) pv = go.AddComponent<PieceView>();
                    pv.Init(p.Type, p.Color, _board.MovementCapabilitiesOnSquare[i]);
                    go.transform.position = boardView.GetWorldPositionForPiece(i);
                _bySquare[i] = pv;
            }
        }

        void UpdateHighlights()
        {
            if (highlights != null)
            {
                if (_result != GameResult.InProgress)
                    highlights.Refresh(null, null);
                else
                    highlights.Refresh(_selected, _legal);
            }

            RefreshCaptureTargetMaterials();
            RefreshAugmentPanel();
        }

        void RefreshCaptureTargetMaterials()
        {
            foreach (var pv in _bySquare.Values)
            {
                if (pv != null)
                    pv.SetCaptureTargetHighlight(captureTargetMaterial, false);
            }

            if (captureTargetMaterial == null || _result != GameResult.InProgress || !_selected.HasValue)
                return;

            CaptureVictimSquares.AddVictims(_board, _selected.Value, _legal, _captureVictims);

            foreach (int sq in _captureVictims)
            {
                if (_bySquare.TryGetValue(sq, out PieceView pv) && pv != null)
                    pv.SetCaptureTargetHighlight(captureTargetMaterial, true);
            }
        }

        void RefreshStatus()
        {
            if (statusText != null)
            {
                switch (_result)
                {
                    case GameResult.InProgress:
                        bool inCheck = _board.IsInCheck(_board.SideToMove);
                        statusText.text = (_board.SideToMove == Chess.Core.Color.White ? "White" : "Black") + " to move"
                                          + (inCheck ? " — CHECK" : "");
                        break;
                    case GameResult.WhiteWinsCheckmate:
                        statusText.text = "White wins — checkmate";
                        break;
                    case GameResult.BlackWinsCheckmate:
                        statusText.text = "Black wins — checkmate";
                        break;
                    case GameResult.Stalemate:
                        statusText.text = "Stalemate — draw";
                        break;
                }
            }

            RefreshGimmickButtonState();
            RefreshGimmickArmedFeedback();
            TryPromptMultiplayerGameOver();
        }

        void TryPromptMultiplayerGameOver()
        {
            if (_multiplayerGameOverDialogShown)
                return;
            if (!FusionDdolHub.IsRunnerActive)
                return;
            if (_result != GameResult.WhiteWinsCheckmate && _result != GameResult.BlackWinsCheckmate &&
                _result != GameResult.Stalemate)
                return;

            _multiplayerGameOverDialogShown = true;
            SimpleConfirmDialog.Show(
                "Game over",
                "Return to the lobby?",
                onConfirm: FusionDdolHub.ShutdownAfterUserConfirmed,
                onCancel: () => { },
                showCancel: true);
        }
    }
}
