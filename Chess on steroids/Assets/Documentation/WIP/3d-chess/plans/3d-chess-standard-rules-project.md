---
date: 2026-05-12T00:00:00Z
author: Composer
topic: "3D Chess (Standard Rules)"
tags: [plan, implementation, chess, unity, 3d]
status: draft
last_updated: 2026-05-12
last_updated_by: Composer
stakeholder_notes: "Imported piece assets; v1 termination = checkmate/stalemate only; promotion = queen only; fixed camera (no orbit)."
---

# 3D Chess (Standard Rules) Implementation Plan

## Overview

Build a **local two-player** 3D chess experience in Unity 6 (project currently **6000.4.1f1**, URP) where an **authoritative rules layer** drives all legality (movement, captures, castling, en passant, promotion, check, checkmate, stalemate). **You import** the finished **3D piece prefabs** (12 variants) and assign them in the registry. The **board is standard**: **8×8** squares with **initial array** and geometry consistent with orthodox chess (one square = one logical cell; physical scale can follow a fixed Unity unit per square, e.g. **1 world unit** nominal square width for layout and raycasts). The **camera is fixed** (single rig/angle for play—no orbit or free camera in v1).

**Why split logic and view:** A **pure C# rules core** (no `UnityEngine` dependency in the core assembly) keeps move generation testable, minimizes bugs in “standard rules,” and keeps prefab/visual changes from touching legality.

## Current State Analysis

- **Unity project** exists with **SampleScene** and template assets; **no chess scripts** under `Assets` besides `TutorialInfo` readme helpers (`Assets/TutorialInfo/Scripts/Readme.cs`).
- **No `Assets/Documentation` tree** yet; this file establishes the feature documentation layout.

## Desired End State

- Two players take turns on **one machine** (hot-seat); side to move is enforced.
- **All piece moves conform** to standard chess rules, including:
  - **Castling** (king-side and queen-side) with correct restrictions (no king in check, no through-check, no moved king/rook, empty path, correct rook identification).
  - **En passant** when a pawn’s double-step creates a capturable “ghost”; correct resolution and turn handling.
  - **Promotion** when a pawn reaches the back rank: **always to queen** in v1 (no underpromotion UI or choice).
- **Illegal moves are rejected** (including moves that leave own king in check).
- **Check**, **checkmate**, and **stalemate** are detected; game can end with a clear outcome message or state flag.
- **Board**: 8×8 visual grid; square centers or colliders align with logical coordinates **a1–h8** under a documented mapping (see Phase 2).
- **Pieces**: Each `(color, type)` pair is instantiated from **dedicated prefabs** (12 prefabs total: 6 types × 2 colors), assigned via a Unity `ScriptableObject` or serialized lookup on a board bootstrap component.

### Automated Verification

- **Unity** opens without script compile errors after each phase.
- **Optional but recommended:** Add **Edit Mode Tests** under an `Editor` folder; run via Unity Test Framework with cases for:
  - Opening pawn moves; knight L-moves; blocked sliders
  - Castling legality matrix (piece in way, attacked squares, moved flags)
  - En passant timing (only immediately after double push)
  - Promotion / capture encoding in move representation
  - Check detection and “no self-check” filtering

*(If tests are deferred, note in Phase 1 pause that manual regression must be broader.)*

### Manual Verification

- Play through scripted scenarios in-editor: **Scholar’s mate attempt blocked by rule**; **castling** with rook under attack but king path safe (should be allowed per rules); **castling through check** disallowed; **en passant** only on the correct turn; **promotion** on all files; **stalemate** patterns.
- Visual: prefab swap per type/color; board scale “feels” standard relative to pieces (art tuning).

## Key Discoveries

- **Greenfield chess implementation** in this repo—no existing chess modules to extend (`grep` over `Assets/**/*.cs` finds no chess-related symbols).
- **Unity 6** baseline should be assumed for API usage and package compatibility.

## Assumptions (Resolves Open Questions)

