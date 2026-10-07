using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Subject143.Nightfarer
{
    /// <summary>Title screen: start the game, open the combat arena, view controls, quit.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        public string gameScene = "SampleScene";
        public string arenaScene = "NightfarerTestScene";
        public Button startButton, arenaButton, controlsButton, quitButton, controlsBack;
        public GameObject menuPanel, controlsPanel;
        public Text status;

        void Awake()
        {
            Time.timeScale = 1f;
            DeviceInputSource.CursorReleased = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (startButton != null) startButton.onClick.AddListener(() => Load(gameScene));
            if (arenaButton != null) arenaButton.onClick.AddListener(() => Load(arenaScene));
            if (controlsButton != null) controlsButton.onClick.AddListener(() => Show(true));
            if (controlsBack != null) controlsBack.onClick.AddListener(() => Show(false));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            Show(false);
        }

        void Start() => NightfarerUI.EnsureEventSystem();

        void Show(bool controls)
        {
            if (menuPanel != null) menuPanel.SetActive(!controls);
            if (controlsPanel != null) controlsPanel.SetActive(controls);
            var first = controls ? controlsBack : startButton;
            if (first != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Load(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                if (status != null) status.text = $"Scene '{scene}' is not in Build Settings";
                return;
            }
            DeviceInputSource.CursorReleased = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(scene);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Builds the title screen canvas (used by the editor builder).</summary>
        public static MainMenuUI Build(Transform parent, UISpriteSet sprites, Font font)
        {
            var root = new GameObject("MainMenuUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var menu = root.AddComponent<MainMenuUI>();

            // Reuse the in-game factory's look by building a temporary HUD-less layout here.
            Color gold = new Color(0.93f, 0.82f, 0.56f);
            Text T(Transform p, string s, int size, TextAnchor a, Color c, FontStyle st, Vector2 anchor, Vector2 pos, Vector2 sz)
            {
                var go = new GameObject(s.Length > 20 ? "Text" : s, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(p, false);
                var t = go.GetComponent<Text>();
                t.font = font; t.text = s; t.fontSize = size; t.alignment = a; t.color = c; t.fontStyle = st; t.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = sz;
                return t;
            }
            Button B(Transform p, string label, Vector2 pos)
            {
                var go = new GameObject(label + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(p, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(420, 64);
                go.GetComponent<Image>().sprite = sprites.white;
                var b = go.GetComponent<Button>();
                var cb = b.colors;
                cb.normalColor = new Color(0.06f, 0.06f, 0.08f, 0.75f);
                cb.highlightedColor = new Color(0.36f, 0.29f, 0.16f, 0.95f);
                cb.selectedColor = cb.highlightedColor;
                cb.pressedColor = new Color(0.55f, 0.44f, 0.22f, 1f);
                b.colors = cb;
                go.AddComponent<Outline>().effectColor = new Color(0.78f, 0.66f, 0.42f, 0.5f);
                var t = T(go.transform, label.ToUpperInvariant(), 28, TextAnchor.MiddleLeft, gold, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(20, 0), new Vector2(380, 64));
                return b;
            }

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            var srt = (RectTransform)shade.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = new Vector2(0.5f, 1f); srt.offsetMin = srt.offsetMax = Vector2.zero;
            var simg = shade.GetComponent<Image>();
            simg.sprite = sprites.gradient;
            simg.color = new Color(0f, 0f, 0f, 0.65f);
            simg.raycastTarget = false;

            var title = T(root.transform, "SUBJECT 143", 110, TextAnchor.MiddleLeft, gold, FontStyle.Bold, new Vector2(0, 1), new Vector2(560, -230), new Vector2(1000, 140));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            T(root.transform, "N I G H T F A R E R   P R O T O T Y P E", 26, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.82f), FontStyle.Normal, new Vector2(0, 1), new Vector2(560, -320), new Vector2(1000, 40));

            var panel = new GameObject("Menu", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f); prt.pivot = new Vector2(0, 0.5f); prt.anchoredPosition = new Vector2(120, -80); prt.sizeDelta = new Vector2(440, 400);
            menu.menuPanel = panel;
            menu.startButton = B(panel.transform, "Start", new Vector2(0, 120));
            menu.arenaButton = B(panel.transform, "Combat Arena", new Vector2(0, 40));
            menu.controlsButton = B(panel.transform, "Controls", new Vector2(0, -40));
            menu.quitButton = B(panel.transform, "Quit", new Vector2(0, -120));

            var controls = new GameObject("Controls", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            controls.transform.SetParent(root.transform, false);
            var crt = (RectTransform)controls.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(1000, 660);
            var cimg = controls.GetComponent<Image>();
            cimg.sprite = sprites.white;
            cimg.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
            var ct = T(controls.transform, NightfarerUIFactory.ControlsText, 22, TextAnchor.UpperLeft, Color.white, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(920, 560));
            ct.supportRichText = true;
            menu.controlsPanel = controls;
            menu.controlsBack = B(controls.transform, "Back", new Vector2(290, -280));
            var backRt = (RectTransform)menu.controlsBack.transform;
            backRt.sizeDelta = new Vector2(420, 56);

            menu.status = T(root.transform, "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.5f, 0.4f), FontStyle.Normal, new Vector2(0, 0), new Vector2(560, 60), new Vector2(1000, 40));
            return menu;
        }
    }
}
