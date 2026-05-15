---
date: 2026-05-29T12:00:00Z
author: Composer
topic: "King cannot lose king-step movement"
tags: [plan, chess, augment, PieceType.King, KingStepLike, capability]
status: draft
last_updated: 2026-05-29
last_updated_by: Composer
implementation_phases_complete: true
---

# King cannot lose king-step movement — implementation plan

## Overview

Prevent **`PieceType.King`** squares from ever losing **`PieceMovementCapability.KingStepLike`**. Players may augment kings with rook/knight/etc., but **`RemoveMovement_king`** must not strip the core one-square king geometry; **castling** and **check** remain tied to the real king as today. Non-king pieces that gain **`KingStepLike`** keep the current behavior: they may add/remove that augment freely.

## Current State Analysis

- **Defaults:** `PieceMovementCapabilityDefaults.For(PieceType.King)` is **`KingStepLike`** only (```34:34:Assets/Features/Chess/Core/PieceMovementCapability.cs```). FEN seeding assigns defaults per `Piece.Type` (```67:72:Assets/Features/Chess/Core/Fen.cs```).
- **Revocation:** `Board.RemoveCapability` applies **`&= ~mask`** with **no** piece-type guard (```195:199:Assets/Features/Chess/Core/Board.cs```). Any caller (including Unity) can clear **`KingStepLike`** on a king.
- **UI:** `AugmentPanelView.ApplyInteractable` treats **`KingStepLike`** like other families: **Remove** is enabled whenever the bit is set (```68:71:Assets/Features/Chess/Unity/AugmentPanelView.cs```). For a king, that bit is always set, so **Remove king** is always clickable and **revokes** king-step.
- **Sanitization:** `PieceMovementCapabilityRules.SanitizeForPieceType` is currently a **passthrough** (```42:43:Assets/Features/Chess/Core/PieceMovementCapability.cs```), so **`ApplyMove`** does not repair a king missing **`KingStepLike`** (```288:290:Assets/Features/Chess/Core/Board.cs```).

**Risk if unfixed:** A king with **`KingStepLike` cleared** still has **`PieceType.King`** for **`AddCastling`** and **`FindKing`**, but **`MoveGenerator`** only adds **`AddKingMoves`** when **`KingStepLike`** is set (```43:44:Assets/Features/Chess/Core/MoveGenerator.cs```), and **`IsSquareAttacked`** king-ring detection keys off **`KingStepLike`** (```141:148:Assets/Features/Chess/Core/Board.cs```). The royal can end up **unable to move** while the game still treats it as the king — inconsistent and game-breaking.

## Desired End State

1. **Core invariant:** On any square occupied by **`PieceType.King`**, **`MovementCapabilitiesOnSquare[sq] & KingStepLike`** is **always non-zero** after any public API call that mutates capabilities or applies a move.
2. **UI:** When the selected piece is **`PieceType.King`**, **RemoveMovement_king** is **not interactable** (Add may remain disabled when the bit is already present, unchanged).
3. **Tests:** Edit Mode test proves **`RemoveCapability(kingSquare, KingStepLike)`** leaves **`KingStepLike`** set for a king; optional second test that a **rook** with **`KingStepLike`** can still have that bit removed (regression guard).

### Automated verification

- Unity compiles; **`Chess.Core`** stays free of **`UnityEngine`**.
- **`CapabilityMovementTests`** (or new test class) passes.

### Manual verification

- Select own king → **Remove king** (king-step) is **greyed out** or does nothing; king still has adjacent moves (and castling when legal).
- Select augmented rook/pawn → **Remove king** still works for **non-kings** with **`KingStepLike`**.

## What We're NOT Doing

- Locking other movement families on the king (players may still remove bishop/rook augments if present).
- Changing **universal** king-step grants on **non-kings** (see `universal-king-step-augment-project.md`).
- FEN extension to encode capability overrides.
- Special-casing “king cannot lose castling” beyond existing rights logic.

## Implementation Approach

Use **defense in depth**:

1. **`Board.RemoveCapability`:** If **`Squares[square].Type == PieceType.King`**, **strip `KingStepLike` from `mask` before applying** (no-op revoke for king-step on royalty). Single choke point for programmatic revokes.
2. **`PieceMovementCapabilityRules.SanitizeForPieceType`:** If **`type == PieceType.King`**, **`caps |= KingStepLike`**. Ensures **`ApplyMove`**, future loaders, or any bug that drops the bit **self-heals** when writing post-move capability state.
3. **`AugmentPanelView`:** For **`mask == KingStepLike`** and **`isRemove`**, set **`interactable = false`** when **`piece.Type == PieceType.King`**. Restore a named **`Piece piece`** parameter (replace discard `_`) for the type check.

**Note:** `FilterGrantMask` / `AddCapability` need **no** change for this feature (adding king-step to a king is redundant but harmless).

---

## Phase 1: Core invariant

- [x] Complete (core + automated tests below)

### Overview

Guarantee kings cannot lose **`KingStepLike`** via **`RemoveCapability`** or leave **`ApplyMove`** without it.

### Changes required

**File:** `Assets/Features/Chess/Core/Board.cs`

- **`RemoveCapability`:** Before `&= ~mask`, if the occupant is **`PieceType.King`**, compute  
  `mask &= ~PieceMovementCapability.KingStepLike`  
  (equivalently: **`mask` must not include `KingStepLike` when revoking on a king**). Update XML to state that king-step cannot be removed from a king.

**File:** `Assets/Features/Chess/Core/PieceMovementCapability.cs`

- **`SanitizeForPieceType`:** If **`type == PieceType.King`**, return **`caps | PieceMovementCapability.KingStepLike`**. Expand `<remarks>` to document **mandatory king-step for kings** (royal identity vs geometry).

**Implementation note:** After this phase, run existing Edit Mode tests; add the new test below before closing the task.

```csharp
// Illustrative — SanitizeForPieceType
if (type == PieceType.King)
    caps |= PieceMovementCapability.KingStepLike;
return caps;
```

```csharp
// Illustrative — RemoveCapability body
if (Squares[square].Type == PieceType.King)
    mask &= ~PieceMovementCapability.KingStepLike;
MovementCapabilitiesOnSquare[square] &= ~mask;
```

---

## Phase 2: Unity panel + tests

- [x] Complete (panel + tests; manual UI pass still required)

### Overview

Align the augment UI with the invariant; lock behavior with tests.

### Changes required

**File:** `Assets/Features/Chess/Unity/AugmentPanelView.cs`

- In **`ApplyInteractable`**, when **`mask == PieceMovementCapability.KingStepLike`** and **`isRemove`** and **`piece.Type == PieceType.King`**, set **`b.interactable = false`** and **return**.
- Rename parameter **`Piece _`** → **`Piece piece`** for the above (only use `piece.Type`).

**File:** `Assets/Features/Chess/Tests/Editor/CapabilityMovementTests.cs`

- **`King_RemoveKingStep_NoOp_KingKeepsAdjacentMoves`** (name as preferred): **`Fen.Parse`** a position with a white king on a known square → **`RemoveCapability(..., KingStepLike)`** → assert **`KingStepLike`** still set; **`MoveGenerator.GenerateLegalMoves`** contains at least one move **from king square** (or assert mask bit only if you isolate empty board king in center).

- **`NonKing_CanStillRemoveKingStep`:** Rook (or pawn) **`AddCapability`** **`KingStepLike`** then **`RemoveCapability`** same → bit cleared (**optional** compact test).

**Implementation note:** Manual pass in-editor: king **RemoveMovement_king** disabled; non-king augmented piece removal still works.

---

## Research

- Capability model: `Assets/Features/Chess/Core/PieceMovementCapability.cs`
- Revoke path: ```142:148:Assets/Features/Chess/Unity/ChessGameController.cs``` → **`Board.RemoveCapability`**
- Prior related plan: `Assets/Documentation/WIP/piece-movement-gimmicks/plans/universal-king-step-augment-project.md` (king-step on any piece — this plan adds **exception**: **kings retain** **`KingStepLike`**)