| Topic | Assumption for this plan |
|--------|---------------------------|
| Multiplayer / AI | **Hot-seat only.** No network, no engine AI in this plan. |
| Draw rules | **Checkmate & stalemate only** for termination in v1. **Threefold repetition** and **50-move rule** are **out of scope** unless added in a later plan. |
| Promotion | **Queen only.** When a promotion move is played, the rules core and view always treat it as **queen**; **no** promotion dialog or underpromotion in v1. |
| Camera | **Fixed** rig (position/rotation set in scene or on a `ChessCameraRig`); no orbit, zoom, or first-person controls in v1. |
| Coordinate system | **White ranks 1–2 toward −Z (or +Z—pick one)** and document in code; files **a–h** mapped to **x** increasing; **never flip** mid-game. |
| Clock / notation / PGN | **Not required** for v1. |
| Piece art | **You import** the 3D models/prefabs and wire them into `PiecePrefabLibrary`; code only requires **root transform** placement per square center (pivot at base recommended). |

## What We're NOT Doing

- Online multiplayer, matchmaking, authentication.
- Chess engine / AI opponent.
- Full FIDE clock, increment, or flag.
- Threefold repetition detection, 50-move rule, dead position tablebases.
- Undo/redo, replay, or PGN export (can be future work).
- **Underpromotion** (rook/bishop/knight) and any promotion picker UI.
- Orbiting / zooming / user-controlled camera (fixed view only in v1).
- Mobile-specific UX beyond basic pointer/click (optional stretch).

## Implementation Approach

1. **Rules core first**: 64-square mailbox or bitboard (mailbox is easier to debug for a first implementation); explicit **move struct** carrying from/to, promotion piece, castling flags, EP capture square; **generate pseudo-legal → filter king safety** for slider/knight/pawn/king individually.
2. **Unity view second**: Build **board geometry + square index map**; **sync transforms** from authoritative state after each legal move.
3. **Input layer**: Raycast → square; select → highlight legal; confirm move → ask rules core; on success **animate or lerp** then commit state (commit should still be rules-driven to avoid desync).
4. **Game flow**: Small **GameSession** / `ChessGameController` holds turn, selected square, game result enum.

**Implementation note:** After each phase, **pause for manual playtesting** before starting the next phase, per team workflow.

---

## Phase 1: Rules Engine (Pure C#)

### Overview

Deliver a **testable** chess rules library: position representation, move application, legal move generation, and terminal state checks **without Unity dependencies**.

### Changes Required

#### 1. New assembly

**Files** (suggested):

- `Assets/Features/Chess/Core/*.cs` — `Board`, `Move`, `Piece`, `PieceType`, `Color`, `MoveGenerator`, `Fen`, `GameRules` (check / checkmate / stalemate). (Uses default `Assembly-CSharp`, no asmdef.)

**Changes**: Implement `FenService.LoadStartingPosition()` or hard-coded **standard start**; maintain **castling rights**, **en passant target**, **halfmove/fullmove** counters if you anticipate 50-move later (can stub counters for now).

**Implementation snippet (illustrativeMove struct fields)**

```csharp
public readonly struct Move {
    public int From { get; }
    public int To { get; }
    public PieceType Promotion { get; } // None for most moves
    public bool IsEnPassant { get; }
    public bool IsCastle { get; }
}
```

#### 2. Unit tests (recommended)

**File**: `Assets/Features/Chess/Tests/Editor/*.cs` — compiles into `Assembly-CSharp-Editor` with the Test Framework package.

**Changes**: Cover en passant, castling paths, promotion, pinned pieces, discovered check.

**Implementation note:** After completing this phase and automated tests (or a documented manual checklist if tests skipped), **pause for confirmation** before Phase 2.

---

## Phase 2: Board & Coordinate Mapping (Unity)

### Overview

Create an **8×8 board** in `SampleScene` (or new scene): either a single mesh with a material shader or **64 child quads/cubes** with shared material. Establish **square center positions** in world space and a **bidirectional map**: `(file, rank) ↔ Vector3` / `(file, rank) ↔ collider instance id`.

### Changes Required

#### 1. Board bootstrap

**File** (suggested): `Assets/Features/Chess/Unity/ChessBoardView.cs`

**Changes**:

- Serialize **origin** (center of a1) and **square spacing** (e.g. `1f`).
- Build or reference 64 named transforms for debugging.
- `TryRaycastToSquare(Ray ray, out int squareIndex)` using `Physics.Raycast` to a board layer.

#### 2. Piece prefab registry

**File**: `Assets/Features/Chess/Unity/PiecePrefabLibrary.cs` (or `ScriptableObject`)

