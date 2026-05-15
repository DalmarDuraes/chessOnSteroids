---
date: 2026-05-18T12:00:00Z
author: Composer
topic: "Universal king-step augment (non-royal)"
tags: [plan, chess, king-step, augment, capability, bugfix, parity]
status: draft
last_updated: 2026-05-18
last_updated_by: Composer
stakeholder_notes: "Pivot from prior doc: KingStepLike may be granted to any piece via panel; only PieceType.King is royal for check/mate/castling eligibility."
---

# Universal king-step augment (non-royal) — implementation plan

## Overview

Allow **every piece type** to gain **`PieceMovementCapability.KingStepLike`** (one square in any orthogonal/diagonal direction, same geometry as existing **`AddKingMoves`**). **Identity does not change:** `PieceType` + `Color` stay the same; **`FindKing`**, **check**, **checkmate**, **stalemate**, and **castling** logic continue to key off **only `PieceType.King`**. A pawn with king-step can **give check** to the opponent’s **real king**, but is **never** itself a king for “in check” / game end.

This supersedes the **“KingStep only on actual king”** restriction captured in `Assets/Documentation/WIP/piece-movement-gimmicks/plans/composable-piece-movements-project.md`.

## Current State Analysis

Royalty is already correct: **`Board.FindKing`** only returns `PieceType.King` squares; **`IsInCheck`** only asks whether that square is attacked.**  
`KingStepLike` is artificially limited to royal pieces in four places:

| Location | Behavior today | File (approx.) |
|----------|----------------|----------------|
| Capability rules | `SanitizeForPieceType` / `FilterGrantMask` clears `KingStepLike` when `type != King` | ```42:49:Assets/Features/Chess/Core/PieceMovementCapability.cs``` |
| `AddCapability` | Uses `FilterGrantMask` → king-step grants on rook/pawn noop | ```186:193:Assets/Features/Chess/Core/Board.cs``` |
| Pseudo-move dispatch | `AddKingMoves` only if `KingStepLike && pc.Type == King` (**castling unchanged**: still `PieceType.King` only) | ```43:46:Assets/Features/Chess/Core/MoveGenerator.cs``` |
| Attack rays (king ring) | Adjacent threats require **`PieceType.King` **and** `KingStepLike` | ```141:148:Assets/Features/Chess/Core/Board.cs``` |
| Unity panel | King add/remove **disabled** if `piece.Type != King` | ```60:70:Assets/Features/Chess/Unity/AugmentPanelView.cs``` |

**Promotion:** `ApplyMove` sets `capsTo = SanitizeForPieceType(placed.Type, fromCaps)` (```282:289:Assets/Features/Chess/Core/Board.cs```), which today **drops** king-step when promoting to queen etc.

**Tests:** `KingStep_GrantIgnoredOnRook` asserts king-step stays off rook; **`Promotion_StripsKingStepWhenNotKing`** asserts strip after promotion (**both conflict** with the new rule).

## Desired End State

- **`Board.AddCapability`**: **`KingStepLike`** may attach to **any** non-empty occupancy (still reject empty squares; no change to **`RemoveCapability`**).
- **`GeneratePseudoLegalMoves`**: Emit **king-step** moves whenever **`KingStepLike`** is set, **regardless of `PieceType`**. **`AddCastling`** remains **`if (pc.Type == PieceType.King)`** only (castle is not part of **`KingStepLike`**).
- **`IsSquareAttacked`**: For the **king-ring** probe, treat an enemy as attacking if **`KingStepLike`** is set (**do not** require **`PieceType.King`**), so augmented pieces threaten the **actual** king correctly.
- **`AugmentPanelView`**: **`KingStepLike`** uses the **same** add/remove enable rules as rook/knight (present bit vs absent); **remove** king-only **`interactable = false`** branch.
- **`PieceMovementCapability`/XML docs** updated to describe “king-step pattern, non-royal allowed.”
- **Rules helper:** Either **`SanitizeForPieceType` becomes passthrough for king-step**, or deletion of that stripping with a short comment that **royalty is not encoded in capabilities**.
- **Tests** updated / added:
  - Rook/pawn **`AddCapability(..., KingStepLike)`** → mask gains bit; pseudo/legal moves include adjacent steps **where rules allow**.
  - **Check**: enemy pawn with **`KingStepLike`** adjacent to **your** **`PieceType.King`** → **`IsInCheck`** true when geometry matches.
  - Promotion: optional case **pawn carries `KingStepLike` promote to queen → queen retains `KingStepLike`** (reflects new sanitize behavior).

### Automated verification

- Unity compiles; **Core** stays free of `UnityEngine`.
- Edit Mode **`CapabilityMovementTests`** (and any new cases) green.

