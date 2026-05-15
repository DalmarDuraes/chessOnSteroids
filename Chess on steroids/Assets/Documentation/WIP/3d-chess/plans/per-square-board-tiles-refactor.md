---
date: 2026-05-13T12:00:00Z
author: Composer
topic: "Per-square chess board tiles (checker materials)"
tags: [plan, implementation, chess, unity, board, materials]
status: draft
last_updated: 2026-05-13
last_updated_by: Composer
reference_materials:
  - Assets/Materials/Board_black.mat
  - Assets/Materials/Board_white.mat
---

# Per-square chess board tiles — Implementation plan

## Overview

Replace the **single monolithic board cube** in `ChessBoardView.BuildDefaultVisualsAndCollider()` with **64 discrete square tiles**, laid out on the same logical 8×8 grid as today, using a **standard chessboard checker pattern** and the project materials **`Board_black.mat`** and **`Board_white.mat`**. Raycast-to-square behavior and `squareSize` / `origin` semantics stay compatible with the existing rules layer (`Square` indices unchanged).

## Current State Analysis

- **Board mesh**: One `PrimitiveType.Cube` scaled to `8 × squareSize`, centered at `origin + (3.5f * squareSize, -0.05f, 3.5f * squareSize)` local; single `BoxCollider`; brown tint applied in code (`ChessBoardView.cs` approx. lines 49–62).
- **Coordinates**: `GetWorldPositionForSquare` / pieces use **corner of cell (a1-origin style)**: local `(origin.x + f * squareSize, y, origin.z + r * squareSize)` — see `ChessBoardView.cs` lines 20–28, 34–46.
- **Materials**: URP Lit (`guid: 933532a4fcc9baf4fa0491de14d08ed7`). **`Board_black.mat`** has `_BaseColor` ≈ white; **`Board_white.mat`** has `_BaseColor` ≈ dark brown/red — **names and tints are inverted** relative to “light/dark square” English. Implementation must use **two serialized `Material` fields** (“dark square” / “light square” or parity-based assignment) so you can drag **`Board_black`** / **`Board_white`** correctly without code assuming file names match visual intent.

## Desired End State

- **64 child objects** (e.g. parent `BoardSquares`), each representing one square `(file, rank)`.
- **Checker assignment** per FIDE-style board: **light square on h1** → with `h1` = file `7`, rank `0`: light when `(file + rank) % 2 == 1`, dark when `(file + rank) % 2 == 0` (equivalently `a1` is dark). Same formula works for all 64 squares.
- Each tile uses **`MeshRenderer.sharedMaterial`** = either **`lightSquareMaterial`** or **`darkSquareMaterial`** `[SerializeField]`, default-assigned in the Inspector to `Assets/Materials/Board_white.mat` and `Assets/Materials/Board_black.mat` **only if** that matches desired look; otherwise swap in Inspector once.
- **Collision**: Each tile (or a single thin collider layer) must keep **`TryRaycastToSquare`** working with the **same** `FloorToInt` mapping from local `x,z` to `file,rank` (origin still at **bottom-left corner of a1** cell as today).
- **Tile geometry**: Thin box per square (height small vs `squareSize`), centered in the cell for a clean look; **optional follow-up**: move **`GetWorldPositionForPiece`** to **cell centers** `(f + 0.5f, r + 0.5f) * squareSize` so pieces sit visually in the middle of each tile (currently pieces align to the **corner** of the cell — same refactor can fix that in one pass).

### Automated verification

- Unity compiles after changes.
- No duplicate `BoardSurface` / duplicate `BoardSquares` roots on repeated play (build clears or uses idempotent pattern).

### Manual verification

- In Scene view, **8×8 checker** matches standard chess orientation; **h1** is the **light** material slot you assigned for `(f+r)%2==1`.
- Play Mode: **clicking squares** still selects/moves as before; ray hits tile colliders.
- Materials show expected colors after you set **light vs dark** slots (swap if names vs colors confuse).

## Key Discoveries

