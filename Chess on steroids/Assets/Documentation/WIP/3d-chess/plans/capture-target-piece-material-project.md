---
date: 2026-05-12T12:00:00-03:00
author: Composer
topic: "Capture target — piece material feedback when selected"
tags: [plan, implementation, Chess.Unity, PieceView, UX]
status: draft
last_updated: 2026-05-12
last_updated_by: Composer
revision: one shared capture material for all victims; may reuse legal quad material asset in Inspector
implemented: 2026-05-12 — PieceView + ChessGameController + CaptureVictimSquares + tests
---

# Capture target — piece material feedback when selected

## Overview

When the player **selects a friendly piece**, any **enemy piece that can be captured** on the next click (by at least one legal move from that selection) should **change material** so the capture target is obvious. Quiet-move destinations stay as they are today (legal-move **quad** highlights). This plan adds **per-piece** material override on those victims and clears it when selection changes, the move is played, or the game state resets.

## Current state analysis

- **Selection and legal moves** live in `ChessGameController`: `_selected`, `_bySquare` maps square → `PieceView`, `UpdateHighlights()` calls `LegalMoveHighlight.Refresh(_selected, _legal)` (`ChessGameController.cs` roughly lines 270–280).
- **`LegalMoveHighlight`** draws **quads** on the selected square and on every legal **destination** with the same `legalMaterial` for all destinations (`LegalMoveHighlight.cs` lines 51–85). It does **not** distinguish captures from non-captures and does **not** see piece renderers.
- **`PieceView`** only stores `pieceType` / `pieceSide` and `Init`; there is **no** renderer or material handle (`PieceView.cs` lines 6–18).
- **Captures in rules**: a legal move captures if (a) the destination square holds an **enemy** piece (including promotion captures), or (b) the move is **en passant** — then `Move.Flags` includes `EnPassant` and the **victim is on the square behind the ep target** (same file as moving pawn, rank of pawn), not on `Move.To` (see `MoveGenerator.AddPawnMoves` ep logic in `MoveGenerator.cs`). Any implementation must treat **ep** explicitly or the wrong square would be highlighted.

## Desired end state

- With **no selection**, no piece uses a “capture target” material override.
- With **selection** on square `from`, for every legal move `m` with `m.From == from` that is a **capture** (occupied enemy `m.To`, or **en passant** with victim on the computed victim square), the **`PieceView` on that victim square** uses a **highlight material** (configurable in the Editor).
- **Non-capture** legal moves keep **only** the existing floor markers from `LegalMoveHighlight` (no change required there for quiet moves).
- After **deselect**, **move applied**, **new game**, or **game over** (highlights cleared), **all** piece material overrides are cleared.

### Confirmed decisions

- **One material only**: a single serialized **`Material`** on **`ChessGameController`** (`captureTargetMaterial`); no per-side or per-piece-type highlight assets.
- **Same material for everyone**: that asset is applied to **all** capture targets (white and black victims use **identical** highlight material).
- **Optional**: you can assign the **same** `.mat` file as **`LegalMoveHighlight.legalMaterial`** if it reads well on both quads and meshes; otherwise use a dedicated URP/Lit variant for pieces.

### Automated verification

- **Compilation**: Unity compiles all referenced scripts.
- **Optional (recommended)**: Extract a small pure helper, e.g. `Chess.Unity.CaptureHighlightUtil` or static method on controller, `static void AddCaptureVictimSquares(Board board, int from, IReadOnlyList<Move> legal, HashSet<int> dest)` (or return `HashSet<int>`), and add **Edit Mode** tests in `Assets/Features/Chess/Tests/Editor/` that assert victim indices for:
  - a simple **NxB** style capture,
  - one **en passant** FEN (victim square is not `Move.To`),
  - a position with **no** selection / no captures → empty set.

### Manual verification

