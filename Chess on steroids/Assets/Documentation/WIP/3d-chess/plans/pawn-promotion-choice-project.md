---
date: 2026-05-12T12:00:00-03:00
author: Composer
topic: "Pawn promotion — popup UI to choose piece"
tags: [plan, implementation, promotion, UI, Chess.Core, Chess.Unity, TMP]
status: draft
last_updated: 2026-05-12
last_updated_by: Composer
revision: hierarchy placement + no-cancel promotion; wired to PromotionPanel.prefab
---

# Pawn promotion — popup UI to choose piece

## Overview

When a **pawn moves to the back rank** (by stepping forward or capturing), the game must **not** immediately finish the turn by assuming a queen. Instead, a **popup UI** appears with four choices: **queen, rook, bishop, knight**. The player **taps one option**; then the pawn is **replaced on that square** with the chosen piece, the turn ends, and the board updates as usual.

This document describes how to implement that experience on top of the existing **Chess.Core** rules engine and **Chess.Unity** click-to-move controller.

## Player experience (what we are building)

1. Player selects their pawn and clicks a **legal** square on the **8th rank** (white) or **1st rank** (black)—either an empty square ahead or an enemy-occupied diagonal.
2. **Popup opens** using the existing **`PromotionPanel`** UI prefab (see below), **hidden by default** in the scene. It offers **four** promotion choices: Queen, Rook, Bishop, Knight (each button has TMP child text today).
3. Player clicks **one** option.
4. **Popup closes.** The pawn is gone from `from`; the destination square shows the **correct prefab** for the chosen piece. Side to move and game-over status refresh like any other move.
5. If the popup is open, **clicks on the board do not apply another move** until the player has chosen a piece. **There is no cancel:** the dimmed backdrop and any click outside the four buttons do **not** dismiss the panel; the player **must** pick Queen, Rook, Bishop, or Knight to continue.

That is the product behavior; the technical sections below exist only to make steps 2–4 reliable and rule-correct.

## Current state (codebase)

- **Move list:** `MoveGenerator.AddPawnMoves` only creates promotion moves with **queen** today (`MoveGenerator.cs`). The engine must list **all four** promotion piece types so the chosen option maps to a real `Move` with the right `Promotion` field.
- **Applying moves:** `Board.ApplyMove` already promotes using `Move.Promotion` when a pawn lands on rank 0 or 7 (`Board.cs`).

## Desired end state

- **Rules:** Every legal pawn push or capture to the back rank exists in the legal-move list as **four** moves (Q, R, B, N), filtered by check as today.
- **UI:** Use **`Assets/Features/Promotion/PromotionPanel.prefab`**: four **`Button`**s (one per piece) with TMP labels; show/hide the panel root while waiting for promotion after a valid promotion destination click. Layout or styling may be edited in that prefab as needed.
- **Flow:** After the popup choice, call `ApplyMove` with the **exact** `Move` that matches `(from, to, Promotion)`—never guess from `(from, to)` alone.
- **Highlights:** Only **one** legal-move marker per destination square from the selected pawn, so promotion does not draw four stacked highlights on the same cell.

### Confirmed decisions

- **Scene setup:** The `PromotionPanel` instance **lives in the scene hierarchy** as a child of the game **Canvas** (reference it from `ChessGameController` with `[SerializeField]`). Do **not** use `Instantiate` for this panel at runtime unless product requirements change.
- **No escape from promotion:** Backdrop clicks, empty space, and **Esc** must **not** dismiss the panel. The only way forward is one of the **four promotion buttons**—the player **must** promote.

### Automated verification

- Edit Mode tests in `Assets/Features/Chess/Tests/Editor/MoveGeneratorTests.cs`: at least one FEN where a pawn can promote; assert **four** legal promotion moves for the same `from`/`to` with `Promotion` ∈ {Queen, Rook, Bishop, Knight}.
- Project compiles (Chess.Core + Chess.Unity).

