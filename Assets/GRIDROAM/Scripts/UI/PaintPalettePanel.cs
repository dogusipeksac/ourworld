using System.Collections;
using Gridroam.Core;
using Gridroam.Jobs;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Safe-area HUD modal for painting residential houses.
    /// Presents 8 architectural colour swatches with Turkish names + Cancel,
    /// highlights the requested colour during a House Painting job, supports keyboard 1-8 and Esc,
    /// locks player movement while open, and animates open/close over 0.2 s (unscaled time).
    /// </summary>
    public class PaintPalettePanel : MonoBehaviour
    {
        public static PaintPalettePanel Instance { get; private set; }

        public static readonly Color[] DefaultSwatchColors =
        {
            new Color(0.96f, 0.96f, 0.95f, 1f), // 1. Beyaz
            new Color(0.95f, 0.89f, 0.78f, 1f), // 2. Krem
            new Color(0.82f, 0.44f, 0.32f, 1f), // 3. Kiremit
            new Color(0.64f, 0.81f, 0.93f, 1f), // 4. Gök Mavisi
            new Color(0.66f, 0.78f, 0.65f, 1f), // 5. Adaçayı Yeşili
            new Color(0.95f, 0.89f, 0.60f, 1f), // 6. Açık Sarı
            new Color(0.79f, 0.72f, 0.88f, 1f), // 7. Lila
            new Color(0.32f, 0.34f, 0.38f, 1f)  // 8. Antrasit
        };

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text jobHintLabel;
        [SerializeField] private Button[] swatchButtons = new Button[8];
        [SerializeField] private Text[] swatchNameLabels = new Text[8];
        [SerializeField] private Color[] swatchColors = DefaultSwatchColors;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text cancelLabel;

        private PaintableHouse _targetHouse;
        private int _openedFrame = -1;
        private Coroutine _animRoutine;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        public void WireReferences(
            GameObject root,
            Text titleTxt,
            Button[] buttons,
            Color[] colors,
            Button cancelBtn,
            Text cancelTxt)
        {
            panelRoot = root;
            titleLabel = titleTxt;
            swatchButtons = buttons;
            swatchColors = colors;
            cancelButton = cancelBtn;
            cancelLabel = cancelTxt;
        }

        public void WirePolishReferences(CanvasGroup group, Text hintTxt, Text[] swatchLabels)
        {
            canvasGroup = group;
            jobHintLabel = hintTxt;
            swatchNameLabels = swatchLabels;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (swatchColors == null || swatchColors.Length < 8)
            {
                swatchColors = DefaultSwatchColors;
            }

            if (swatchButtons != null)
            {
                for (int i = 0; i < swatchButtons.Length; i++)
                {
                    int capturedIndex = i;
                    if (swatchButtons[i] != null)
                    {
                        swatchButtons[i].onClick.AddListener(() => SelectSwatch(capturedIndex));
                    }
                }
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Close);
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            RefreshLabels();
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleLanguageChanged(string _)
        {
            RefreshLabels();
        }

        public void RefreshLabels()
        {
            if (titleLabel != null)
            {
                string baseTitle = LocalizationService.Tr("ui.paint.title", "Evi Boya");
                titleLabel.text = _targetHouse != null
                    ? $"{baseTitle} · {_targetHouse.DisplayName}"
                    : baseTitle;
            }

            if (cancelLabel != null)
            {
                cancelLabel.text = LocalizationService.Tr("ui.btn.cancel", "İptal");
            }

            int jobTargetIdx = -1;
            if (JobManager.Instance != null &&
                JobManager.Instance.ActiveJob == JobType.HousePainting &&
                JobManager.Instance.TargetPaintHouse == _targetHouse)
            {
                jobTargetIdx = JobManager.Instance.TargetPaintSwatchIndex;
            }

            if (jobHintLabel != null)
            {
                if (jobTargetIdx >= 0)
                {
                    string colorName = JobManager.GetLocalizedColorName(jobTargetIdx);
                    string fmt = LocalizationService.Tr("ui.paint.job_hint", "Sipariş Rengi: {0} (+60 ₺)");
                    jobHintLabel.text = string.Format(fmt, colorName);
                    jobHintLabel.gameObject.SetActive(true);
                }
                else
                {
                    jobHintLabel.gameObject.SetActive(false);
                }
            }

            if (swatchNameLabels != null)
            {
                for (int i = 0; i < swatchNameLabels.Length; i++)
                {
                    if (swatchNameLabels[i] != null)
                    {
                        string cName = JobManager.GetLocalizedColorName(i);
                        swatchNameLabels[i].text = (i == jobTargetIdx) ? $"★ {cName}" : cName;
                    }
                }
            }
        }

        public void Open(PaintableHouse house)
        {
            if (house == null || panelRoot == null) return;

            _targetHouse = house;
            _openedFrame = Time.frameCount;
            RefreshLabels();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimatePanelRoutine(opening: true));
        }

        public void Close()
        {
            if (panelRoot == null) return;

            _targetHouse = null;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            if (!panelRoot.activeSelf) return;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimatePanelRoutine(opening: false));
        }

        public void SelectSwatch(int index)
        {
            if (swatchColors == null || index < 0 || index >= swatchColors.Length)
            {
                return;
            }

            Color chosen = swatchColors[index];
            if (_targetHouse != null)
            {
                _targetHouse.ApplyColor(chosen);
            }

            Close();
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { SelectSwatch(0); return; }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { SelectSwatch(1); return; }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { SelectSwatch(2); return; }
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) { SelectSwatch(3); return; }
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) { SelectSwatch(4); return; }
            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) { SelectSwatch(5); return; }
            if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) { SelectSwatch(6); return; }
            if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) { SelectSwatch(7); }
        }

        private IEnumerator AnimatePanelRoutine(bool opening)
        {
            if (panelRoot == null) yield break;

            RectTransform rt = panelRoot.GetComponent<RectTransform>();
            if (opening)
            {
                panelRoot.SetActive(true);
            }

            const float duration = 0.20f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;
            Vector3 startScale = opening ? Vector3.one * 0.94f : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.one * 0.94f;

            if (opening && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (rt != null)
            {
                rt.localScale = startScale;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }
                if (rt != null)
                {
                    rt.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = endAlpha;
            }
            if (rt != null)
            {
                rt.localScale = opening ? Vector3.one : endScale;
            }
            if (!opening)
            {
                panelRoot.SetActive(false);
            }

            _animRoutine = null;
        }
    }
}
