using System;
using ChokeHold.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChokeHold.UI
{
    /// <summary>
    /// Meniul de setari (Esc): sensibilitatea mouse-ului (aceeasi scara ca in shooterele clasice, deci se poate
    /// copia valoarea din alt joc), sensibilitatea cu luneta si saritura pe rotita. Se salveaza intre sesiuni.
    /// Isi construieste singur Canvas-ul la runtime.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private static readonly Color Panel = new Color(0.05f, 0.06f, 0.07f, 0.94f);
        private static readonly Color Button = new Color(0.2f, 0.22f, 0.25f, 1f);
        private static readonly Color Accent = new Color(1f, 0.84f, 0.35f);

        private GameObject panel;
        private TMP_Text sensitivityText;
        private TMP_Text zoomText;
        private TMP_Text scrollText;
        private bool open;
        private bool blockedLastFrame;

        public bool IsOpen => open;

        private void Start()
        {
            Build();
            Refresh();
        }

        private void OnDestroy()
        {
            if (open)
            {
                PlayerInputGate.Request(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            // Esc deschide setarile doar daca niciun alt meniu nu era deschis (Esc inchide si meniul de cumparare)
            if (Keyboard.current.escapeKey.wasPressedThisFrame && (open || !blockedLastFrame))
            {
                SetOpen(!open);
            }
        }

        private void LateUpdate()
        {
            blockedLastFrame = PlayerInputGate.Blocked;
        }

        private void SetOpen(bool value)
        {
            if (open == value)
            {
                return;
            }

            open = value;
            panel.SetActive(open);
            PlayerInputGate.Request(open);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (!open)
            {
                GameSettings.Save();
            }
            Refresh();
        }

        private void Refresh()
        {
            if (sensitivityText == null)
            {
                return;
            }

            float sensitivity = GameSettings.Sensitivity;
            // cm pentru o rotire completa la 800 DPI: ajuta la comparat cu alte jocuri
            float cmPer360 = 360f / (sensitivity * 0.022f) / 800f * 2.54f;
            sensitivityText.text = $"{sensitivity:0.00}   <size=70%><color=#AAAAAA>{cmPer360:0} cm / 360° la 800 DPI</color></size>";
            zoomText.text = $"{GameSettings.ZoomSensitivityRatio:0.00}";
            scrollText.text = GameSettings.ScrollWheelJump ? "DA (armele: 1 / 2 / 3)" : "NU (rotita schimba armele)";
        }

        private static float Step()
        {
            // Shift = pas mare, Ctrl = pas fin
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) return 0.5f;
            if (Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed) return 0.01f;
            return 0.05f;
        }

        // ------------------------------------------------------------------
        // Constructie UI
        // ------------------------------------------------------------------

        private void Build()
        {
            GameObject canvasObject = new GameObject("Settings Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 520f);
            panel.AddComponent<Image>().color = Panel;

            TMP_Text title = Text(panelRect, "Title", new Vector2(0f, 205f), new Vector2(900f, 60f), 44f, TextAlignmentOptions.Center);
            title.text = "SETARI";

            sensitivityText = Row(panelRect, "Sensibilitate mouse", 110f,
                () => GameSettings.Sensitivity -= Step(), () => GameSettings.Sensitivity += Step());
            zoomText = Row(panelRect, "Sensibilitate cu luneta (raport)", 20f,
                () => GameSettings.ZoomSensitivityRatio -= Step(), () => GameSettings.ZoomSensitivityRatio += Step());

            TMP_Text scrollLabel = Text(panelRect, "ScrollLabel", new Vector2(-300f, -70f), new Vector2(500f, 50f), 26f, TextAlignmentOptions.Left);
            scrollLabel.text = "Saritura pe rotita mouse-ului";
            scrollText = MakeButton(panelRect, "ScrollToggle", new Vector2(250f, -70f), new Vector2(560f, 56f), () =>
            {
                GameSettings.ScrollWheelJump = !GameSettings.ScrollWheelJump;
            });

            TMP_Text hint = Text(panelRect, "Hint", new Vector2(0f, -150f), new Vector2(1100f, 40f), 20f, TextAlignmentOptions.Center);
            hint.color = new Color(0.7f, 0.72f, 0.75f);
            hint.text = "Pas: click 0,05  ·  Shift + click 0,5  ·  Ctrl + click 0,01  ·  scara clasica: 0,022° pe count";

            MakeButton(panelRect, "Close", new Vector2(0f, -215f), new Vector2(300f, 56f), () => SetOpen(false)).text = "Inapoi  [Esc]";

            panel.SetActive(false);
        }

        private TMP_Text Row(RectTransform parent, string label, float y, Action minus, Action plus)
        {
            TMP_Text name = Text(parent, label, new Vector2(-300f, y), new Vector2(500f, 50f), 26f, TextAlignmentOptions.Left);
            name.text = label;
            MakeButton(parent, label + " -", new Vector2(0f, y), new Vector2(60f, 56f), minus).text = "-";
            TMP_Text value = Text(parent, label + " Value", new Vector2(250f, y), new Vector2(420f, 50f), 30f, TextAlignmentOptions.Center);
            value.color = Accent;
            MakeButton(parent, label + " +", new Vector2(500f, y), new Vector2(60f, 56f), plus).text = "+";
            return value;
        }

        private TMP_Text MakeButton(RectTransform parent, string name, Vector2 position, Vector2 size, Action onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.AddComponent<Image>();
            image.color = Button;
            UnityEngine.UI.Button button = go.AddComponent<UnityEngine.UI.Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f);
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                onClick();
                Refresh();
            });

            TMP_Text text = Text(rect, "Label", Vector2.zero, size, 26f, TextAlignmentOptions.Center);
            return text;
        }

        private static TMP_Text Text(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Bold;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
