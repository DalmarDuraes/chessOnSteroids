using System.Collections.Generic;
using Chess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chess.Unity
{
    public sealed class ChessGameController : MonoBehaviour
    {
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

        Board _board;
        readonly Dictionary<int, PieceView> _bySquare = new();
        readonly List<Move> _fromToBuffer = new(8);
        readonly HashSet<int> _captureVictims = new();
        int? _selected;
        List<Move> _legal = new();
        GameResult _result = GameResult.InProgress;
        int? _pendingPromotionFrom;
        int? _pendingPromotionTo;

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
            RebuildPieces();
            UpdateHighlights();
            RefreshStatus();

            if (newGameButton != null)
                newGameButton.onClick.AddListener(RestartGame);
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
            _board.ApplyMove(move);
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
            if (statusText == null) return;

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
    }
}