**Changes**: References to **12 prefabs** (`WhitePawn`, … `BlackKing`) filled with **your imported assets**.

**Implementation note:** Confirm **placement height** (Y offset) so pieces sit **on** the board, not intersecting.

#### 3. Fixed camera

**File** (suggested): `Assets/Features/Chess/Unity/ChessCameraRig.cs` *or* scene-only `Camera` (no script if static is enough).

**Changes**: One **fixed** transform for the main view; optionally serialize **look-at** target (board center) for setup in the Inspector. **No** orbit/zoom input handling in v1.

---

## Phase 3: Piece Instances & State Sync

### Overview

On **Start**, instantiate piece prefabs for the **start position** (or FEN). Keep a **dictionary** `squareIndex → PieceView`. When the rules core accepts a move, **update transforms** (snap or tween).

### Changes Required

#### 1. Piece view

**File**: `Assets/Features/Chess/Unity/PieceView.cs`

**Changes**: Holds `PieceType`, `Color`, optional animator hook.

#### 2. Game controller

**File**: `Assets/Features/Chess/Unity/ChessGameController.cs`

**Changes**: Owns `GameState`, handles **select / deselect / move request**, calls `MoveGenerator.GetLegalMoves`, applies `Board.ApplyMove`, refreshes views, switches turn, evaluates **GameResult**.

**Implementation note:** For **captures**, disable or destroy the captured `PieceView` after the move is committed. For **castling**, move both king and rook views.

---

## Phase 4: Special Moves UX & Queen-Only Promotion

### Overview

Ensure **castling** shows both pieces moving; **en passant** removes the correct captured pawn (behind the target square). **Promotion**: when the player selects a pawn move to the **last rank**, the controller and rules core apply it as **promotion to queen automatically**—**no** UI or extra click.

### Changes Required

#### 1. Promotion (automatic queen)

**Files**: `ChessGameController.cs` + rules `MoveGenerator` / `Board.ApplyMove`

**Changes**: Legal pawn moves to the 8th/1st rank are generated **only** as queen promotions (or the move struct always sets `Promotion = Queen` for those targets). The **view** replaces the pawn instance with the **queen prefab** for that color after the move commits.

#### 2. Highlights

**File**: `Assets/Features/Chess/Unity/LegalMoveHighlight.cs`

**Changes**: Projector, decals, or emissive tiles for **selected** and **legal** squares.

---

## Phase 5: Endgame & Feedback

### Overview

Expose **Check** (optional king highlight), **Checkmate**, **Stalemate** via UI text or simple panel. Freeze input or offer **New Game**.

### Changes Required

#### 1. `GameResult` handling

**File**: `ChessGameController.cs` extensions

**Changes**: When `GameResult` is decided, show message and block further moves.

---

## Phase 6 (Optional): Polish & Content

### Overview

Tune **board materials** to match imported pieces; lock **fixed** camera framing (height, FOV) for a clear full-board read; add **move sound**; confirm imported prefab **pivot at base** for clean placement.

---

## Research

- **Original reference:** User request (no external spec file).
- **Related research:** _(Add links or notes under `Assets/Documentation/WIP/3d-chess/research/` if you create them.)_
- **Similar implementation:** N/A in-repo (greenfield).

---

## Revision Notes

- **2026-05-12 (implementation):** Phases 1–5 landed: `Assets/Features/Chess/Core/`, `Assets/Features/Chess/Unity/`, `Assets/Features/Chess/Tests/Editor/`. No asmdefs — default assemblies only. **Scene setup:** Empty with `ChessBoardView`, `ChessGameController`, `LegalMoveHighlight` (assign board reference + legal/selected materials), optional `ChessCameraRig`. Canvas + **TextMeshPro – Text (UI)** for status + optional **Button** → `ChessGameController.RestartGame()`. Create **Piece Prefab Library** asset; assign 12 prefabs or use runtime capsule placeholders.
- **2026-05-12:** Stakeholder confirmed **imported assets**, **mate/stalemate-only** draws in v1, **queen-only promotion**, **fixed camera**. Plan updated to drop promotion UI and orbit camera scope.
- Adjust assumptions table if product owner requires **AI**, **online**, or **full draw rules** in scope; phases 1–3 remain largely the same, but timelines and test coverage expand.
