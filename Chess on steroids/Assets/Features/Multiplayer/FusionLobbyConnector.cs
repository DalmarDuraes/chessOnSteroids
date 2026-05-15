using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chess.Multiplayer
{
    /// <summary>
    /// Binds lobby UI to Fusion host/join on <b>PhotonScene</b>.
    /// Assign Host / Join buttons, room + player TMP inputs, and status <see cref="TMP_Text"/> in the Inspector — no runtime discovery.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FusionLobbyConnector : MonoBehaviour
    {
        [Header("Lobby UI")]
        [Tooltip("Drag the Photon Host button here.")]
        [SerializeField] Button _hostButton;
        [Tooltip("Drag the Photon Join button here.")]
        [SerializeField] Button _joinButton;
        [Tooltip("TMP session / room name (required — assign in Inspector).")]
        [SerializeField] TMP_InputField _roomNameField;
        [SerializeField] TMP_InputField _playerNameField;
        [SerializeField] TMP_Text _statusLabel;

        [SerializeField] string _defaultSessionName = "DevChess2P";

        bool _busy;

        void Start()
        {
            if (_hostButton == null || _joinButton == null ||
                _roomNameField == null || _playerNameField == null || _statusLabel == null)
            {
                Debug.LogError(
                    "[Fusion Lobby] Assign Host Button, Join Button, Room TMP input, Player TMP input, and Status TMP text on FusionLobbyConnector (Inspector).",
                    this);
                return;
            }

            _hostButton.onClick.AddListener(() => StartCoroutine(CoTryHost()));
            _joinButton.onClick.AddListener(() => StartCoroutine(CoTryJoin()));
            SetInteractable(true);
            string roomPreview = ReadRoomNameForStatus();
            SetStatus($"Room: {roomPreview} — enter display name (optional), then Host or Join.");
        }

        IEnumerator CoTryHost()
        {
            if (_busy) yield break;

            FusionDdolHub.EnsureExists();
            if (FusionDdolHub.IsRunnerActive)
            {
                SetStatus("Already connected.");
                yield break;
            }

            string room = NormalizeRoomName(out string roomErr);
            if (room == null)
            {
                SetStatus(roomErr ?? "Room name invalid.");
                yield break;
            }

            string displayName = ReadDisplayNameOptional();

            SetInteractable(false);
            _busy = true;
            SetStatus("Starting host…");

            Task launch = FusionDdolHub.Instance.StartHostAsync(room, displayName);
            yield return new WaitUntil(() => launch.IsCompleted);

            _busy = false;
            SetInteractable(true);

            if (launch.IsFaulted)
            {
                SetStatus(describeFault(launch));
                yield break;
            }

            if (!FusionDdolHub.Instance || !FusionDdolHub.Instance.Runner.IsRunning)
                SetStatus("Host failed. Check Photon App Id Fusion and Fusion cloud reachability.");
            else
                SetStatus("Hosting — loading match…");
        }

        IEnumerator CoTryJoin()
        {
            if (_busy) yield break;

            FusionDdolHub.EnsureExists();
            if (FusionDdolHub.IsRunnerActive)
            {
                SetStatus("Already connected.");
                yield break;
            }

            string room = NormalizeRoomName(out string roomErr);
            if (room == null)
            {
                SetStatus(roomErr ?? "Room name invalid.");
                yield break;
            }

            string displayName = ReadDisplayNameOptional();

            SetInteractable(false);
            _busy = true;
            SetStatus("Joining…");

            Task launch = FusionDdolHub.Instance.StartClientAsync(room, displayName);
            yield return new WaitUntil(() => launch.IsCompleted);

            _busy = false;
            SetInteractable(true);

            if (launch.IsFaulted)
            {
                SetStatus(describeFault(launch));
                yield break;
            }

            if (!FusionDdolHub.Instance || !FusionDdolHub.Instance.Runner.IsRunning)
                SetStatus("Join failed (no host or room name mismatch).");
            else
                SetStatus("Joined — loading match…");
        }

        static string describeFault(Task t) =>
            t.Exception != null ? t.Exception.GetBaseException().Message : "Unknown error.";

        string NormalizeRoomName(out string error)
        {
            error = null;
            string s = _roomNameField.text?.Trim() ?? string.Empty;
            if (s.Length == 0)
            {
                error = "Enter a room name.";
                return null;
            }

            return s;
        }

        string ReadDisplayNameOptional()
        {
            string s = _playerNameField.text?.Trim() ?? string.Empty;
            return s.Length == 0 ? null : s;
        }

        string ReadRoomNameForStatus()
        {
            if (!string.IsNullOrWhiteSpace(_roomNameField.text))
                return _roomNameField.text.Trim();
            return string.IsNullOrWhiteSpace(_defaultSessionName) ? "(set room)" : _defaultSessionName.Trim();
        }

        void SetInteractable(bool v)
        {
            _hostButton.interactable = v;
            _joinButton.interactable = v;
            _roomNameField.interactable = v;
            _playerNameField.interactable = v;
        }

        void SetStatus(string message) =>
            _statusLabel.text = message;
    }
}