- Select a piece that can capture: **enemy piece(s)** show the capture material; **empty** legal-move squares still show only quad highlights.
- Select a piece with **only** quiet moves: **no** enemy material changes.
- Trigger **en passant** legal move list: the **captured pawn** (on its own square) shows the material even if the **ep landing** square is empty.
- Change selection to another piece or deselect: previous targets **revert**.
- Play the capture: pieces **rebuild** after `RebuildPieces` — no stale highlight.

### Key discoveries

- Victim square for **en passant** ≠ `Move.To` (`MoveGenerator` en passant branch).
- **Piece visuals** may use **multiple** `Renderer`s on children (typical for prefab packs); `PieceView` on root should drive **all** relevant renderers under that instance.

## What we're NOT doing

- Replacing **quad** highlights with piece materials for **quiet** moves (out of scope unless you extend later).
- **Global** prefab / asset changes: we assume **runtime** material override on **instances** only (or duplicated materials) so library prefabs stay untouched.
- **Animation**, **outline post-processing**, or **URP Renderer Features** — only a **swappable Material** (or shared material asset) as configured.
- **AI / network** — local human UX only.

## Implementation approach

1. **Compute capture victim squares** from `Board` + `from` + filtered legal moves (`m.From == from`). For each move:
   - If `(m.Flags & MoveFlag.EnPassant) != 0` → victim square = `Square.Index(Square.File(m.To), Square.Rank(from))` (same formula as core generator).
   - Else if `!board.Squares[m.To].IsEmpty && board.Squares[m.To].Color != board.SideToMove` → victim square = `m.To`.
   - Else → not a capture for material purposes (quiet move or ill-formed; should not happen for ep empty target).
2. **`PieceView`**: cache child `Renderer`s; on enable/init store baseline `sharedMaterial` (or `material` if you require instancing — prefer **`sharedMaterial`** + optional **MaterialPropertyBlock** if you only need color; simplest first path is **material swap** with a **dedicated highlight Material** asset to avoid MPB complexity). Expose `SetCaptureHighlighted(bool)` or `SetCaptureHighlight(Material highlight, bool on)` with internal restore.
3. **`ChessGameController`**: after `RebuildPieces` or in `UpdateHighlights`, call `RefreshCaptureTargetMaterials()`: turn off highlight on **all** `PieceView`s in `_bySquare`, then for each victim index in the set, if `_bySquare.TryGetValue(idx, out var pv)`, enable highlight. **Order**: ensure `RefreshCaptureTargetMaterials` runs when `_selected` is cleared (same path as `UpdateHighlights`).
4. Serialize **one** `Material` on `ChessGameController` (`captureTargetMaterial`). Use that **same asset** for every victim square’s `PieceView` (see **Confirmed decisions**).

---

## Phase 1: `PieceView` — renderer cache and highlight toggle

### Overview

Allow each piece instance to switch renderers to a highlight material and restore defaults.

### File

`Assets/Features/Chess/Unity/PieceView.cs`

### Changes

- In `Awake` or first use, **`GetComponentsInChildren<Renderer>(includeInactive: true)`** and cache arrays of **baseline** `sharedMaterial` per renderer (or per material slot).
- Add `[SerializeField] bool useCaptureHighlightWithExternalMaterial` is unnecessary if controller passes material — prefer **`public void SetCaptureTargetHighlight(Material highlightMaterial, bool enabled)`**:
  - If `enabled && highlightMaterial != null`, assign to each renderer’s **`sharedMaterial`** (or match slot count if multi-material meshes).
  - If `!enabled`, restore cached baseline materials.
- **`Init`** may run after `Awake`: refresh baseline cache when `Init` is called if the object is recycled (today pieces are destroyed on rebuild — so `Init` after instantiate is enough; rebuild destroys old views, so baseline can be snapshot in `Init` or `Start` after hierarchy is stable).

**Implementation note**: If a prefab uses **multiple materials** per renderer, snapshot **`sharedMaterials`** arrays and restore the full arrays. If only **slot 0** matters for chess meshes, document that constraint or implement full array copy.

