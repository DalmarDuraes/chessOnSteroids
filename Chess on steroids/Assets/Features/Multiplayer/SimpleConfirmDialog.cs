using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chess.Multiplayer
{
    /// <summary>
    /// Minimal blocking modal built at runtime under a canvas (solo play unaffected unless Show is called).
    /// </summary>
    public sealed class SimpleConfirmDialog : MonoBehaviour
    {
        static SimpleConfirmDialog _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public static void Show(
            string title,
            string body,
            Action onConfirm,
            Action onCancel = null,
            bool showCancel = true)
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }

            Canvas canvas = FindAnyCanvas() ?? CreateOverlayCanvas();

            var go = new GameObject(nameof(SimpleConfirmDialog));
            go.transform.SetParent(canvas.transform, false);

            _instance = go.AddComponent<SimpleConfirmDialog>();
            _instance.BuildModal(title, body, onConfirm, onCancel, showCancel);
        }

        static Canvas FindAnyCanvas() => FindAnyObjectByType<Canvas>();

        static Canvas CreateOverlayCanvas()
        {
            var cgo = new GameObject("SimpleConfirmOverlayCanvas");
            var c = cgo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 5000;
            cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cgo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(cgo);
            return c;
        }

        void BuildModal(string title, string body, Action onConfirm, Action onCancel, bool showCancel)
        {
            transform.SetAsLastSibling();

            var rootRt = gameObject.AddComponent<RectTransform>();
            StretchFull(rootRt);

            var blocker = gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.55f);
            blocker.raycastTarget = true;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(420f, 220f);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;

            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var v = panel.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 24, 24);
            v.spacing = 12f;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            MkText(panel.transform, title, 22f, FontStyles.Bold);
            MkText(panel.transform, body, 16f, FontStyles.Normal);

            var row = new GameObject("Buttons");
            row.transform.SetParent(panel.transform, false);
            var rowHl = row.AddComponent<HorizontalLayoutGroup>();
            rowHl.spacing = 12f;
            rowHl.childAlignment = TextAnchor.MiddleCenter;
            rowHl.childForceExpandWidth = true;
            rowHl.childForceExpandHeight = false;

            MkButton(row.transform, "Confirm", () =>
            {
                onConfirm?.Invoke();
                Destroy(gameObject);
                if (_instance == this)
                    _instance = null;
            });

            if (showCancel)
            {
                MkButton(row.transform, "Cancel", () =>
                {
                    onCancel?.Invoke();
                    Destroy(gameObject);
                    if (_instance == this)
                        _instance = null;
                });
            }
        }

        static void MkText(Transform parent, string value, float size, FontStyles style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var flex = go.AddComponent<LayoutElement>();
            flex.preferredHeight = style == FontStyles.Bold ? 36f : 80f;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.text = value;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        static void MkButton(Transform parent, string caption, Action onPress)
        {
            var go = new GameObject(caption + "Button");
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.85f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var lie = go.AddComponent<LayoutElement>();
            lie.minHeight = 40f;
            lie.flexibleWidth = 1f;

            btn.onClick.AddListener(() => onPress?.Invoke());

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            StretchFull(lblRt);

            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = caption;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
