using UnityEngine;
using UnityEngine.UI;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Builds the Nightreign-style HUD and pause menu hierarchy (uGUI, 1920x1080 reference). The editor builder
    /// saves the result as a prefab you can restyle freely; NightfarerUI only depends on the references set here.
    /// </summary>
    public static class NightfarerUIFactory
    {
        static readonly Color Gold = new Color(0.93f, 0.82f, 0.56f);
        static readonly Color Panel = new Color(0.07f, 0.07f, 0.09f, 0.86f);
        static readonly Color Frame = new Color(0.78f, 0.66f, 0.42f, 0.55f);

        static Font font;
        static UISpriteSet sprites;

        public static NightfarerUI Build(Transform parent, UISpriteSet spriteSet, Font uiFont)
        {
            sprites = spriteSet;
            font = uiFont;

            var root = new GameObject("NightfarerUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var ui = root.AddComponent<NightfarerUI>();
            ui.canvas = canvas;

            // ---------------- HUD
            var hud = Rect("HUD", root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ui.hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            ui.hudGroup.blocksRaycasts = false;

            ui.vignette = Img("Damage Vignette", hud, sprites.vignette, new Color(0.6f, 0f, 0f, 0f));
            Stretch(ui.vignette.rectTransform);

            var vitals = Rect("Vitals", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -40), new Vector2(620, 110));
            ui.nameText = Txt("Name", vitals, "NIGHTFARER", 26, TextAnchor.UpperLeft, Gold, FontStyle.Bold);
            Place(ui.nameText.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(600, 34));
            Bar(vitals, "HP", new Vector2(0, -40), new Vector2(560, 20), new Color(0.72f, 0.12f, 0.1f), out ui.hpFill, out ui.hpTrail);
            Bar(vitals, "Stamina", new Vector2(0, -70), new Vector2(400, 12), new Color(0.3f, 0.66f, 0.28f), out ui.staminaFill, out _);
            ui.surgeText = Txt("Surge", vitals, "SURGE", 16, TextAnchor.MiddleLeft, new Color(0.55f, 0.85f, 1f), FontStyle.Bold);
            Place(ui.surgeText.rectTransform, new Vector2(0, 1), new Vector2(412, -66), new Vector2(120, 20));

            // Skill slot (bottom-left).
            var skill = Slot(hud, "Skill", new Vector2(0, 0), new Vector2(56, 64), 96, "SKILL", "E");
            ui.skillCooldown = Img("Cooldown", skill, sprites.white, new Color(0f, 0f, 0f, 0.72f));
            Stretch(ui.skillCooldown.rectTransform);
            ui.skillCooldown.type = Image.Type.Filled;
            ui.skillCooldown.fillMethod = Image.FillMethod.Radial360;
            ui.skillCooldown.fillOrigin = (int)Image.Origin360.Top;
            ui.skillCooldown.fillClockwise = false;
            ui.skillCooldownText = Txt("Seconds", skill, "", 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(ui.skillCooldownText.rectTransform);
            ui.skillName = Txt("Skill Name", hud, "Claw Shot", 16, TextAnchor.UpperCenter, Gold, FontStyle.Normal);
            Place(ui.skillName.rectTransform, new Vector2(0, 0), new Vector2(56 - 22, 58), new Vector2(140, 22), new Vector2(0, 1));

            // Ultimate (bottom-left, round).
            var ult = Rect("Ultimate", hud, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(176, 56), new Vector2(112, 112));
            var ultBg = Img("Back", ult, sprites.circle, new Color(0.05f, 0.05f, 0.06f, 0.9f));
            Stretch(ultBg.rectTransform);
            ui.ultimateFill = Img("Gauge", ult, sprites.circle, new Color(0.95f, 0.66f, 0.22f));
            Stretch(ui.ultimateFill.rectTransform);
            ui.ultimateFill.type = Image.Type.Filled;
            ui.ultimateFill.fillMethod = Image.FillMethod.Radial360;
            ui.ultimateFill.fillOrigin = (int)Image.Origin360.Bottom;
            var ultInner = Img("Inner", ult, sprites.circle, new Color(0.08f, 0.07f, 0.07f, 0.95f));
            Place(ultInner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84), new Vector2(0.5f, 0.5f));
            ui.ultimateLabel = Txt("Label", ult, "ULT", 22, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Stretch(ui.ultimateLabel.rectTransform);
            ui.ultimateGlow = Img("Ready Glow", ult, sprites.ring, new Color(1f, 0.8f, 0.4f, 0f));
            Place(ui.ultimateGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(138, 138), new Vector2(0.5f, 0.5f));
            KeyHint(ult, "G");

            // Equipment (bottom-right).
            var weapon = Slot(hud, "Weapon", new Vector2(1, 0), new Vector2(-56, 64), 120, "", "X");
            ui.weaponName = Txt("Weapon Name", weapon, "Greatsword", 17, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Place(ui.weaponName.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108, 108), new Vector2(0.5f, 0.5f));
            var flask = Slot(hud, "Flask", new Vector2(1, 0), new Vector2(-200, 64), 96, "", "R");
            ui.flaskIcon = Img("Flask Icon", flask, sprites.circle, new Color(0.75f, 0.12f, 0.12f));
            Place(ui.flaskIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(46, 56), new Vector2(0.5f, 0.5f));
            ui.flaskCount = Txt("Count", flask, "3", 28, TextAnchor.LowerRight, Color.white, FontStyle.Bold);
            Place(ui.flaskCount.rectTransform, new Vector2(1, 0), new Vector2(-8, 4), new Vector2(60, 36), new Vector2(1, 0));
            var flaskLabel = Txt("Label", hud, "Crimson Tears", 16, TextAnchor.UpperCenter, Gold, FontStyle.Normal);
            Place(flaskLabel.rectTransform, new Vector2(1, 0), new Vector2(-200 + 22, 58), new Vector2(140, 22), new Vector2(1, 1));

            // Lock-on reticle.
            ui.reticle = Rect("Lock-on Reticle", hud, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var ring = Img("Ring", ui.reticle, sprites.ring, new Color(1f, 1f, 1f, 0.9f));
            Stretch(ring.rectTransform);
            var dot = Img("Dot", ui.reticle, sprites.circle, Color.white);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8), new Vector2(0.5f, 0.5f));

            // Target (boss-style) bar.
            var target = Rect("Target Bar", hud, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(900, 52));
            ui.targetBar = target.gameObject;
            ui.targetName = Txt("Name", target, "Target", 22, TextAnchor.LowerLeft, Color.white, FontStyle.Normal);
            Place(ui.targetName.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(900, 30));
            Bar(target, "Target HP", new Vector2(0, -34), new Vector2(900, 14), new Color(0.62f, 0.1f, 0.08f), out ui.targetFill, out ui.targetTrail);

            ui.notification = Txt("Notification", hud, "", 34, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Place(ui.notification.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1200, 60), new Vector2(0.5f, 0.5f));
            ui.notification.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

            // ---------------- Pause menu
            var pause = Rect("Pause Menu", root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ui.pauseRoot = pause.gameObject;
            var dim = Img("Dim", pause, sprites.white, new Color(0f, 0f, 0f, 0.74f));
            Stretch(dim.rectTransform);
            var title = Txt("Title", pause, "PAUSED", 64, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 90), new Vector2(0.5f, 0.5f));

            var main = Rect("Main", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(440, 520));
            ui.mainPanel = main.gameObject;
            string[] labels = { "Resume", "Settings", "Controls", "Restart", "Main Menu", "Quit" };
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                buttons[i] = MenuButton(main, labels[i], new Vector2(0, 200 - i * 80), new Vector2(420, 62));
            ui.resumeButton = buttons[0]; ui.settingsButton = buttons[1]; ui.controlsButton = buttons[2];
            ui.restartButton = buttons[3]; ui.mainMenuButton = buttons[4]; ui.quitButton = buttons[5];

            var settings = Rect("Settings", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(820, 600));
            ui.settingsPanel = settings.gameObject;
            var sBg = Img("Panel", settings, sprites.white, Panel);
            Stretch(sBg.rectTransform);
            ui.mouseSensitivity = SliderRow(settings, "Mouse sensitivity", 0, 0.02f, 0.4f);
            ui.padSensitivity = SliderRow(settings, "Gamepad sensitivity", 1, 60f, 400f);
            ui.fieldOfView = SliderRow(settings, "Field of view", 2, 45f, 80f);
            ui.invertY = ToggleRow(settings, "Invert camera Y", 3);
            ui.animationSetButton = CycleRow(settings, "Animation set", 4, out ui.animationSetLabel);
            ui.profileButton = CycleRow(settings, "Movement feel", 5, out ui.profileLabel);
            ui.debugOverlay = ToggleRow(settings, "Debug overlay (F1)", 6);
            ui.settingsBack = MenuButton(settings, "Back", new Vector2(0, -250), new Vector2(300, 56));

            var controls = Rect("Controls", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(980, 640));
            ui.controlsPanel = controls.gameObject;
            var cBg = Img("Panel", controls, sprites.white, Panel);
            Stretch(cBg.rectTransform);
            var cText = Txt("Text", controls, ControlsText, 22, TextAnchor.UpperLeft, Color.white, FontStyle.Normal);
            Place(cText.rectTransform, new Vector2(0, 1), new Vector2(40, -30), new Vector2(900, 520));
            cText.supportRichText = true;
            ui.controlsBack = MenuButton(controls, "Back", new Vector2(0, -270), new Vector2(300, 56));

            pause.gameObject.SetActive(false);
            return ui;
        }

        public const string ControlsText =
            "<b><color=#EED18F>ACTION                         KEYBOARD / MOUSE        GAMEPAD</color></b>\n" +
            "Move / camera                  WASD / Mouse                LS / RS\n" +
            "Dodge (tap) / Sprint (hold)    Space                       B\n" +
            "Surge sprint (while moving)    Left Alt                    L3\n" +
            "Jump / climb ledge             F                           A\n" +
            "Light attack                   Left mouse                  RB\n" +
            "Heavy attack (hold to charge)  Right mouse                 RT\n" +
            "Lock on / switch target        Q or Middle mouse / flick   R3 / flick RS\n" +
            "Character skill                E                           LT\n" +
            "Ultimate Art                   G                           Y\n" +
            "Crimson Tears flask            R                           X\n" +
            "Switch weapon                  X                           D-pad right\n" +
            "Walk toggle                    Left Ctrl                   D-pad up\n" +
            "Pause                          Esc                         Start\n\n" +
            "Spirit springs: stand in the light and press Jump. High falls end in a roll; there is no fall damage.";

        // ------------------------------------------------------------------ building blocks

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(string name, Transform parent, string text, int size, TextAnchor anchor, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color color, out Image fill, out Image trail)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var frame = Img("Frame", rt, sprites.white, Frame);
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(-2, -2);
            frame.rectTransform.offsetMax = new Vector2(2, 2);
            var bg = Img("Back", rt, sprites.white, new Color(0.04f, 0.04f, 0.05f, 0.9f));
            Stretch(bg.rectTransform);
            trail = Img("Trail", rt, sprites.white, new Color(1f, 0.92f, 0.75f, 0.85f));
            Stretch(trail.rectTransform);
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            fill = Img("Fill", rt, sprites.gradient, color);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
        }

        static RectTransform Slot(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, string label, string key)
        {
            var rt = Rect(name, parent, anchor, anchor, anchor, pos, new Vector2(size, size));
            var frame = Img("Frame", rt, sprites.white, Frame);
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(-3, -3);
            frame.rectTransform.offsetMax = new Vector2(3, 3);
            var bg = Img("Back", rt, sprites.white, Panel);
            Stretch(bg.rectTransform);
            if (!string.IsNullOrEmpty(label))
            {
                var l = Txt("Icon", rt, label, 20, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
                Stretch(l.rectTransform);
            }
            KeyHint(rt, key);
            return rt;
        }

        static void KeyHint(Transform parent, string key)
        {
            var bg = Img("Key", parent, sprites.white, new Color(0f, 0f, 0f, 0.8f));
            Place(bg.rectTransform, new Vector2(0, 1), new Vector2(-6, 6), new Vector2(26, 24));
            var t = Txt("Label", bg.transform, key, 15, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(t.rectTransform);
        }

        static Button MenuButton(Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var rt = Rect(label + " Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.white;
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = new Color(0.11f, 0.11f, 0.13f, 0.92f);
            colors.highlightedColor = new Color(0.36f, 0.29f, 0.16f, 0.95f);
            colors.selectedColor = new Color(0.36f, 0.29f, 0.16f, 0.95f);
            colors.pressedColor = new Color(0.55f, 0.44f, 0.22f, 1f);
            b.colors = colors;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Frame;
            var t = Txt("Text", rt, label.ToUpperInvariant(), 26, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Stretch(t.rectTransform);
            return b;
        }

        static RectTransform Row(Transform parent, string label, int index)
        {
            var row = Rect(label, parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40 - index * 66), new Vector2(740, 52));
            var t = Txt("Label", row, label, 24, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal);
            Place(t.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(340, 52), new Vector2(0, 0.5f));
            return row;
        }

        static Slider SliderRow(Transform parent, string label, int index, float min, float max)
        {
            var row = Row(parent, label, index);
            var area = Rect("Slider", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(360, 22));
            var bg = Img("Background", area, sprites.white, new Color(0.2f, 0.2f, 0.22f, 1f));
            Stretch(bg.rectTransform);
            var fillArea = Rect("Fill Area", area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(fillArea);
            var fill = Img("Fill", fillArea, sprites.white, new Color(0.78f, 0.62f, 0.3f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("Handle Slide Area", area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            var handle = Img("Handle", handleArea, sprites.circle, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(30, 30);
            var s = area.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            bg.raycastTarget = true;
            return s;
        }

        static Toggle ToggleRow(Transform parent, string label, int index)
        {
            var row = Row(parent, label, index);
            var box = Rect("Toggle", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(40, 40));
            var bg = Img("Background", box, sprites.white, new Color(0.2f, 0.2f, 0.22f, 1f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var check = Img("Checkmark", box, sprites.white, new Color(0.9f, 0.74f, 0.38f));
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24), new Vector2(0.5f, 0.5f));
            var t = box.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = check;
            return t;
        }

        static Button CycleRow(Transform parent, string label, int index, out Text value)
        {
            var row = Row(parent, label, index);
            var b = MenuButton(row, "-", Vector2.zero, new Vector2(360, 46));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            value = b.GetComponentInChildren<Text>();
            value.fontSize = 20;
            return b;
        }
    }
}
