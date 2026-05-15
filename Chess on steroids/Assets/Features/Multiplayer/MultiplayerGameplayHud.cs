using UnityEngine;
using UnityEngine.UI;

namespace Chess.Multiplayer
{
    /// <summary>
    /// Adds a "Back to lobby" control when a Fusion session is active so players can leave from MainScene.
    /// </summary>
    public static class MultiplayerGameplayHud
    {
        const string BackButtonName = "BackToLobby_Multiplayer";

        public static void EnsureBackToLobbyButton(MonoBehaviour hostBehaviour)
        {
            if (!FusionDdolHub.IsRunnerActive || hostBehaviour == null)
                return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null || canvas.transform.Find(BackButtonName) != null)
                return;

            var go = new GameObject(BackButtonName);
            go.transform.SetParent(canvas.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(200f, 44f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => FusionDdolHub.PromptLeaveMatch());

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 15f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "Back to lobby";
        }
    }
}
