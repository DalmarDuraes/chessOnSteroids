---
date: "2026-05-15T12:00:00-03:00"
author: Composer
topic: "Photon Fusion 2 — lobby Host/Join to shared MainScene session"
tags: [plan, implementation, photon, fusion2, multiplayer, NetworkRunner, scene-build-settings, disconnect, ux, confirmation-dialog]
status: draft
last_updated: 2026-05-15
last_updated_by: Composer
---

# Photon Fusion 2 — lobby Host / Join to shared gameplay scene

## Overview

Enable **PhotonScene → MainScene flow**: **`MultiplayerHostJoinPanel`** (**HostButton** / **JoinButton**); after connection **both peers** Fusion-load **`MainScene`** into one session. **`PlayerCount` is fixed at 2.** When the session **ends or the user disconnects**, return to **`PhotonScene`** (build index **0**) after appropriate **confirmation dialogs** (**post-checkmate** / **manual quit**, and **peer dropped**). Chess logic stays offline for this milestone; multiplayer scope is **shared session + clean disconnect + confirmations**.

## Product rules (locked)

| Rule | Decision |
|------|----------|
| **Build index 0** | **`PhotonScene`** (lobby/menu). |
| **Build index 1** | **`MainScene`** (gameplay). Reorder **`EditorBuildSettings`** if repo still has MainScene listed first (see Current State). |
| **Players per room** | **Exactly 2** (`StartGameArgs.PlayerCount = 2` for Host and Join). |
| **Disconnect** | **Required**: leaving the Fusion session (**shutdown** runner / callbacks) brings the user back **`PhotonScene`**; handle **peer dropped** where practical (Fusion `OnShutdown` / disconnect reasons). |
| **Confirmation dialogs** | **Required** when: (1) **game ended by checkmate or stalemate** and the user chooses to leave the Fusion session (**Return to lobby?** Confirm / Cancel), and (2) **peer disconnected** (**Other player disconnected. Return to lobby?** Confirm — optional Cancel if UX allows staying on idle board offline). Prefer **blocking modal** once per event; gated so **solo / offline** chess is **unaffected** (**no multiplayer runner** ⇒ no dialogs from these flows). |

## Current State Analysis

- **Photon Fusion** under `Assets/Photon/Fusion/`; **`PhotonAppSettings`** has **App Id Fusion** (`PhotonAppSettings.asset`).
- **`NetworkProjectConfig.fusion`**: **`PeerMode: 0`** — one **`NetworkRunner`** per process (**two builds / two editors**).
- **`EditorBuildSettings`**: **`PhotonScene` is present**, but ordering may still be **`MainScene` then `PhotonScene`** — **must be reordered** to **PhotonScene (0)** / **MainScene (1)** per locked rules (`ProjectSettings/EditorBuildSettings.asset`).
- **`MultiplayerHostJoinPanel.prefab`**: **`HostButton`**, **`JoinButton (1)`**; **`onClick` empty**.
- **`FusionBootstrap.InitializeNetworkRunner`** (`FusionBootstrap.cs` ~636–665) is the reference for **`NetworkSceneManagerDefault`**, **`NetworkObjectProviderDefault`**, **`NetworkSceneInfo`**, **`StartGameArgs`**.

### Key discoveries

