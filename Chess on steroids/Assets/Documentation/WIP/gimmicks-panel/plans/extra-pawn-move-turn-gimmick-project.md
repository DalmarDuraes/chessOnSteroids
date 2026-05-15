---
date: "2026-05-15T12:00:00-03:00"
author: Composer
topic: "Extra pawn move without ending turn (Gimmicks panel)"
tags: [plan, implementation, chess, unity, gimmicks, turn-order, Board.ApplyMove, PieceType.Pawn]
status: draft
last_updated: 2026-05-15
last_updated_by: Composer
---

# Extra pawn move without ending turn — implementation plan

## Overview

Add a **gimmick** activated from `Assets/Features/GimmicksPanel/GimmicksPanel.prefab` (`GainPawnMovementBTN`, label *“Gain a pawn movement”*), with **armed-state feedback** on sibling **TextMeshProUGUI** `Gimmickfeedback`: the active player **arms** the effect, then their **next move** moves a **PAWN piece** (`PieceType.Pawn` on the departure square)—**regardless of how that pawn moves** (normal push, knight capability, king-step augment, sliders, etc.)—and that ply **does not switch** `SideToMove`. They then complete their turn with a **second** legal move as usual (any piece), after which `SideToMove` flips. Core change is **`Board.ApplyMove`** supporting a half-turn (no flip, and correct **fullmove** semantics for Black).

## Product rules (locked)

| Rule | Decision |
|------|-----------|
| Next move after arming is not a pawn piece | Move applies **normally**; activation is **used up** (no saved token). |
| Activations per game | **Unlimited** (player may press the button again whenever it is available). |
| While side to move is **in check** | Button **not clickable** (`interactable = false`). |
| **What counts as “pawn gimmick” move** | **Piece type on `move.From`** is `PieceType.Pawn` — **not** `PawnLike` capability. A pawn with knight/king/slider augments still qualifies; moving a knight that only has `PawnLike` does **not** qualify. |
| No pawns left for side to move | Button **not clickable** (cannot arm). |
| Armed feedback | `Gimmickfeedback` (**TextMeshProUGUI**) on `GimmicksPanel.prefab` shows clear **armed vs idle** copy (implementation-owned strings). |

## Current State Analysis

- **Turn switch** happens only in `Board.FinishMove` via `SideToMove = SideToMove.Opponent()` (`Board.cs` ~338).
- **`FullmoveNumber`** increments when **`us == Color.Black`** completes a normal ply (`Board.cs` ~335–336). If Black makes a “free” pawn ply that must **not** end the turn, that increment **must not** run yet — only Black’s **finishing** ply of that double sequence should increment.
- **`ChessGameController.ApplyMoveComplete`** (`ChessGameController.cs` ~311–319) always applies a full ply: `ApplyMove` → regenerate legals → evaluate. No gimmick or panel wiring exists for `GimmicksPanel`.
- **Prefab** (`GimmicksPanel.prefab`): root `GimmicksPanel`; child **`Gimmickfeedback`** is a **TextMeshProUGUI** (sibling of `GainPawnMovementBTN`) for armed-state messaging; inspector placeholder text is `New Text` until wired. Child button `GainPawnMovementBTN` has **empty** `m_OnClick` PersistentCalls (wire in code). No gimmick `MonoBehaviour` on the panel — wiring matches **serialized refs or `FindDeep`** (like `AugmentPanel`).
- **Legal move generation** is unchanged: both the gimmick ply and the follow-up move must each be **individually legal** (existing `MoveGenerator.GenerateLegalMoves`). Unusual pawn motion comes from **capabilities on the pawn square**, not from reclassifying the piece type.

## Desired End State

1. **Gain a pawn movement** is clickable only when: game **InProgress**, **not** in promotion UI, side to move is **not** in check, and that side has **at least one** `PieceType.Pawn` on the board.
2. On click → gimmick **armed** (unlimited re-use over the game subject to the same gates); **`Gimmickfeedback`** text switches to an **armed** message (copy TBD — e.g. *“Extra pawn move ready — move a pawn next.”*). When **not armed**, text returns to an **idle** message (**empty string** or neutral hint such as *“Gain a pawn movement to keep your turn after moving a pawn.”* — align with tone/space in layout).
3. Player’s **next applied move**:
   - If **armed** and the piece on **`move.From`** is **`PieceType.Pawn`**: `ApplyMove(..., switchTurn: false)`; **disarm** (activation consumed).
   - If **armed** and the piece is **not** a pawn: normal `ApplyMove`; **disarm** (activation **used up** with no extra half-ply).
4. Second move ends the turn as today.
5. **Restart** clears armed state and refreshes **button** + **`Gimmickfeedback`** label.
6. **Automated tests** cover `ApplyMove` with “no flip” for White and Black fullmove behavior.

### Key Discoveries