**Implementation note**: After this phase compiles, pause for manual check on **one** prefab piece in isolation (material toggles in Inspector via test hook or temporary context menu) before wiring controller.

---

## Phase 2: `ChessGameController` — victim square set + refresh

### Overview

Drive `PieceView` highlight from current selection and legal moves.

### File

`Assets/Features/Chess/Unity/ChessGameController.cs`

### Changes

- Add **`[SerializeField] Material captureTargetMaterial`** (nullable: if null, skip). **Do not** add a second material for black vs white — one reference for all victims.
- Add `readonly HashSet<int> _captureVictims = new()` and a method **`void RefreshCaptureTargetMaterials()`**:
  - For each `PieceView` in `_bySquare.Values`, call `SetCaptureTargetHighlight(..., false)`.
  - If `_selected` is null, `_result != InProgress`, or `captureTargetMaterial == null`, stop after clear.
  - Compute victim squares (see **Implementation approach** above) into `_captureVictims`.
  - For each index in `_captureVictims`, if `_bySquare.TryGetValue(i, out var pv)`, apply highlight with `true`.
- Call **`RefreshCaptureTargetMaterials()`** at the end of **`UpdateHighlights()`**, and ensure **`RebuildPieces()`** invokes **`UpdateHighlights()`** already after repopulating `_bySquare` (today `ApplyMoveComplete` and `RestartGame` call `UpdateHighlights` — verify **`RebuildPieces` alone** paths if any don’t refresh materials).
- **`RestartGame` / `ApplyMoveComplete`**: selection cleared → `RefreshCaptureTargetMaterials` via `UpdateHighlights` must clear all.

**Implementation note**: Consider extracting victim computation into a **static** helper for testability (same file or `CaptureHighlightUtil.cs`).

---

## Phase 3: Optional tests + polish

### Overview

Lock victim square logic, especially **en passant**.

### File(s)

- New: `Assets/Features/Chess/Tests/Editor/CaptureVictimSquaresTests.cs` (or similar), referencing a **public static** helper that takes `Board`, `from`, and legal move list.
- Use existing **`Fen.Parse`** and **`MoveGenerator.GenerateLegalMoves`** from tests (`MoveGeneratorTests.cs` pattern).

### Changes

- One test: position with **explicit capture** on `To` — assert victim set contains defender’s square.
- One test: **en passant** FEN — assert victim set contains **pawn** square, not only `To`.

**Implementation note**: Manual pass: full game scene with low-poly pieces confirms all submeshes behave.

---

## Research

- `Assets/Features/Chess/Unity/ChessGameController.cs` — selection, `_bySquare`, `UpdateHighlights`
- `Assets/Features/Chess/Unity/PieceView.cs` — piece instance
- `Assets/Features/Chess/Unity/LegalMoveHighlight.cs` — quad destinations (parallel feedback)
- `Assets/Features/Chess/Core/Move.cs` — `MoveFlag.EnPassant`
- `Assets/Features/Chess/Core/MoveGenerator.cs` — victim square for en passant (mirror logic)
- `Assets/Features/Chess/Core/CaptureVictimSquares.cs` — victim set helper (used by controller + tests)

## Implementation status

- [x] **Phase 1** — `PieceView`: child `Renderer` baseline cache, `SetCaptureTargetHighlight`.
- [x] **Phase 2** — `ChessGameController`: `captureTargetMaterial`, `RefreshCaptureTargetMaterials`, `UpdateHighlights` always refreshes piece materials (even if `highlights` is null).
- [x] **Phase 3** — `CaptureVictimSquaresTests` + `CaptureVictimSquares.cs` in Core.
- [ ] **Manual** — Assign `captureTargetMaterial` on `ChessGameController` in the scene; verify captures and en passant victim glow; clear on deselect / end of game.