| Area | Detail |
|------|--------|
| Gameplay **`SceneRef`** | With **MainScene at index 1**, use **`SceneRef.FromBuildIndex(1)`** (or **`GetBuildIndexByScenePath`** for validation). Never use **PhotonScene** as Fusion’s networked gameplay scene index. |
| Host / Join | **`GameMode.Host`** / **`GameMode.Client`**; **`SessionName`** + **`MatchmakingMode`** per [Fusion matchmaking](https://doc.photonengine.com/en-us/fusion/current/manual/matchmaking). |
| **`PlayerCount`** | **2** Host **and** Join args so Session capacity and matchmaking stay aligned (**locked**). |
| Disconnect | **`NetworkRunner.Shutdown()`** clears simulation; **`INetworkRunnerCallbacks.OnShutdown`** (and related Fusion APIs) usable to **`LoadScene` index 0** and destroy **DontDestroyOnLoad** runner instance so re-Host / re-Join works. |

## Desired End State

1. **Build Settings**: **`PhotonScene`** index **0**, **`MainScene`** index **1** (both enabled).
2. **PhotonScene**: lobby UI + **`FusionLobbyConnector`** (or equivalent) + **`NetworkRunner`** prefab path; **`PlayerCount`** always **2** in **`StartGameArgs`**.
3. **Host**: **`GameMode.Host`**, networked **`Scene`** = **MainScene**, session **visible/open**, **`PlayerCount = 2`**.
4. **Join**: **`GameMode.Client`** + matchmaking joins an available **2-slot** Host session **`Scene`** = **MainScene**; failures surface in UI.
5. **Disconnect**: user (**Back to menu** / **manual quit**) or **remote peer drop** → **confirmation modal** → on confirm → **`Shutdown` + PhotonScene**.
6. **End of game**: when **`GameResult`** is mate or stalemate **and** a Fusion session is active → prompt **Return to lobby?** Confirm runs **same teardown** as disconnect; Cancel keeps board/scene until user uses **Back to menu** (document copy).
7. **Verification**: two clients; Host + Join reach **MainScene**; dialogs fire on mate/stalemate and peer drop as above; repeatable lobby.

## What We're NOT Doing

- **Authoritative chess**, **RPCs**, **NetworkBehaviour** on pieces.
- **Dedicated Server** exe.
- **Host migration / reconnect mid-game** (`HostMigration` stays off unless you broaden scope later).
- **Spectators**, **>2** players, dynamic player count UI.
- **Region picker** UI (defaults only).

## Implementation Approach

1. **`EditorBuildSettings`**: reorder to **PhotonScene (0)** / **MainScene (1)**; single source for indices (constants or **`SceneUtility`**).
2. **`FusionLobbyConnector`**: Host/Join, **`PlayerCount = 2`**, single-flight guard, **`StartGame`** with **`SceneRef.FromBuildIndex(1)`** in **`NetworkSceneInfo`** (**additive** or per Fusion recommendation).
3. **Disconnect subsystem** (**new**): **`INetworkRunnerCallbacks`** impl (Dedicated component on runner or DontDestroy companion); **`FusionLobbyConnector`** exposes **`LeaveSession()`** (**Back to menu**). **Do not** immediately load **PhotonScene** on **`OnShutdown`** / button press — first show **confirmation** (below); **`Confirm`** executes **`runner.Shutdown()`**, then **`OnShutdown`** (or **`Confirm` continuation**) performs **`LoadScene` index **0**** + **`Destroy(runner)`** + reset lobby flags.
4. **Peer disconnect**: **`OnShutdown`** with reasons indicating peer/session end → show **confirmation** (“Other player disconnected. Return to lobby?”); **`Confirm`** = same teardown as manual leave; **`Cancel`** = remain in **MainScene** with session ended (offline board) unless you disallow Cancel.
5. **Post-checkmate / stalemate**: hook **`ChessGameController`** (or small **`GameEndFlowCoordinator`**) when **`GameResult`** becomes **mate** or **stalemate** and **`NetworkRunner`** is **running** → show modal **Return to lobby?** (**Confirm** = **`LeaveSessionConfirmed()`**, **Cancel** = dismiss and keep viewing board).
6. **Editor**: **Play Mode Start Scene** = **PhotonScene** for faster iterate.

7. **Reusable modal UI**: **`ConfirmDialog.prefab`** (or existing project pattern — **TMP** title/body + **Confirm** / **Cancel** buttons) instantiated under **`MainScene` Canvas**, queue-safe **singleton** (`Show(message, onConfirm, onCancel)`), **`selectable`/focus** basics for accessibility.

---

## Phase 1: Build settings — PhotonScene index 0

- [x]

### Overview

Enforce locked **scene order**.

### Changes Required

#### 1. `ProjectSettings/EditorBuildSettings.asset`

**Changes**:

- **`m_Scenes[0]`** → `Assets/Scenes/PhotonScene.unity` (enabled **1**).
- **`m_Scenes[1]`** → `Assets/Scenes/MainScene.unity` (enabled **1**).

**Implementation note**: If Unity reorders GUIs differently, validate **`SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/PhotonScene.unity") == 0`**.

---

## Phase 2: NetworkRunner prefab + PhotonScene wiring

- [x]

### Overview

 **`NetworkRunner`** prefab **`+ NetworkSceneManagerDefault + NetworkObjectProviderDefault`**.

### Changes Required

- Implemented as **runtime setup** in `FusionDdolHub` (DontDestroy child **`NetworkRunner`** + **`NetworkSceneManagerDefault`** + **`NetworkObjectProviderDefault`**) instead of a prefab asset; **`FusionLobbyConnector`** is **auto-created** on **PhotonScene** (build index **0**).

---

## Phase 3: Lobby — Host / Join (`PlayerCount = 2`)

- [x]

### Overview

 **`StartGameArgs.PlayerCount = 2`** for **both** Host and Join; networked scene **MainScene only** (**index 1**).

### Implementation notes

- **Host**: `GameMode.Host`, **`PlayerCount = 2`**, **`IsVisible`** / **`IsOpen`** per matchmaking docs.
- **Join**: `GameMode.Client`, **`PlayerCount = 2`**, matchmaking-compatible **`SessionName` / `MatchmakingMode`**; **no sessions** → UI message.
- **Single `NetworkRunner`**: **`FindObjectsByType<NetworkRunner>`** / singleton pattern before spawning another.

---

## Phase 4: Disconnect, confirmations — back to PhotonScene

- [x]

### Overview

Fulfill locked rule: **sessions tear down cleanly** and return to **`PhotonScene`**, using **confirmation modals** for **manual leave**, **peer drop**, and **game over (mate/stalemate)** — **only when a Fusion session is active**.

### Changes Required

#### 1. `FusionSessionLifecycle.cs` (or sibling) + **`INetworkRunnerCallbacks`**

Register callbacks on **`NetworkRunner`** (`AddCallbacks`):

- **`OnShutdown(NetworkRunner runner, ShutdownReason reason)`**  
  - If shutdown was initiated **after user confirmed**: proceed with **`LoadScene` index **0****, **`Destroy(runner)`**, reset **`FusionLobbyConnector`** busy state.  
  - If shutdown is **unexpected** (e.g. **peer disconnected** / **Photon closed session**): **do not load scene immediately** — show **`ConfirmDialog`** with copy derived from **`ShutdownReason`** (**host**: “Other player disconnected…”, **guest**: analogous). **`Confirm`** → load **PhotonScene** + cleanup; **`Cancel`** → optional (see product rules).

- **`LeaveSessionConfirmed()`**: single entry called from **Confirmed** handlers only — **`runner.Shutdown()`** (no second dialog).

#### 2. **`MainScene` UI — manual quit**

- **Back to menu** / **Disconnect**: on click → **`ConfirmDialog`** (“Leave match and return to lobby?” Confirm / Cancel). **Confirm** → **`LeaveSessionConfirmed()`**.

#### 3. **`MainScene` — game over (offline rules, networked session)**

- **`ChessGameController`** **`RefreshStatus`** or **`GameRules.Evaluate`** path: when **`_result`** first becomes **`WhiteWinsCheckmate`**, **`BlackWinsCheckmate`**, or **`Stalemate`** **and** **`NetworkRunner` exists?.IsRunning** → show **one** **`ConfirmDialog`**: **Return to lobby?** (**Confirm** = **`LeaveSessionConfirmed()`**, **Cancel** = dismiss, user can press **Back to menu** later).
- Guard with **`bool gameOverDialogShown`** per match to avoid re-entrancy.

#### 4. **Peer disconnect (modal, not toast-only)**

- Per **§1**, replace **toast-only** UX with required **confirmation** before returning to lobby (or **immediate** Confirm single-button modal if **Cancel** is omitted).

#### 5. **`SimpleConfirmDialog` UI asset**

**File**: e.g. `Assets/Features/Multiplayer/UI/SimpleConfirmDialog.prefab`

**Behaviour**: **`SimpleConfirmDialogView`**/`Modal` script — **`Show(title, body, Action onConfirm, Action onCancel?, bool cancelVisible)`**, blocks interaction behind panel (**raycast** full-screen blocker).

**Implemented (code)**: `Assets/Features/Multiplayer/FusionDdolHub.cs` (runner hierarchy, `INetworkRunnerCallbacks`, `OnShutdown`, host/client `StartGame`), `SimpleConfirmDialog.cs` (runtime modal, no prefab), `MultiplayerGameplayHud.cs` (“Back to lobby”), and `ChessGameController` game-over prompt + `FusionDdolHub.ShutdownAfterUserConfirmed`. Session lifecycle is merged into `FusionDdolHub` (no separate `FusionSessionLifecycle.cs`).

---

## Phase 5: Manual integration checks

### Overview

Regression + multiplayer smoke.

### Checklist

- Build order verified (**Photon** = **0**).
- **Host** → **Join** → both **MainScene**, same session, **≤2** in room.
- **Back to menu** → **confirmation** → **PhotonScene**, **no orphaned** runner on second Host.
- **Mate/stalemate** in multiplayer session → **Return to lobby?** behaves as specified.
- Remote close app → surviving client sees **peer-disconnect confirmation** → **PhotonScene** on confirm.
- **Join** alone with no Host → UX error path.

---

## Research

- `Assets/Photon/Fusion/Runtime/FusionBootstrap.cs` — `InitializeNetworkRunner`, `StartGameArgs`.
- `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`.
- `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- `Assets/Features/Multiplayer/MultiplayerHostJoinPanel.prefab`.
- Scenes: `Assets/Scenes/PhotonScene.unity`, `Assets/Scenes/MainScene.unity`.
- Docs: [Matchmaking](https://doc.photonengine.com/en-us/fusion/current/manual/matchmaking), [Scene loading](https://doc.photonengine.com/en-us/fusion/current/manual/scene-loading), runner callbacks / shutdown in Fusion docs.

## Success Criteria

### Automated verification

- Compiles after Fusion references.
- Optional: assert **`PhotonScene`** build index **== 0** and **`MainScene`** **== 1** in EditMode smoke test.

### Manual verification

- **PhotonScene** boots as index **0** in **Build Settings**.
- Host + Join (2 peers) reach **MainScene**; **`PlayerCount`** **2**.
- Disconnect / confirmations return **PhotonScene**; repeatable matchmaking.
- **MainScene**: **solo** play (no runner) shows **no** multiplayer quit/mate dialogs from these flows.
