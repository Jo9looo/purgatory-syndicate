using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PurgatorySyndicate.Menu
{
    /// <summary>
    /// Builds the whitebox main menu at runtime (UGUI): title, Mulai, Keluar.
    /// Visuals are plain placeholders — swap the Image sprites with Aseprite art later
    /// without touching the logic.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string BattleSceneName = "SampleScene";

        private Font uiFont;

        private void Start()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureEventSystem();
            BuildMenu();
        }

        /// <summary>
        /// Project uses the new Input System, so the EventSystem needs
        /// InputSystemUIInputModule (NOT the legacy StandaloneInputModule).
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildMenu()
        {
            // Root canvas, scales with resolution.
            GameObject canvasObject = new GameObject("MenuCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            // Dark backdrop.
            Image backdrop = CreateImage(canvas.transform, "Backdrop", new Color(0.06f, 0.05f, 0.09f));
            StretchFull(backdrop.rectTransform);

            // Title + subtitle.
            CreateText(canvas.transform, "Title", "PURGATORY SYNDICATE",
                64, FontStyle.Bold, new Color(1f, 0.92f, 0.6f),
                new Vector2(0f, 220f), new Vector2(1200f, 90f));

            CreateText(canvas.transform, "Subtitle", "Tactical Exorcism — Prototype Whitebox",
                24, FontStyle.Italic, new Color(0.7f, 0.7f, 0.8f),
                new Vector2(0f, 150f), new Vector2(900f, 40f));

            // Buttons.
            CreateButton(canvas.transform, "MULAI", new Vector2(0f, 10f), StartBattle);
            CreateButton(canvas.transform, "KELUAR", new Vector2(0f, -90f), QuitGame);

            // Footer.
            CreateText(canvas.transform, "Footer", "Whitebox build — UI placeholder, art menyusul",
                16, FontStyle.Normal, new Color(0.45f, 0.45f, 0.5f),
                new Vector2(0f, -480f), new Vector2(800f, 30f));
        }

        // ---------------------------------------------------------------- Actions

        private void StartBattle()
        {
            PurgatorySyndicate.Battle.BattleProgression.Reset();
            SceneManager.LoadScene(BattleSceneName);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- UI builders

        private void CreateButton(Transform parent, string label, Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject($"Button_{label}");
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.15f, 0.22f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.28f, 0.42f);
            colors.pressedColor = new Color(0.45f, 0.4f, 0.2f);
            button.colors = colors;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 72f);

            Text text = CreateText(buttonObject.transform, "Label", label,
                28, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
            StretchFull(text.rectTransform);
        }

        private Text CreateText(Transform parent, string objectName, string content,
            int fontSize, FontStyle style, Color color,
            Vector2 anchoredPosition, Vector2 size)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

            RectTransform rect = text.rectTransform;
            rect.anchoredPosition = anchoredPosition;
            if (size != Vector2.zero)
            {
                rect.sizeDelta = size;
            }

            return text;
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            GameObject imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