- Gimmick detection must use **`Squares[move.From].Type == PieceType.Pawn`**, not `(caps & PawnLike)` — aligns with “correlated to the **PAWN piece** and not the movement as a pawn.”
- `FinishMove` couples **en passant / halfmove clock** with **side flip / fullmove** (`Board.cs` ~323–338). The gimmick needs the same `FinishMove` bookkeeping for the qualifying ply, but **`switchTurn: false`**.
- Button **interactable** must be recomputed whenever position or side-to-move or check status changes (`RefreshStatus`, `ApplyMoveComplete`, `RestartGame`, `Start`, and after arming is optional—arming doesn’t change check/pawn count).
- **`Gimmickfeedback`** must update whenever **armed** toggles (`OnGainPawnGimmickClicked`, `ApplyMoveComplete`, `RestartGame`) so the label never stays “armed” across restart or after the next move consumes activation.

## What We're NOT Doing

- **More than one** extra half-ply **per button press** (each activation still applies only to the **next** move).
- Changing **FEN export/import** for gimmick or armed state (runtime-only unless you add persistence later).
- New `MoveFlag` values (turn behavior via `ApplyMove` parameter).
- **AI / network** sync (local controller only).

## Implementation Approach

1. **Core**: `Board.ApplyMove(Move m, bool switchTurn = true)` and `FinishMove(..., switchTurn)` as before.
2. **Unity**: `bool _extraPawnGimmickArmed` in `ChessGameController`.
3. **Qualifying ply**: `_board.Squares[move.From].Type == PieceType.Pawn` (covers promoted pieces only after promotion — they are no longer pawns).
4. **Button refresh** (new helper e.g. `RefreshGimmickButtonState()`):
   - `bool canArm = _result == InProgress && !_pendingPromotionFrom.HasValue && !_board.IsInCheck(_board.SideToMove) && CountPawns(_board, _board.SideToMove) > 0`
   - `gainPawnGimmickButton.interactable = canArm` (null-safe).
   - Implement `CountPawns(Board b, Color c)` as a private static / local loop over `Squares` for `Type == PieceType.Pawn && Color == c`.
5. Call `RefreshGimmickButtonState()` from `Start`, `RestartGame`, `ApplyMoveComplete`, `RefreshStatus` (and `RefreshAfterCapabilityEdit` if capabilities can add/remove “logical” pawn presence — only piece **type** matters, so pawn count changes only on captures/promotions; capability edits don’t change count — optional to skip there for fewer calls).
6. **Armed label**: `[SerializeField] TextMeshProUGUI gimmickArmedFeedbackText` (optional); if null, `FindDeep(gimmicksPanelOrCanvas, "Gimmickfeedback")`. Add `RefreshGimmickArmedFeedback()` sets `text` from `_extraPawnGimmickArmed` (+ optional `ForceMeshUpdate()` / `Canvas.ForceUpdateCanvases()` if layout clips). Invoke alongside every arm/disarm, `Start` (after binding), and `RestartGame`.

## Phase 1: Board — conditional turn end

- [x] Complete

### Overview

Make turn switching and fullmove advancement explicit so a qualifying ply can update the position without ending the turn.

### Changes Required

#### 1. `Board.cs`

**File**: `Assets/Features/Chess/Core/Board.cs`

**Changes**:

- Change `FinishMove` to accept `bool switchTurn`.
- When `switchTurn == false`: keep en passant + halfmove clock logic identical; **skip** `FullmoveNumber` increment and **skip** `SideToMove` flip.
- `public void ApplyMove(Move move, bool switchTurn = true)` threading `switchTurn` into every `FinishMove` call path.

```csharp
// Conceptual sketch — match local style/naming
public void ApplyMove(Move move, bool switchTurn = true)
{
    // ... existing body; every FinishMove(..., captureOrEp) → FinishMove(..., captureOrEp, switchTurn)
}

void FinishMove(Color us, Piece piece, Move m, bool captureOrEp, bool switchTurn)
{
    // en passant target + halfmove: unchanged

    if (!switchTurn)
        return;

    if (us == Color.Black)
        FullmoveNumber++;

    SideToMove = SideToMove.Opponent();
}
```

**Implementation note**: Audit **all** `FinishMove(` call sites inside `ApplyMove` (castle branches + normal tail).

---

**Pause for manual confirmation**: White `e2-e4` with `switchTurn: false` leaves `SideToMove == White` and `FullmoveNumber` unchanged; Black equivalent leaves `SideToMove == Black` and `FullmoveNumber` unchanged.

---

## Phase 2: Tests — ApplyMove switchTurn

- [x] Complete (automated verification only)

### Overview

Regression-proof fullmove / side-to-move behavior for both colors.

### Changes Required

#### 1. New or extended editor test file

**File**: `Assets/Features/Chess/Tests/Editor/` (e.g. `DoublePawnGimmickBoardTests.cs`)

**Cases** (examples):

