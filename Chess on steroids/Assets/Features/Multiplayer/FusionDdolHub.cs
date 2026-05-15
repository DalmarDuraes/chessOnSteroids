using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chess.Multiplayer
{
    /// <summary>
    /// DontDestroy parent for <see cref="NetworkRunner"/> (callbacks live on this object, not on the runner).
    /// </summary>
    public sealed class FusionDdolHub : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const int PlayerCountLocked = 2;

        static FusionDdolHub _instance;
        NetworkRunner _runner;

        bool _userRequestedShutdown;

        public static FusionDdolHub EnsureExists()
        {
            if (_instance != null)
                return _instance;

            var found = FindAnyObjectByType<FusionDdolHub>();
            if (found != null)
            {
                _instance = found;
                return _instance;
            }

            var go = new GameObject("[Chess Fusion Hub]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FusionDdolHub>();
            _instance.CreateRunnerHierarchy();
            return _instance;
        }

        public static FusionDdolHub Instance => _instance;

        public NetworkRunner Runner => _runner;

        public static bool IsRunnerActive =>
            FindAnyObjectByType<NetworkRunner>() is { IsRunning: true };

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void CreateRunnerHierarchy()
        {
            var runnerGo = new GameObject("NetworkRunner");
            runnerGo.transform.SetParent(transform, false);
            _runner = runnerGo.AddComponent<NetworkRunner>();
            runnerGo.AddComponent<NetworkSceneManagerDefault>();
            runnerGo.AddComponent<NetworkObjectProviderDefault>();
            _runner.AddCallbacks(this);
        }

        StartGameArgs BuildStartArgs(GameMode mode, string sessionName, string displayNameOrNull)
        {
            int gameplay = ChessFusionBuildIndices.GameplayScene;
            if (gameplay < 0)
                Debug.LogError($"[Fusion] Gameplay scene not in build settings: {ChessFusionBuildIndices.GameplayScenePath}");

            var sceneInfo = new NetworkSceneInfo();
            if (gameplay >= 0)
                sceneInfo.AddSceneRef(SceneRef.FromIndex(gameplay), LoadSceneMode.Additive);

            AuthenticationValues auth = TryBuildAuthValues(displayNameOrNull);

            var args = new StartGameArgs
            {
                GameMode = mode,
                Address = NetAddress.Any(),
                Scene = sceneInfo,
                SessionName = sessionName,
                PlayerCount = PlayerCountLocked,
                SceneManager = _runner.GetComponent<INetworkSceneManager>(),
                ObjectProvider = _runner.GetComponent<INetworkObjectProvider>(),
                IsVisible = true,
                IsOpen = true,
            };

            if (auth != null)
                args.AuthValues = auth;

            return args;
        }

        /// <summary>Photon user id carrying the chosen display name (unique per connect). NickName UI in Fusion is separate; this is what we have without realtime surface.</summary>
        static AuthenticationValues TryBuildAuthValues(string displayNameOrNull)
        {
            string trimmed = displayNameOrNull?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            string nick = SanitizeForUserIdPrefix(trimmed, maxLen: 20);
            if (nick.Length == 0)
                nick = "Player";

            string suffix = Guid.NewGuid().ToString("N")[..8];
            string userId = $"{nick}_{suffix}";
            const int maxUserIdLen = 48;
            if (userId.Length > maxUserIdLen)
                userId = userId.Substring(0, maxUserIdLen);

            return new AuthenticationValues(userId);
        }

        static string SanitizeForUserIdPrefix(string s, int maxLen)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c is '-' or '_')
                    sb.Append(c);
                if (sb.Length >= maxLen)
                    break;
            }

            return sb.ToString();
        }

        public Task StartHostAsync(string sessionName, string displayNameOrNull = null)
        {
            EnsureExists();
            if (_runner.IsRunning)
            {
                Debug.LogWarning("[Fusion] StartHost ignored: runner already running.");
                return Task.CompletedTask;
            }

            var args = BuildStartArgs(GameMode.Host, sessionName, displayNameOrNull);
            return _runner.StartGame(args);
        }

        public Task StartClientAsync(string sessionName, string displayNameOrNull = null)
        {
            EnsureExists();
            if (_runner.IsRunning)
            {
                Debug.LogWarning("[Fusion] StartClient ignored: runner already running.");
                return Task.CompletedTask;
            }

            var args = BuildStartArgs(GameMode.Client, sessionName, displayNameOrNull);
            return _runner.StartGame(args);
        }

        /// <summary>User already confirmed (e.g. game-over dialog).</summary>
        public static void ShutdownAfterUserConfirmed()
        {
            if (_instance == null)
                return;
            _instance._userRequestedShutdown = true;
            _instance._runner?.Shutdown();
        }

        public static void PromptLeaveMatch()
        {
            if (!IsRunnerActive)
                return;

            SimpleConfirmDialog.Show(
                "Leave match?",
                "Return to the lobby? This ends the session for you.",
                onConfirm: () =>
                {
                    if (_instance == null)
                        return;
                    _instance._userRequestedShutdown = true;
                    _instance._runner?.Shutdown();
                },
                onCancel: () => { },
                showCancel: true);
        }

        void LoadPhotonLobbyAndDestroyHub()
        {
            int idx = ChessFusionBuildIndices.PhotonScene;
            if (idx < 0)
                idx = 0;
            Destroy(gameObject);
            SceneManager.LoadScene(idx, LoadSceneMode.Single);
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_userRequestedShutdown)
            {
                _userRequestedShutdown = false;
                LoadPhotonLobbyAndDestroyHub();
                return;
            }

            string body = DescribeShutdown(shutdownReason);
            Destroy(gameObject);

            SimpleConfirmDialog.Show(
                "Session ended",
                body + " Return to the lobby?",
                onConfirm: () =>
                {
                    int idx = ChessFusionBuildIndices.PhotonScene;
                    if (idx < 0)
                        idx = 0;
                    SceneManager.LoadScene(idx, LoadSceneMode.Single);
                },
                onCancel: () => { },
                showCancel: true);
        }

        static string DescribeShutdown(ShutdownReason reason) =>
            reason switch
            {
                ShutdownReason.Ok => "The connection closed.",
                ShutdownReason.Error => "A network error occurred.",
                ShutdownReason.IncompatibleConfiguration => "Session configuration mismatch.",
                ShutdownReason.ServerInRoom => "Session is no longer available.",
                ShutdownReason.DisconnectedByPluginLogic => "Disconnected.",
                ShutdownReason.GameClosed => "The game was closed.",
                ShutdownReason.GameIsFull => "The session is full.",
                ShutdownReason.InvalidRegion => "Invalid region.",
                ShutdownReason.MaxCcuReached => "Server capacity reached.",
                ShutdownReason.InvalidAuthentication => "Authentication failed.",
                ShutdownReason.CustomAuthenticationFailed => "Authentication failed.",
                _ => "The session ended (" + reason + ").",
            };

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            System.ArraySegment<byte> data) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    }
}