### Manual verification

- Play Mode: promote by **advance** and by **capture**; each option shows the right model and the game continues correctly.
- Popup visibility: hidden at start and after choice; visible only during promotion pick.
- With the panel open, **backdrop / off-button clicks** and **Esc** do **not** close it; only a **promotion button** completes the move.
- Highlight on promotion square is a **single** dot.

## What we are not doing

- Promotion choice for a future **AI** opponent (only human-driven popup for now, unless you reuse the same four moves programmatically later).
- **Cancel, dismiss-on-backdrop, or Esc to close** the promotion UI (confirmed: **out of scope**—player must pick a piece).
- Fancy promotion **animation** beyond swapping prefabs in `RebuildPieces`.
- Full **PGN** or extended notation export.

## Implementation approach (mapped to the UX)

| Player step | Implementation |
|-------------|----------------|
| Clicks promotion destination | `ChessGameController`: resolve clicks against `_legal`. If this `(from, to)` has **one** non-promotion move, apply as today. If it is **promotion**, **open popup** and remember `from`/`to`; do **not** call `ApplyMove` yet. |
| Sees four options | **Hierarchy:** `PromotionPanel` is already **placed under the scene Canvas** (not instantiated at runtime). `ChessGameController` uses **serialized refs** to the root and four buttons; root starts **inactive**. |
| Clicks Queen / Rook / Bishop / Knight | Handler finds the `Move` in `_legal` with matching `Promotion`, calls `ApplyMove`, closes panel, clears selection, `RebuildPieces`, recomputes `_legal`, updates status and highlights. |

Supporting work:

- **Core:** Emit four promotion moves in `MoveGenerator` for each promotion push/capture.
- **Highlights:** Deduplicate by destination square for moves from the selected piece in `LegalMoveHighlight.Refresh`.
- **Restart / new game:** Hide popup and clear any pending promotion state.

---

## Phase 1: Chess.Core — four promotion moves per square

### Overview

For each pawn advance or capture to the back rank, add four pseudo-legal moves with `Promotion` = Queen, Rook, Bishop, Knight (existing legality filter in `GenerateLegalMoves` remains).

### File

`Assets/Features/Chess/Core/MoveGenerator.cs`

### Changes

Replace the single `PieceType.Queen` promotion `dest.Add(...)` calls with a loop or helper, e.g. `AddPromotionMoves(dest, from, to)`, that adds `new Move(from, to, pt)` for each `pt` in `{ Queen, Rook, Bishop, Knight }`.

**Implementation note:** Build and run new generator tests before wiring UI.

---

## Phase 2: Unity — `PromotionPanel` prefab + controller

### Overview

Wire the existing promotion UI prefab and connect it to the “promotion destination clicked → choose → apply” flow.

### Existing UI asset

**Prefab:** `Assets/Features/Promotion/PromotionPanel.prefab`

- **Root:** GameObject **`PromotionPanel`** — `RectTransform` stretched to parent, **`Image`** (semi-transparent fullscreen-style backdrop). Use **`SetActive`** on this root to show or hide the popup.
- **Children:** Four **`Button`**s with **`UnityEngine.UI.Button`**:
  - `QueenPromotionButton` — child TMP **`QueenTxt`**
  - `RookPromotionButton` — child TMP **`Rooktxt`**
  - `KnightPromotionButton` — child TMP **`Knight TXT`**
  - `BishopPromotionButton` — child TMP **`Bishop Txt`**
- The prefab root is **not** a `Canvas`; place the instance as a child of a scene **Canvas** (with **GraphicRaycaster** as usual). **EventSystem** must exist for clicks.
- **Prefabs may be adjusted** (layout, colors, sort order, `CanvasGroup` for fade, etc.) as long as the four buttons remain identifiable for scripting.

### Files