- Current board: ```49:62:Assets/Features/Chess/Unity/ChessBoardView.cs``` — single cube + color tint.
- Square indexing: ```1:16:Assets/Features/Chess/Core/Square.cs``` — `Index = rank * 8 + file`, rank 0 = White’s first rank.
- Material assets exist at `Assets/Materials/Board_black.mat` and `Board_white.mat` (URP Lit); **visual vs asset name** may require swapped Inspector assignment.

## What We're NOT Doing

- Changing **rules core** (`Board`, `MoveGenerator`) or square indexing.
- **Procedural** UV atlases or a single texture atlas for the whole board (explicitly per-square materials as requested).
- **Fancy** edge bevels, raised borders, or animation (unless added later).
- **Addressables** loading of materials (local `[SerializeField]` references only).

## Implementation Approach

1. Add **`[SerializeField] Material lightSquareMaterial`** and **`darkSquareMaterial`** on `ChessBoardView` (or one field “material A/B” with XML doc describing parity). Optionally use **`[SerializeField] bool invertChecker`** if designers want one-click swap without re-dragging materials.
2. Replace **`BuildDefaultVisualsAndCollider()`**:
   - Clear/destroy previous board visuals under a known child name (e.g. `BoardSquares`) to avoid duplicates on re-run.
   - For `file` 0..7, `rank` 0..7: create primitive **cube** (or quad rotated flat); **local position** = cell **center**: `origin + new Vector3((file + 0.5f) * squareSize, tileThickness * 0.5f, (rank + 0.5f) * squareSize)`; **local scale** = `(squareSize, tileThickness, squareSize)` with small `tileThickness` (SerializeField default 0.02f–0.1f).
   - `bool isLight = ((file + rank) & 1) == 1`; assign `sharedMaterial` accordingly.
   - Name tiles for debugging, e.g. `Sq_a1` via `Square.ToAlgebraic`.
3. **Raycast**: With origin at **a1 corner** and tiles **centered** in cells, `FloorToInt((p.x - origin.x) / squareSize)` still returns correct `file`/`rank` for points on top faces (Mathf.Floor of hit projected to local xz unchanged).
4. **Optional (recommended)**: Update **`GetWorldPositionForSquare`** / **`GetWorldPositionForPiece`** to use **cell centers** `(f + 0.5f, r + 0.5f)` for X/Z so pieces align with new tiles; update **`LegalMoveHighlight`** if it assumed corner positions (it calls the same API — will auto-fix).

## Phase 1: Implement per-square tiles

### Overview

Implement 64-tile build, materials, colliders, idempotent cleanup.

### Changes required

#### 1. `ChessBoardView.cs`

**File**: `Assets/Features/Chess/Unity/ChessBoardView.cs`

**Changes**:

- Serialized: `lightSquareMaterial`, `darkSquareMaterial`, `tileThickness` (float).
- Replace body of `BuildDefaultVisualsAndCollider()` with per-square loop; parent transform `BoardSquares`.
- Remove hardcoded brown `sharedMaterial.color` on monolithic cube.
- Document in XML: parity rule and that `h1` must be **light** when `isLight = ((f+r)&1)==1`.

**Implementation note**: After compile + visual check in Scene, pause for manual confirmation before any follow-up polish.

---

## Phase 2 (optional): Piece alignment to cell centers

### Overview

If pieces look offset after tiles use cell centers, adjust `GetWorldPositionForSquare` X/Z to `(file + 0.5f) * squareSize` and `(rank + 0.5f) * squareSize` (keeping `origin` as a1 corner).

### Changes required

#### 1. `ChessBoardView.cs`

**File**: `Assets/Features/Chess/Unity/ChessBoardView.cs`

**Changes**: Single coordinate change for piece/highlights; re-test raycast + one full game.

---

## Research

- **User request**: Per-square board with `Board_black.mat` / `Board_white.mat`.
- **Existing implementation**: `Assets/Features/Chess/Unity/ChessBoardView.cs` (`BuildDefaultVisualsAndCollider`).
- **Materials**: `Assets/Materials/Board_black.mat`, `Assets/Materials/Board_white.mat`.
