---
date: 2026-05-13T12:00:00Z
author: Composer
topic: "Composable piece movement gimmicks"
tags: [plan, implementation, chess, unity, move-generator, gimmicks, augment-panel]
status: implemented
last_updated: 2026-05-17
last_updated_by: Composer
stakeholder_notes: "Identity vs movement masks; royalty only PieceType.King. King-step moves (KingStepLike) are allowed ONLY on the actual king—not on pawn/rook/etc. GrantCapability ignores KingStepLike for non-kings; promotion strips stray KingStepLike if type ≠ King; panel disables king-step toggles unless selected piece.Type is King. Per-square bitmask add/remove; zero mask immobile; promotion carries full mask (within KingStep constraint). No cost/cooldown; fully visible."
---

# Composable piece movement gimmicks — implementation plan

## Overview

Each piece keeps **`PieceType` + color** and a **movement capability bitmask** describing **which movement families** apply (**union**, each family **at most once**). **Families:** **pawn-like**, **knight-like**, **bishop sliders**, **rook sliders**. **King single-step (`KingStepLike`)** applies **only** to **`PieceType.King`** — non‑kings **never** gain, retain, or use that bit. **Castling** stays separate (**real kings** at home + rights). Players use **`AugmentPanel.prefab`** to **add/remove** toggles per family (king-step controls **only when a king is selected**). A piece may strip native families (e.g. remove pawn-like from a pawn); **`None`** → **no** pseudo-moves until a capability returns.

**Two axes (stakeholder):**

- **What it is considered** = **`PieceType` + color** on the board (pawn, knight, king, …). This is **identity**: art, promotion rules, which piece is “the” king for **castling** wiring, etc.
- **How it moves** = **capability mask** **except** **`KingStepLike`**, which is **restricted**: **only squares occupied by `PieceType.King`** may carry or receive king-step capability. Non‑royal pieces **never** slide one square “as a king”; they may still combine pawn + knight + rook + bishop (sliders/leapers) via the panel. **Check / checkmate / stalemate** use **only** the **`PieceType.King`** square (`FindKing`, `IsInCheck`)—unchanged.

**Confirmed product rules:**

- **King-step exclusivity:** **`KingStepLike` exists only when `PieceType` is King** on that square. **`GrantCapability` / `AddCapability`** must **reject or ignore `KingStepLike`** for non‑kings. After **promotion**, **strip `KingStepLike`** if the new **`Piece.Type` is not `King`**. **Move-gen and `IsSquareAttacked`** only apply king-step semantics when **`Piece.Type == King`** (never for other types).

- **No duplicate family:** each movement **type appears at most once** (bitmask; re-adding an already-present bit is a no-op; UI may disable add when set).
- **No cooldown, no cost** — unlimited clicks while the panel is open / piece selected (subject to existing “side to move” selection if unchanged).
- **Information:** movement capabilities are **fully visible** to **both players** (`PieceView` mirror + unchanged board visibility).

This keeps **Chess.Core** authoritative for legality; **`PieceView`** mirrors the bitmask for Inspectors/UI.

## Current State Analysis

- **Piece identity** is `Piece` struct: `PieceType` + `Color` only (`Piece.cs`). Board holds `Piece[64]` (`Board.cs`).
- **Movement** today is **`switch(pc.Type)`** inside `MoveGenerator.GeneratePseudoLegalMoves` (`MoveGenerator.cs` ~23–44).
- **Legality**: pseudo-moves filtered by king-in-check after `Board.ApplyMove` clone.
- **Attacks**: `Board.IsSquareAttacked` hard-codes orthogonal piece types—not capability-aware yet.
- **Unity**: `ChessGameController` + `RebuildPieces`; `PieceView.Init(type, side)` only.

## Desired End State

