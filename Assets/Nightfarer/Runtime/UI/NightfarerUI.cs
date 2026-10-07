using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Drives the HUD (health with damage trail, stamina, skill cooldown, ultimate gauge, flasks, weapon, lock-on
    /// reticle, target bar, notifications, damage vignette) and the pause menu (settings saved to PlayerPrefs).
    /// </summary>
    public class NightfarerUI : MonoBehaviour
    {
        public NightfarerCharacter character;
        public NightfarerDebugHUD debugHud;
        public string mainMenuScene = "MainMenu";

        [Header("HUD")]
        public Canvas canvas;
        public CanvasGroup hudGroup;
        public Text nameText, surgeText, skillName, skillCooldownText, ultimateLabel, weaponName, flaskCount, targetName, notification;
        public Image hpFill, hpTrail, staminaFill, skillCooldown, ultimateFill, ultimateGlow, flaskIcon, targetFill, targetTrail, vignette;
        public RectTransform reticle;
        public GameObject targetBar;

        [Header("Pause menu")]
        public GameObject pauseRoot, mainPanel, settingsPanel, controlsPanel;
        public Button resumeButton, settingsButton, controlsButton, restartButton, mainMenuButton, quitButton, settingsBack, controlsBack;
        public Slider mouseSensitivity, padSensitivity, fieldOfView;
        public Toggle invertY, debugOverlay;
        public Button animationSetButton, profileButton;
        public Text animationSetLabel, profileLabel;

        public bool IsPaused { get; private set; }

        float hpTrailValue = 1f, targetTrailValue = 1f, hpTrailDelay, targetTrailDelay;
        float notifyTime, vignetteAlpha;
        TrainingDummy lastTarget;
        InputAction pauseAction;

        void Awake()
        {
            pauseAction = new InputAction("UIPause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
            WireMenu();
            if (pauseRoot != null) pauseRoot.SetActive(false);
            if (notification != null) notification.text = "";
        }

        void OnEnable() => pauseAction?.Enable();
        void OnDisable() => pauseAction?.Disable();
        void OnDestroy()
        {
            pauseAction?.Dispose();
            if (IsPaused) Time.timeScale = 1f;
        }

        void Start()
        {
            EnsureEventSystem();
            if (character != null)
            {
                character.Damaged += OnDamaged;
                character.Notification += Notify;
                character.Dodged += () => Notify("Dodged");
            }
            LoadSettings();
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        // ------------------------------------------------------------------ HUD

        void Update()
        {
            if (pauseAction != null && pauseAction.WasPressedThisFrame()) TogglePause();
            if (character == null || character.Profile == null) return;
            float dt = Time.unscaledDeltaTime;
            var v = character.Vitals;

            if (nameText != null) nameText.text = character.Profile.displayName.ToUpperInvariant();
            float hp = v.Health / Mathf.Max(1f, v.MaxHealth);
            UpdateBar(hpFill, hpTrail, hp, ref hpTrailValue, ref hpTrailDelay, dt);
            if (staminaFill != null) staminaFill.fillAmount = v.Stamina / Mathf.Max(1f, v.MaxStamina);
            if (surgeText != null) surgeText.enabled = character.IsSurging;

            var skill = character.Profile.skill;
            if (skillName != null) skillName.text = skill != null ? skill.displayName : "-";
            if (skillCooldown != null)
            {
                float cd = skill != null && skill.cooldown > 0f ? Mathf.Clamp01(character.SkillCooldownRemaining / skill.cooldown) : 0f;
                skillCooldown.fillAmount = cd;
                if (skillCooldownText != null) skillCooldownText.text = character.SkillCooldownRemaining > 0f ? Mathf.CeilToInt(character.SkillCooldownRemaining).ToString() : "";
            }

            if (ultimateFill != null) ultimateFill.fillAmount = character.UltimateGauge;
            if (ultimateGlow != null)
            {
                var c = ultimateGlow.color;
                c.a = character.UltimateReady ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
                ultimateGlow.color = c;
            }
            if (ultimateLabel != null) ultimateLabel.text = character.UltimateReady ? "READY" : Mathf.FloorToInt(character.UltimateGauge * 100f) + "%";

            if (weaponName != null) weaponName.text = character.Weapon != null ? ShortName(character.Weapon.displayName) : "";
            if (flaskCount != null) flaskCount.text = character.FlaskCharges.ToString();
            if (flaskIcon != null) flaskIcon.color = character.FlaskCharges > 0 ? new Color(0.75f, 0.12f, 0.12f) : new Color(0.25f, 0.2f, 0.2f);

            UpdateLockOn(dt);

            if (notification != null)
            {
                notifyTime -= dt;
                var c = notification.color;
                c.a = Mathf.Clamp01(notifyTime / 0.4f);
                notification.color = c;
            }
            if (vignette != null)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, dt * 1.6f);
                float lowHp = hp < 0.3f ? 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
                var c = vignette.color;
                c.a = Mathf.Max(vignetteAlpha, lowHp);
                vignette.color = c;
            }
        }

        static string ShortName(string s)
        {
            int i = s.IndexOf('(');
            return (i > 0 ? s.Substring(0, i) : s).Trim();
        }

        static void UpdateBar(Image fill, Image trail, float value, ref float trailValue, ref float delay, float dt)
        {
            if (fill == null) return;
            if (value < fill.fillAmount - 0.001f) delay = 0.6f;
            fill.fillAmount = value;
            if (value > trailValue) trailValue = value;
            else if ((delay -= dt) <= 0f) trailValue = Mathf.MoveTowards(trailValue, value, dt * 0.6f);
            if (trail != null) trail.fillAmount = trailValue;
        }

        void UpdateLockOn(float dt)
        {
            var target = character.LockOn != null ? character.LockOn.Current : null;
            var cam = character.cameraRig != null ? character.cameraRig.cam : Camera.main;
            bool show = target != null && cam != null;
            if (reticle != null)
            {
                Vector3 sp = show ? cam.WorldToScreenPoint(target.Point) : Vector3.zero;
                show &= sp.z > 0f;
                reticle.gameObject.SetActive(show);
                if (show) reticle.position = sp;
            }

            var dummy = target != null ? target.GetComponent<TrainingDummy>() : null;
            if (targetBar != null) targetBar.SetActive(dummy != null);
            if (dummy == null) return;
            if (dummy != lastTarget)
            {
                lastTarget = dummy;
                targetTrailValue = dummy.Health / dummy.maxHealth;
            }
            if (targetName != null) targetName.text = dummy.name;
            UpdateBar(targetFill, targetTrail, dummy.Health / dummy.maxHealth, ref targetTrailValue, ref targetTrailDelay, dt);
        }

        void OnDamaged(DamageInfo info)
        {
            vignetteAlpha = Mathf.Clamp01(0.25f + info.amount / 400f);
        }

        public void Notify(string message)
        {
            if (notification == null) return;
            notification.text = message.ToUpperInvariant();
            notifyTime = 1.8f;
        }

        // ------------------------------------------------------------------ pause menu

        void WireMenu()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => ShowPanel(settingsPanel));
            if (controlsButton != null) controlsButton.onClick.AddListener(() => ShowPanel(controlsPanel));
            if (settingsBack != null) settingsBack.onClick.AddListener(() => { SaveSettings(); ShowPanel(mainPanel); });
            if (controlsBack != null) controlsBack.onClick.AddListener(() => ShowPanel(mainPanel));
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (mouseSensitivity != null) mouseSensitivity.onValueChanged.AddListener(_ => ApplySettings());
            if (padSensitivity != null) padSensitivity.onValueChanged.AddListener(_ => ApplySettings());
            if (fieldOfView != null) fieldOfView.onValueChanged.AddListener(_ => ApplySettings());
            if (invertY != null) invertY.onValueChanged.AddListener(_ => ApplySettings());
            if (debugOverlay != null) debugOverlay.onValueChanged.AddListener(_ => ApplySettings());
            if (animationSetButton != null) animationSetButton.onClick.AddListener(() => { character?.CycleAnimationSet(); RefreshLabels(); });
            if (profileButton != null) profileButton.onClick.AddListener(() => { character?.CycleProfile(); RefreshLabels(); });
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused || pauseRoot == null) return;
            IsPaused = true;
            Time.timeScale = 0f;
            if (character != null) character.InputBlocked = true;
            DeviceInputSource.CursorReleased = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            pauseRoot.SetActive(true);
            if (hudGroup != null) hudGroup.alpha = 0.35f;
            ShowPanel(mainPanel);
            RefreshLabels();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            SaveSettings();
            Time.timeScale = 1f;
            if (character != null) character.InputBlocked = false;
            DeviceInputSource.CursorReleased = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            pauseRoot.SetActive(false);
            if (hudGroup != null) hudGroup.alpha = 1f;
        }

        void ShowPanel(GameObject panel)
        {
            if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
            var first = panel == mainPanel ? resumeButton : panel == settingsPanel ? (Selectable)mouseSensitivity : controlsBack;
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void RefreshLabels()
        {
            if (character == null) return;
            if (animationSetLabel != null) animationSetLabel.text = character.Animator.ActiveSet != null ? character.Animator.ActiveSet.displayName : "-";
            if (profileLabel != null) profileLabel.text = character.Profile != null ? character.Profile.displayName : "-";
        }

        void Restart()
        {
            Time.timeScale = 1f;
            DeviceInputSource.CursorReleased = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void GoToMainMenu()
        {
            Time.timeScale = 1f;
            DeviceInputSource.CursorReleased = false;
            if (Application.CanStreamedLevelBeLoaded(mainMenuScene)) SceneManager.LoadScene(mainMenuScene);
            else Notify("Main menu scene not in build settings");
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ settings

        void LoadSettings()
        {
            var cam = character != null ? character.cameraRig : null;
            if (mouseSensitivity != null) mouseSensitivity.SetValueWithoutNotify(PlayerPrefs.GetFloat("nf.mouseSens", cam != null ? cam.mouseSensitivity : 0.12f));
            if (padSensitivity != null) padSensitivity.SetValueWithoutNotify(PlayerPrefs.GetFloat("nf.padSens", cam != null ? cam.stickSpeed : 200f));
            if (fieldOfView != null) fieldOfView.SetValueWithoutNotify(PlayerPrefs.GetFloat("nf.fov", cam != null ? cam.baseFov : 55f));
            if (invertY != null) invertY.SetIsOnWithoutNotify(PlayerPrefs.GetInt("nf.invertY", 0) == 1);
            if (debugOverlay != null) debugOverlay.SetIsOnWithoutNotify(PlayerPrefs.GetInt("nf.debug", 0) == 1);
            ApplySettings();
        }

        void ApplySettings()
        {
            var cam = character != null ? character.cameraRig : null;
            if (cam != null)
            {
                if (mouseSensitivity != null) cam.mouseSensitivity = mouseSensitivity.value;
                if (padSensitivity != null) cam.stickSpeed = padSensitivity.value;
                if (fieldOfView != null) cam.baseFov = fieldOfView.value;
                if (invertY != null) cam.invertY = invertY.isOn;
            }
            if (debugHud != null && debugOverlay != null) debugHud.visible = debugOverlay.isOn;
        }

        void SaveSettings()
        {
            if (mouseSensitivity != null) PlayerPrefs.SetFloat("nf.mouseSens", mouseSensitivity.value);
            if (padSensitivity != null) PlayerPrefs.SetFloat("nf.padSens", padSensitivity.value);
            if (fieldOfView != null) PlayerPrefs.SetFloat("nf.fov", fieldOfView.value);
            if (invertY != null) PlayerPrefs.SetInt("nf.invertY", invertY.isOn ? 1 : 0);
            if (debugOverlay != null) PlayerPrefs.SetInt("nf.debug", debugOverlay.isOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