1. From start: apply `e2→e4` with `switchTurn: false` → `SideToMove` still White; `FullmoveNumber` still 1.
2. Synthetic Black to move: pawn move with `switchTurn: false` → still Black; `FullmoveNumber` unchanged.
3. Same position: pawn move with `switchTurn: true` → White to move; `FullmoveNumber` incremented once when Black completes.

**Optional** (controller integration or future): position with pawn that has non-`PawnLike` moves only — still use `PieceType.Pawn` on `From` for gimmick path (manual or test via controller if exposed).

**Automated verification**: Unity Test Runner EditMode.

---

## Phase 3: Controller + Gimmicks panel wiring

- [x] Complete (manual playtesting still per success criteria below)

### Overview

Arm gimmick on button press; consume on next move; drive **interactable** from check + pawn count; qualify gimmick ply by **pawn piece type**, not movement family.

### Changes Required

#### 1. `ChessGameController.cs`

**File**: `Assets/Features/Chess/Unity/ChessGameController.cs`

**Changes**:

- Field `bool _extraPawnGimmickArmed`.
- `[SerializeField] TextMeshProUGUI gimmickArmedFeedbackText` (optional); if null, `FindDeep(..., "Gimmickfeedback")` under the same Canvas / panel used for gimmicks (`using TMPro;` already in controller).
- `[SerializeField] Button gainPawnGimmickButton` (optional); `TryBindGimmicksPanel` finds `GainPawnMovementBTN` if null; wire `onClick` → `OnGainPawnGimmickClicked`.
- `OnGainPawnGimmickClicked`: only runs if button was interactable (`RefreshGimmickButtonState` already false when illegal); defensively guard same conditions; set `_extraPawnGimmickArmed = true`; call `RefreshGimmickArmedFeedback()`.
- `RestartGame`: `_extraPawnGimmickArmed = false`; `RefreshGimmickButtonState()`; `RefreshGimmickArmedFeedback()`.
- `ApplyMoveComplete`:
  - `Piece mover = _board.Squares[move.From]` before apply.
  - If `_extraPawnGimmickArmed && mover.Type == PieceType.Pawn` → `ApplyMove(move, switchTurn: false)`.
  - Else → `ApplyMove(move)`.
  - If `_extraPawnGimmickArmed` → set `false` (both branches **consume** activation).
- After refresh chain: **`RefreshGimmickButtonState()`** whenever `RefreshStatus()` runs post-move/restart.
- After **`ApplyMoveComplete`** updates `_extraPawnGimmickArmed`, call **`RefreshGimmickArmedFeedback()`** so the TMP reflects disarm.

**Promotion**: Immediately before apply, `Squares[from]` is still **Pawn** — gimmick ply still uses `switchTurn: false`; second move uses new type.

#### 2. Prefab (reference)

**File**: `Assets/Features/GimmicksPanel/GimmicksPanel.prefab`

- **`Gimmickfeedback`**: bound in Inspector as `TextMeshProUGUI` or resolved by name under `GimmicksPanel`.

**Note**: `m_OnClick` on `GainPawnMovementBTN` can stay empty when wired from code.

**Note**: Root panel `Image` may stay disabled unless you want a visible panel backdrop.

**Pause for manual confirmation**

- **TMP `Gimmickfeedback`**: armed copy visible after click; idle (or blank) after the next ply consumes activation or **New Game**; prefab placeholder **New Text** replaced when final strings ship.
- In check → button grayed out / no click.
- Position with zero pawns for side → button inactive.
- Pawn with extra capabilities (if you have augment panel): move uses gimmick path (no turn end) when armed.
- Arm → move knight (non-pawn) → turn ends; cannot “save” activation.
- Arm unlimited times across a long game when conditions allow.

---

## Research

- UI reference: `Assets/Features/GimmicksPanel/GimmicksPanel.prefab` (`GainPawnMovementBTN`, **`Gimmickfeedback`** TMP)
- Turn end: `Assets/Features/Chess/Core/Board.cs` (`FinishMove`, `ApplyMove`)
- Controller flow: `Assets/Features/Chess/Unity/ChessGameController.cs` (`ApplyMoveComplete`, `RefreshStatus`)
- Prior related plans: `Assets/Documentation/WIP/piece-movement-gimmicks/plans/composable-piece-movements-project.md`

## Success Criteria

### Automated verification

- EditMode tests pass for `ApplyMove(..., switchTurn: false)` **White** and **Black** `FullmoveNumber` / `SideToMove` behavior.
- Project compiles with no new warnings in touched files.

### Manual verification

- Button **off** when in check; **off** when side has no pawns; **on** otherwise (InProgress, no promotion pending).
- Arm → move **pawn** (any legal move that piece can make) → same side to move; second move → opponent.
- Arm → move **non-pawn** → turn ends; activation lost.
- Unlimited re-arming when button is available.
- New game clears armed state and updates button + **`Gimmickfeedback`** text.