- **`PieceMovementCapability` `[Flags]`**, e.g. `PawnLike`, `KnightLike`, `BishopLike`, `RookLike`, `KingStepLike` (**`KingStepLike` legal only alongside `PieceType.King`**—enforce in **`Board`/UI`). **Queen** UI may toggle bishop+rook. **Castling**: only **`PieceType.King`**, unchanged path/rights logic.
- **Per-square storage** `PieceMovementCapability[] MovementCapabilitiesOnSquare` parallel to occupancy, **cloned** with `Board.Clone`, **transferred** on moves (captures: mover’s capabilities replace destination; **`Squares`/mask cleared at `From`**).
- **Initial placement** (starting position / FEN): after each `Squares[sq]` assignment, **`MovementCapabilitiesOnSquare[sq] = PieceMovementCapabilityDefaults.ForPieceType(pc.Type)`** so native moves match chess until the player edits the mask.
- **Move generation**: append moves for **each set capability**. **`KingStepLike`** emits adjacent steps **only when `Piece.Type == King`**; **`AddCastling`** only real kings with existing guards (**alongside** king-step when set). Dedupe **`Move`** list (`HashSet`/equality on **`Move`**).
- **Termination / clock**: pawn-specific **`FinishMove` rules** (en passant target, halfmove clock) remain tied primarily to **`PieceType.Pawn`** **and/or** pawn-generated moves—as implemented, keep **`piece.Type`** as today for promotion and king discovery; pawn **double-step**/EP only originate from **`AddPawnMoves`**, which runs only when **`PawnLike`** is set—so stripping **`PawnLike`** disables those behaviours even if **`PieceType`** stays pawn visually.
- **Zero capabilities:** **`MovementCapabilitiesOnSquare[sq]==None`** → **no pseudo-moves** from that square (piece is **immobile** until a capability returns).
- **Promotion:** move capability mask **`from → to`** with the mover, **then strip `KingStepLike` if promoted `Piece.Type` is not `King`** (**no king-step on queens/rooks/etc.**).
- **Panel UX:** Selecting a friendly selectable piece opens **`AugmentPanel`**; **add/remove** per family. **King-step add/remove interactable only when selected `Piece.Type == King`** (otherwise hidden or grayed).
- **Tests:** immobile when mask `None`; **pawn may stack pawn+rook+knight+bishop sliders** (**never `KingStepLike`**); **`GrantCapability(KingStepLike)` fails for rook**; king retains king-step semantics; **`IsSquareAttacked`** parity.

### Automated verification

- Unity compiles; **Core** stays free of `UnityEngine`.
- Edit Mode tests extended for capability mask semantics and attack detection.

### Manual verification

- Strip **all** capabilities from a piece → no highlights from that square; restore one → moves return.
- On **pawn**, stack rook + knight (no king-step controls); king-step toggle **only appears for king**.
- **Augment panel** add/remove wired on **`AugmentPanel.prefab`** buttons.
- Opponent plainly sees implications (highlighted legal replies / mirrored state).

## Key Discoveries

Move dispatch remains centralized today in `GeneratePseudoLegalMoves` (`MoveGenerator.cs`); it becomes **capability-driven** while **`Piece.Type`** still drives visuals, **`FindKing`**, promotion replacement, castling rook updates, etc.

## Assumptions (resolves design gaps)

| Topic | Assumption |
|--------|------------|
| **Storage** | `PieceMovementCapability[]` length 64 on **`Board`**; **`Piece` struct unchanged**. |
| **Defaults** | Static helper **`PieceMovementCapabilityDefaults.For(PieceType)`** returns starting mask (e.g. rook → **`RookLike`** only; king → **`KingStepLike`**; queen → bishop+rook; pawn → **`PawnLike`**; knight → **`KnightLike`**; bishop → **`BishopLike`**). |
| **Duplicates** | **Impossible by model** (single bit per family); **grant = OR**, **revoke = AND NOT**. |
| **Castling vs king-step** | **`KingStepLike`** = **`AddKingMoves`**-style steps **only**. **`AddCastling`** invoked **only** for **`PieceType.King`** with existing **`AddCastling`** guards (unchanged). |
| **FEN reload** | Masks reset to **defaults per placed type** unless a later phase adds augmented FEN—not in v1. |
| **Check, mate, royalty** | **Only `PieceType.King`** decides which square is royal and drives check/mate/stalemate. **`KingStepLike`** applies **only on kings**, so non‑royal pieces never use king-step for moves or attacks. |
| **`KingStepLike` eligibility** | **`PieceType.King` only.** Reject **`KingStepLike`** grants on non‑kings; strip on promotion when result type ≠ **`King`**; panel hides king-step toggles unless selection is **`King`**; **`IsSquareAttacked`** uses king-step **only when `Piece.Type == King`**. |

## What We're NOT Doing

- **`KingStepLike`** (king-style one-square moves) **on pawn, knight, bishop, rook, or queen**.
- Cooldowns, costs, currency, or per-match augment budgets (confirmed **none**).
- Hidden/fog capabilities (confirmed **fully visible**).
- Editor-only gating of the augment panel in v1.
- Behaviour VMs, networked sync, PGN with capability state (out of scope for v1 persistence).

## Implementation Approach

1. Add **`PieceMovementCapability`** enum + **`Board.MovementCapabilitiesOnSquare`**; **`Clone`/`ApplyMove`** transfer semantics; **`StartingPosition`/FEN** initializers seed defaults per square after piece placement (may require hook in **`Fen.Parse`** or post-pass).
2. Replace type-only **`switch`** pseudo-move core path with **`AppendMovesForCapabilities(board, sq, piece, caps, dest)`** (internal factoring); keep castling conditional on **`PieceType.King`** **only**.
3. **`IsSquareAttacked`**: attackers considered by **capabilities** on each enemy square + **`Piece.Type`** nuances (pawns: **only if `PawnLike`**, etc.—align with generators).
4. **`Move`** dedupe unchanged requirement.
5. **`PieceView`**: mirror **`PieceMovementCapability`** (rename field from “gained only” wording to **`movementCapabilities`** or keep label but document “full enabled set”).
6. **`ChessGameController`**: **`GrantCapability`** / **`RevokeCapability`**; panel **Open/Close** on selection; wire prefab **add/remove** listeners.
7. **`AugmentPanelView`**: caches selected square + **`Piece.Type`** for panel rules; toggle **interactable** (**add**: disable if bit set; **king-step**: show only **`Type == King`**); keep `RevokeKingStep` reachable only on king.

---

## Phase 1: Core data model & board lifecycle

### Overview

Define **`PieceMovementCapability`** `[Flags]`; add **`PieceMovementCapability[] MovementCapabilitiesOnSquare`** to **`Board`**, **`Clone`**, **`ApplyMove` transfer** (**clear from**; **set to** on **to**; capture discards victim mask). Add **`PieceMovementCapabilityDefaults.For(PieceType)`**.

### Changes required

**New file**: `Assets/Features/Chess/Core/PieceMovementCapability.cs`

**File**: `Assets/Features/Chess/Core/Board.cs` — field, clone, **`ApplyMove`**, ensure **empty square** clears mask at indexing.

**File**: `Assets/Features/Chess/Core/Fen.cs` (or **`Board.StartingPosition`** path) — after parse, initialise masks from defaults per **`Squares`**.

**Pause:** compile-only; existing standard games behave identically once defaults match Orthodox types.

---

## Phase 2: Move generation driven by capability mask + dedupe

### Overview

**`GeneratePseudoLegalMoves`**: for each friendly piece read **`caps = MovementCapabilitiesOnSquare[sq]`**; **if `caps==None`** → skip; else append **PAWN / KNIGHT / BISHOP / ROOK / KINGSTEP** modules per flag **`King`** also runs **`AddCastling`** unchanged when **`Piece.Type==King`**. Remove reliance on **`switch(Type)` as sole dispatch** except where **`Type`** gates castling/promotion.

### Changes required

**`MoveGenerator.cs`** — refactor helpers as today’s plan; **`AddPawnMoves`** guarded by **`PawnLike`**, etc.

**`Move.cs`** — equality for dedupe if needed.

**Tests** — rook-only grant on pawn default (after revoke pawn?), zero-move immobile.

---

## Phase 3: `IsSquareAttacked` parity

### Overview

Attacks rays/jumps keyed off **enemy `MovementCapabilitiesOnSquare`** (and **`Piece.Type`** only where necessary, e.g. castling unrelated to attack rays). **Mirror** move generator semantics.

---

## Phase 4: Unity—`PieceView`, panel add/remove, controller

### Overview

**`PieceView`** — `[SerializeField] PieceMovementCapability movementCapabilities`; **`Init(..., PieceMovementCapability caps)`**.

**`ChessGameController`** — selection opens **`AugmentPanelView`**; **`GrantCapability`** / **`RevokeCapability`**; refresh legal + **`RebuildPieces`** (or incremental view sync).

**`Assets/Features/Augments/AugmentPanelView.cs`** + **`AugmentPanel.prefab`**:

- **`Open(int sq, PieceMovementCapability current)`** updates UI state.
- **Add** buttons: grant single bit (**no-op if present**).
- **Remove** buttons: revoke single bit (**no-op if absent**).

**Prefab** already extended by stakeholder with remove buttons — wire **`onClick`** to **`Revoke`** paths.

---

## Phase 5: Hardening

- Regression castling/native king when **`KingStepLike`** toggled.
- Public **`Board`** API: **`SetCapabilityRaw` discouraged** — prefer **`AddCapability`/`RemoveCapability`** with validation (**`Square.IsValid`**, occupancy optional guard).
- Update **`status`** in frontmatter when shipped.

---

## Research

- `MoveGenerator.cs`, `Board.cs`, `Fen.cs`, `ChessGameController.cs`, `PieceView.cs`, **`Assets/Features/Augments/AugmentPanel.prefab`**.
- Older plan drafts under `Assets/Documentation/WIP/3d-chess/plans/`.