- `Assets/Features/Chess/Unity/ChessGameController.cs` — show/hide **`PromotionPanel`** root, pending `(from, to)`, `OnPromotionChosen(PieceType)`; ignore board clicks while promotion is pending (simplest: all board clicks until choice).
- **Scene:** Canvas hierarchy contains the `PromotionPanel` instance (**placed in editor, not `Instantiate`**); controller holds serialized refs.

### Changes (controller)

- Serialize **`GameObject` / `RectTransform` `promotionPanelRoot`** (the **`PromotionPanel`** root) and **four `Button`** references (`queen`, `rook`, `bishop`, `knight`), **or** a single `Button[]` with a fixed order documented in code.
- In `Start`/`Awake`, add **`onClick`** listeners that call `OnPromotionChosen` with `PieceType.Queen` / `Rook` / `Bishop` / `Knight` (no need for Inspector persistent events if wired in code).
- On board click that resolves to **multiple** legal moves with the same `from`/`to` (promotion): **`promotionPanelRoot.SetActive(true)`**, store pending `from`/`to`; **do not** `ApplyMove` yet.
- After choice: find `Move` in `_legal` with matching `From`, `To`, `Promotion`; `ApplyMove`; **`promotionPanelRoot.SetActive(false)`**; clear pending; `RebuildPieces`; refresh `_legal`, highlights, status.
- **`RestartGame`:** hide panel, clear pending promotion state.

**Implementation note:** Manual Play Mode check: popup appears only for promotion; each button promotes to the correct piece prefab.

### Prefab hygiene (done in repo)

- Bishop button GameObject was renamed to **`BishopPromotionButton`** (no trailing space) so names stay stable for tools and scripting.

---

## Phase 3: LegalMoveHighlight — one marker per destination

### Overview

From the selected square, show at most one legal marker per `to` square so promotion does not stack four quads.

### File

`Assets/Features/Chess/Unity/LegalMoveHighlight.cs`

### Changes

Use a `HashSet<int>` for destination squares already used when placing legal-move markers from the selected piece (both for pool sizing and placement). Selection marker for the pawn’s square unchanged.

---

## Phase 4: Tests

### File

`Assets/Features/Chess/Tests/Editor/MoveGeneratorTests.cs`

### Changes

Add tests that fix promotion FENs and assert four promotion moves (and correct `Promotion` values) where the position allows all four under standard rules.

Optional: update `3d-chess-standard-rules-project.md` if it still states queen-only promotion.

---

## Research

- `Assets/Features/Promotion/PromotionPanel.prefab` — promotion popup (root **`PromotionPanel`**, buttons **`QueenPromotionButton`**, **`RookPromotionButton`**, **`KnightPromotionButton`**, **`BishopPromotionButton`**, TMP children as listed in Phase 2)
- `Assets/Features/Chess/Core/MoveGenerator.cs` — pawn promotion expansion  
- `Assets/Features/Chess/Core/Board.cs` — `ApplyMove` + promotion piece type  
- `Assets/Features/Chess/Unity/ChessGameController.cs` — input and move application  
- `Assets/Features/Chess/Unity/LegalMoveHighlight.cs` — legal move visuals  

## Implementation status

- [x] **Phase 1** — `MoveGenerator.AddPromotionMoves` (Q, R, B, N) for push and capture to the back rank.
- [x] **Phase 2** — `ChessGameController`: pending promotion, panel show/hide, button `onClick` wiring; optional hierarchy bind (`Canvas` → `PromotionPanel`, buttons by name) if Inspector refs omitted; queen fallback only when panel is missing.
- [x] **Phase 3** — `LegalMoveHighlight`: one marker per destination from selected square (`HashSet<int>`).
- [x] **Phase 4** — `MoveGeneratorTests.PawnAdvance_ToBackRank_OffersFourPromotionMoves`.
- [ ] **Manual** — Run Play Mode: add `PromotionPanel` prefab instance under **Canvas** (name **`PromotionPanel`**) so auto-bind runs, or assign the five refs on `ChessGameController`; verify promotion advance/capture and single highlight dot.