### Manual verification

- Select **pawn/rook/etc.** → **Add king / Remove king** on panel behaves like other families.
- **Castling** still only from **actual king** square with existing rights/path rules.
- **Check** message only cares about **`PieceType.King`** vulnerability; augmented pieces cannot “replace” kings in **`FindKing`**.

## What We're NOT Doing

- Treating **`KingStepLike`** as a **second king** for mate/stalemate (no second royal target).
- Granting **castling** via augment (still **`PieceType.King`** + **`AddCastling`** only).
- FEN serialization of arbitrary capability overrides (already out of scope for v1).
- Rebalancing **promotion + default capability merge** (e.g. whether promoted queen should drop **`PawnLike`**) — **optional follow-up** if odd stacks appear in playtests; not required for this change.

## Implementation Approach

1. **`PieceMovementCapabilityRules`**: Remove **`KingStepLike`** stripping from **`SanitizeForPieceType`** (or make **`FilterGrantMask`** return **`requested`** unchanged if no other filters remain). **`ApplyMove`** promotion path keeps using **`SanitizeForPieceType`** only if it still does something meaningful; otherwise replace with **`fromCaps`** or document passthrough (**simplest**: **`capsTo = fromCaps`** if sanitize becomes identity).
2. **`MoveGenerator`**: Change king-step gate to **`(caps & KingStepLike) != 0`** → **`AddKingMoves`**; keep **`AddCastling`** under **`PieceType.King`** only.
3. **`Board.IsSquareAttacked`**: Adjacent-loop condition: **`pc.Color == byAttacker && (caps & KingStepLike) != 0`**, drop **`pc.Type == PieceType.King`**.
4. **`AugmentPanelView`**: Delete king-only early return; let **`KingStepLike`** fall through to generic add/remove bit logic.
5. **Tests/docs**: Fix **`CapabilityMovementTests`**; add **check** case with augmented non-king threat; update **`composable-piece-movements-project.md`** frontmatter or add “**Superseded by**” note if desired.

---

## Phase 1: Core parity (rules + move gen + attacks)

### Overview

Relax **`KingStepLike`** consistently so **movement** and **attack detection** agree.

### Changes required

**File**: `Assets/Features/Chess/Core/PieceMovementCapability.cs`  

- Doc: **`KingStepLike`** = king *pattern*, not royalty.  
- **`SanitizeForPieceType`**: stop clearing **`KingStepLike`** for non-kings; if the method becomes empty aside from passthrough, consider inlining **`caps`** at call sites (**`Board`** only) per team taste.

**File**: `Assets/Features/Chess/Core/Board.cs`  

- **`AddCapability`** XML (**no king-only filter**).  
- **`ApplyMove`**: if **`SanitizeForPieceType`** is identity, set **`capsTo = fromCaps`** (or keep call if retains future hooks).  
- **`IsSquareAttacked`**: adjacent king-ring uses **`KingStepLike`** without **`PieceType.King`** check.

**File**: `Assets/Features/Chess/Core/MoveGenerator.cs`  

- King-step **`AddKingMoves`** without **`PieceType.King`**; **leave castling gated** on **`PieceType.King`**.

**Implementation note**: Run Edit Mode tests; fix failures before Unity UI phase.

---

## Phase 2: Unity augment panel + tests

### Overview

Expose king-step toggles for all selected pieces; align automated tests with new semantics.

### Changes required

**File**: `Assets/Features/Chess/Unity/AugmentPanelView.cs`  

- Remove **`KingStepLike`** special branch that forces **`interactable = false`** for non-kings.

**File**: `Assets/Features/Chess/Tests/Editor/CapabilityMovementTests.cs`  

- Replace **`KingStep_GrantIgnoredOnRook`** with expectation **`RookLike | KingStepLike`** (or equivalent) and assert **adjacent pseudo/legal** destinations exist **from rook square** where position allows.  
- Replace/remove **`Promotion_StripsKingStepWhenNotKing`** with a test that **`KingStepLike`** **survives** promotion when **`fromCaps`** includes it (**grant before promote**, or **`AddCapability`** + move).  
- Add **`NonKing_WithKingStep_CanThreatenRoyalKing`** (or similar): **white king** attacked by **enemy piece** that is **`!King`** **with** **`KingStepLike`** on adjacent square **`IsSquareAttacked(ksq, enemy)`**.

**Implementation note**: Manual pass in-editor on augment panel naming **`AddMovement_king`** / **`RemoveMovement_king`**.

---

## Research

- Prior feature plan: `Assets/Documentation/WIP/piece-movement-gimmicks/plans/composable-piece-movements-project.md` (**king-step exclusivity** section **obsolete** after this work).
- Code references cited in **Current State Analysis** above.
