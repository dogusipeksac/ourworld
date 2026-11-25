using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Tactical uGUI HUD overlay for Aydos Atış Poligonu:
    /// - Dynamic 4-arm expanding crosshair + X hitmarker (white/gold/red)
    /// - Full-screen procedural Sniper Scope mask with mil-dots, rangefinder & live wind telemetry
    /// - Compact Zoomed Target Card plotting real bullet impact dots in Classic & 100m Sniper modes
    /// - Floating score & bullseye callout popups
    /// - Non-overlapping Mode Switcher, Weapon Bar, Ammo/Reload, Hold-Breath, Trap "ÇEK!", and Armory & Upgrades Shop
    /// </summary>
    public sealed class ShootingRangeHud : MonoBehaviour
    {
        private LiveShootingRange _range;
        private Canvas _canvas;
        private Font _font;

        // Crosshair & hitmarker
        private GameObject _crosshairRoot;
        private RectTransform _chTop, _chBot, _chLeft, _chRight;
        private Image _chDot;
        private GameObject _hitmarkerRoot;
        private readonly List<Image> _hitmarkerArms = new List<Image>(4);
        private float _hitmarkerTimer;

        // Sniper full-screen scope
        private GameObject _scopeRoot;
        private Text _scopeTelemetryText;
        private Image _scopeBreathFill;
        private Sprite _scopeMaskSprite;
        private Texture2D _scopeMaskTex;

        // Classic / Sniper zoomed bullseye hit card (top-left)
        private GameObject _targetCardRoot;
        private RectTransform _targetCardDisc;
        private Sprite _targetCardSprite;
        private Texture2D _targetCardTex;
        private readonly List<GameObject> _targetDots = new List<GameObject>(16);

        // Floating score popup
        private Text _popupText;
        private RectTransform _popupRt;
        private float _popupTimer;

        // Quick mode / action buttons (left-upper strip)
        private readonly Image[] _modeBtnBgs = new Image[4];
        private GameObject _pullClayBtnGo;
        private GameObject _breathBtnGo;
        private Image _breathBtnBg;
        private Text _breathBtnLabel;

        // Quick weapon & ammo bar (bottom-center strip)
        private readonly Image[] _weaponBtnBgs = new Image[4];
        private readonly Text[] _weaponBtnLabels = new Text[4];
        private Text _ammoBigText;
        private Text _weaponSubText;

        // Armory & Upgrades Drawer
        private GameObject _armoryRoot;
        private Text _armoryWalletText;
        private Transform _armoryListContent;
        private readonly List<GameObject> _armoryRows = new List<GameObject>(24);

        private int _lastRevision = -1;

        public void Attach(LiveShootingRange range)
        {
            _range = range;
            if (_canvas == null)
            {
                BuildCanvasUi();
            }
            RefreshUiState(force: true);
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(visible);
            }
            if (!visible && _armoryRoot != null)
            {
                _armoryRoot.SetActive(false);
            }
        }

        public void ClearTargetCardDots()
        {
            for (int i = 0; i < _targetDots.Count; i++)
            {
                if (_targetDots[i] != null) Destroy(_targetDots[i]);
            }
            _targetDots.Clear();
        }

        public void ClearPopup()
        {
            _popupTimer = 0f;
            if (_popupText != null) _popupText.gameObject.SetActive(false);
        }

        public void AddTargetCardDot(Vector2 normXY, bool isBullseye)
        {
            if (_targetCardDisc == null) return;
            GameObject dot = new GameObject("HitDot");
            dot.transform.SetParent(_targetCardDisc, false);
            RectTransform rt = dot.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = isBullseye ? new Vector2(11f, 11f) : new Vector2(8f, 8f);
            Vector2 clamped = Vector2.ClampMagnitude(normXY, 1.05f);
            rt.anchoredPosition = clamped * 56f;
            Image img = dot.AddComponent<Image>();
            img.color = isBullseye ? new Color(1.0f, 0.88f, 0.14f, 1f) : new Color(0.95f, 0.22f, 0.18f, 1f);
            img.raycastTarget = false;
            _targetDots.Add(dot);
        }

        public void TriggerHitmarker(int kind)
        {
            // kind: 1 = normal white hit, 2 = gold bullseye/combo/clay, 3 = red penalty
            Color col = kind switch
            {
                2 => new Color(1.0f, 0.86f, 0.18f, 1f),
                3 => new Color(1.0f, 0.22f, 0.18f, 1f),
                _ => new Color(1.0f, 1.0f, 1.0f, 0.96f)
            };
            for (int i = 0; i < _hitmarkerArms.Count; i++)
            {
                _hitmarkerArms[i].color = col;
            }
            _hitmarkerTimer = kind == 2 ? 0.26f : 0.18f;
            if (_hitmarkerRoot != null) _hitmarkerRoot.SetActive(true);
        }

        public void ShowPopup(string text, Color color)
        {
            if (_popupText == null) return;
            _popupText.text = text;
            _popupText.color = color;
            _popupTimer = 1.25f;
            if (_popupRt != null) _popupRt.anchoredPosition = new Vector2(0f, 64f);
            _popupText.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_scopeMaskTex != null) Destroy(_scopeMaskTex);
            if (_targetCardTex != null) Destroy(_targetCardTex);
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (_range == null || !_range.IsPlayerActive || _canvas == null || !_canvas.gameObject.activeSelf) return;

            float dt = Time.deltaTime;
            bool scopeOn = _range.IsFullScreenScope && !_range.IsArmoryOpen;
            if (_scopeRoot != null && _scopeRoot.activeSelf != scopeOn)
            {
                _scopeRoot.SetActive(scopeOn);
            }
            if (_crosshairRoot != null)
            {
                bool showCh = !scopeOn && !_range.IsArmoryOpen;
                if (_crosshairRoot.activeSelf != showCh) _crosshairRoot.SetActive(showCh);
                if (showCh)
                {
                    float gap = Mathf.Clamp(10f + _range.CurrentBloomDeg * 9.5f, 8f, 74f);
                    if (_chTop != null) _chTop.anchoredPosition = new Vector2(0f, gap);
                    if (_chBot != null) _chBot.anchoredPosition = new Vector2(0f, -gap);
                    if (_chLeft != null) _chLeft.anchoredPosition = new Vector2(-gap, 0f);
                    if (_chRight != null) _chRight.anchoredPosition = new Vector2(gap, 0f);
                    if (_chDot != null)
                    {
                        bool hasRedDot = _range.CurrentWeapon == ShootingWeaponId.Pistol9mm &&
                                         ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.PistolRedDot);
                        _chDot.color = (_range.IsAds || hasRedDot)
                            ? new Color(1.0f, 0.20f, 0.16f, 0.96f)
                            : new Color(1.0f, 0.96f, 0.72f, 0.90f);
                    }
                }
            }

            if (scopeOn && _scopeTelemetryText != null)
            {
                bool has8x = ShootingRangeStore.IsWeaponOwned(ShootingWeaponId.Sniper100m) &&
                             ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.SniperScope8x);
                string windArrow = _range.WindSpeedMs >= 0f ? "→ DOĞU" : "← BATI";
                string breathTag = _range.IsHoldingBreath ? "NEFES TUTULUYOR" : "SHIFT / NEFES";
                _scopeTelemetryText.text =
                    $"ZOOM: {(has8x ? "8.0x" : "4.0x")}   |   MESAFE: {_range.CurrentTargetDistanceMeters:F1}m\n" +
                    $"RÜZGAR: {Mathf.Abs(_range.WindSpeedMs):F1} m/s {windArrow}   |   {breathTag}";
                if (_scopeBreathFill != null)
                {
                    _scopeBreathFill.fillAmount = _range.BreathFill01;
                    _scopeBreathFill.color = _range.IsHoldingBreath
                        ? new Color(0.32f, 0.94f, 0.64f, 0.95f)
                        : new Color(0.42f, 0.84f, 1.0f, 0.88f);
                }
            }

            if (_hitmarkerTimer > 0f)
            {
                _hitmarkerTimer -= dt;
                if (_hitmarkerTimer <= 0f && _hitmarkerRoot != null)
                {
                    _hitmarkerRoot.SetActive(false);
                }
            }

            if (_popupTimer > 0f && _popupText != null)
            {
                _popupTimer -= dt;
                if (_popupRt != null)
                {
                    _popupRt.anchoredPosition += new Vector2(0f, 36f * dt);
                }
                Color c = _popupText.color;
                c.a = Mathf.Clamp01(_popupTimer / 0.45f);
                _popupText.color = c;
                if (_popupTimer <= 0f)
                {
                    _popupText.gameObject.SetActive(false);
                }
            }

            if (_lastRevision != _range.HudRevision)
            {
                _lastRevision = _range.HudRevision;
                RefreshUiState(force: false);
            }
        }

        private void RefreshUiState(bool force)
        {
            if (_range == null) return;

            for (int i = 0; i < 4; i++)
            {
                if (_modeBtnBgs[i] != null)
                {
                    bool active = (int)_range.CurrentMode == i;
                    _modeBtnBgs[i].color = active
                        ? new Color(0.92f, 0.62f, 0.12f, 0.96f)
                        : new Color(0.08f, 0.12f, 0.18f, 0.84f);
                }
                if (_weaponBtnBgs[i] != null)
                {
                    bool active = (int)_range.CurrentWeapon == i;
                    _weaponBtnBgs[i].color = active
                        ? new Color(0.16f, 0.56f, 0.86f, 0.95f)
                        : new Color(0.08f, 0.12f, 0.18f, 0.84f);
                }
                if (_weaponBtnLabels[i] != null)
                {
                    bool owned = ShootingRangeStore.IsWeaponOwned((ShootingWeaponId)i);
                    string shortName = i switch
                    {
                        0 => "1 · Tabanca",
                        1 => "2 · Pompalı",
                        2 => "3 · Karabin",
                        _ => "4 · Sniper"
                    };
                    _weaponBtnLabels[i].text = owned ? shortName : $"{shortName} ★";
                }
            }

            if (_ammoBigText != null)
            {
                _ammoBigText.text = _range.IsReloading
                    ? "DOLUYOR..."
                    : $"{_range.CurrentAmmo} / {_range.CurrentMaxAmmo}";
                _ammoBigText.color = _range.CurrentAmmo <= 2
                    ? new Color(1.0f, 0.42f, 0.34f)
                    : new Color(1.0f, 0.96f, 0.84f);
            }

            if (_weaponSubText != null)
            {
                bool owns = ShootingRangeStore.IsWeaponOwned(_range.CurrentWeapon);
                _weaponSubText.text = owns
                    ? $"{_range.GetCurrentSpec().NameTr} · Kişisel (+%30 Ödül)"
                    : $"{_range.GetCurrentSpec().NameTr} · Kulüp Deneme Silahı";
            }

            if (_pullClayBtnGo != null)
            {
                _pullClayBtnGo.SetActive(_range.CurrentMode == ShootingModeId.TrapClay && !_range.IsRoundFinished);
            }
            if (_breathBtnGo != null)
            {
                bool showBreath = _range.IsAds && (_range.CurrentWeapon == ShootingWeaponId.Sniper100m || _range.CurrentWeapon == ShootingWeaponId.TacticalCarbine);
                _breathBtnGo.SetActive(showBreath);
                if (showBreath && _breathBtnBg != null && _breathBtnLabel != null)
                {
                    _breathBtnBg.color = _range.IsHoldingBreath
                        ? new Color(0.18f, 0.72f, 0.44f, 0.96f)
                        : new Color(0.10f, 0.24f, 0.36f, 0.90f);
                    _breathBtnLabel.text = _range.IsHoldingBreath ? "NEFES TUTULUYOR" : "NEFES TUT";
                }
            }
            if (_targetCardRoot != null)
            {
                bool showCard = (_range.CurrentMode == ShootingModeId.ClassicBullseye || _range.CurrentMode == ShootingModeId.SniperWind100m) && !_range.IsArmoryOpen;
                _targetCardRoot.SetActive(showCard);
            }

            if (force || (_armoryRoot != null && _armoryRoot.activeSelf))
            {
                RefreshArmoryDrawer();
            }
        }

        // =====================================================================
        // PROCEDURAL UI CONSTRUCTION
        // =====================================================================
        private void BuildCanvasUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject canvasGo = new GameObject("ShootingRange_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 45;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildSniperScopeOverlay(canvasGo.transform);
            BuildCrosshairAndHitmarker(canvasGo.transform);
            BuildTargetCard(canvasGo.transform);
            BuildModeAndActionStrip(canvasGo.transform);
            BuildBottomWeaponStrip(canvasGo.transform);
            BuildArmoryDrawer(canvasGo.transform);
        }

        private void BuildSniperScopeOverlay(Transform parent)
        {
            _scopeRoot = new GameObject("SniperScopeOverlay");
            _scopeRoot.transform.SetParent(parent, false);
            RectTransform srt = _scopeRoot.AddComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // Procedural 512x512 crisp circular sniper scope vignette texture
            const int texSize = 512;
            _scopeMaskTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[texSize * texSize];
            float half = texSize * 0.5f;
            float innerR = texSize * 0.415f;
            float rimR = texSize * 0.440f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a;
                    if (d >= rimR) a = 255;
                    else if (d > innerR) a = (byte)(Mathf.SmoothStep(0f, 255f, (d - innerR) / (rimR - innerR)));
                    else a = 0;
                    px[y * texSize + x] = new Color32(4, 6, 10, a);
                }
            }
            _scopeMaskTex.SetPixels32(px);
            _scopeMaskTex.Apply(false, false);
            _scopeMaskSprite = Sprite.Create(_scopeMaskTex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));

            // Side black bars for 16:9+ widescreen coverage
            MakePanel("ScopeSideL", _scopeRoot.transform, new Vector2(0f, 0f), new Vector2(0.24f, 1f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.02f, 0.035f, 1f), raycast: false);
            MakePanel("ScopeSideR", _scopeRoot.transform, new Vector2(0.76f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.02f, 0.035f, 1f), raycast: false);

            GameObject ringGo = new GameObject("ScopeCircleMask");
            ringGo.transform.SetParent(_scopeRoot.transform, false);
            RectTransform rrt = ringGo.AddComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(1140f, 1140f);
            Image ringImg = ringGo.AddComponent<Image>();
            ringImg.sprite = _scopeMaskSprite;
            ringImg.raycastTarget = false;

            // Razor-thin mil-dot crosshairs
            MakeCenteredBox("ScopeLineH", _scopeRoot.transform, Vector2.zero, new Vector2(960f, 2.2f), new Color(0.04f, 0.05f, 0.06f, 0.92f));
            MakeCenteredBox("ScopeLineV", _scopeRoot.transform, Vector2.zero, new Vector2(2.2f, 960f), new Color(0.04f, 0.05f, 0.06f, 0.92f));
            MakeCenteredBox("ScopeCenterRed", _scopeRoot.transform, Vector2.zero, new Vector2(6f, 6f), new Color(1.0f, 0.16f, 0.12f, 0.96f));

            // Mil-dot stadia ticks along X & Y
            for (int m = -4; m <= 4; m++)
            {
                if (m == 0) continue;
                MakeCenteredBox($"MilX_{m}", _scopeRoot.transform, new Vector2(m * 62f, 0f), new Vector2(2.5f, 14f), new Color(0.05f, 0.05f, 0.07f, 0.88f));
                MakeCenteredBox($"MilY_{m}", _scopeRoot.transform, new Vector2(0f, m * 62f), new Vector2(14f, 2.5f), new Color(0.05f, 0.05f, 0.07f, 0.88f));
            }

            // Live Rangefinder & Wind Telemetry Readout inside lower scope arc
            GameObject telBg = MakeCenteredBox("ScopeTelemetryBg", _scopeRoot.transform, new Vector2(0f, -295f), new Vector2(540f, 66f), new Color(0.04f, 0.08f, 0.12f, 0.80f));
            _scopeTelemetryText = MakeText("ScopeTelemetryText", telBg.transform, "ZOOM: 4.0x | MESAFE: 97.2m", 20, TextAnchor.MiddleCenter, new Color(0.45f, 0.96f, 0.82f));

            // Breath steadiness mini-bar below telemetry
            GameObject bBarBg = MakeCenteredBox("ScopeBreathBg", _scopeRoot.transform, new Vector2(0f, -342f), new Vector2(280f, 10f), new Color(0.12f, 0.16f, 0.22f, 0.90f));
            GameObject bFillGo = new GameObject("ScopeBreathFill");
            bFillGo.transform.SetParent(bBarBg.transform, false);
            RectTransform bfrt = bFillGo.AddComponent<RectTransform>();
            bfrt.anchorMin = Vector2.zero;
            bfrt.anchorMax = Vector2.one;
            bfrt.offsetMin = Vector2.zero;
            bfrt.offsetMax = Vector2.zero;
            _scopeBreathFill = bFillGo.AddComponent<Image>();
            _scopeBreathFill.type = Image.Type.Filled;
            _scopeBreathFill.fillMethod = Image.FillMethod.Horizontal;
            _scopeBreathFill.color = new Color(0.42f, 0.84f, 1.0f, 0.90f);
            _scopeBreathFill.raycastTarget = false;

            _scopeRoot.SetActive(false);
        }

        private void BuildCrosshairAndHitmarker(Transform parent)
        {
            _crosshairRoot = new GameObject("TacticalCrosshair");
            _crosshairRoot.transform.SetParent(parent, false);
            RectTransform crt = _crosshairRoot.AddComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(180f, 180f);

            Color chCol = new Color(1.0f, 0.98f, 0.92f, 0.92f);
            _chTop = MakeCenteredBox("CH_Top", _crosshairRoot.transform, new Vector2(0f, 18f), new Vector2(3.5f, 18f), chCol).GetComponent<RectTransform>();
            _chBot = MakeCenteredBox("CH_Bot", _crosshairRoot.transform, new Vector2(0f, -18f), new Vector2(3.5f, 18f), chCol).GetComponent<RectTransform>();
            _chLeft = MakeCenteredBox("CH_Left", _crosshairRoot.transform, new Vector2(-18f, 0f), new Vector2(18f, 3.5f), chCol).GetComponent<RectTransform>();
            _chRight = MakeCenteredBox("CH_Right", _crosshairRoot.transform, new Vector2(18f, 0f), new Vector2(18f, 3.5f), chCol).GetComponent<RectTransform>();
            _chDot = MakeCenteredBox("CH_Dot", _crosshairRoot.transform, Vector2.zero, new Vector2(5.5f, 5.5f), new Color(1.0f, 0.24f, 0.18f, 0.95f)).GetComponent<Image>();

            // X Hitmarker
            _hitmarkerRoot = new GameObject("HitmarkerX");
            _hitmarkerRoot.transform.SetParent(parent, false);
            RectTransform hrt = _hitmarkerRoot.AddComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(64f, 64f);
            Vector2[] offsets = { new Vector2(12, 12), new Vector2(-12, 12), new Vector2(12, -12), new Vector2(-12, -12) };
            float[] angles = { 45f, -45f, -45f, 45f };
            for (int i = 0; i < 4; i++)
            {
                GameObject arm = MakeCenteredBox($"HM_{i}", _hitmarkerRoot.transform, offsets[i], new Vector2(16f, 3.8f), Color.white);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                _hitmarkerArms.Add(arm.GetComponent<Image>());
            }
            _hitmarkerRoot.SetActive(false);

            // Floating score popup above crosshair
            GameObject popGo = new GameObject("ScorePopup");
            popGo.transform.SetParent(parent, false);
            _popupRt = popGo.AddComponent<RectTransform>();
            _popupRt.anchorMin = _popupRt.anchorMax = new Vector2(0.5f, 0.5f);
            _popupRt.sizeDelta = new Vector2(460f, 54f);
            _popupRt.anchoredPosition = new Vector2(0f, 64f);
            _popupText = popGo.AddComponent<Text>();
            _popupText.font = _font;
            _popupText.fontSize = 30;
            _popupText.fontStyle = FontStyle.Bold;
            _popupText.alignment = TextAnchor.MiddleCenter;
            _popupText.raycastTarget = false;
            Outline popOut = popGo.AddComponent<Outline>();
            popOut.effectColor = new Color(0f, 0f, 0f, 0.85f);
            popOut.effectDistance = new Vector2(1.6f, -1.6f);
            popGo.SetActive(false);
        }

        private void BuildTargetCard(Transform parent)
        {
            _targetCardRoot = MakePanel("ZoomedTargetCard", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -380f), new Vector2(182f, -206f),
                new Color(0.06f, 0.09f, 0.14f, 0.84f), raycast: false);

            Text title = MakeText("CardTitle", _targetCardRoot.transform, "HEDEF KARTI", 15, TextAnchor.UpperCenter, new Color(0.95f, 0.86f, 0.42f));
            title.GetComponent<RectTransform>().offsetMax = new Vector2(0f, -6f);

            // Procedural 160x160 bullseye rings sprite
            const int s = 160;
            _targetCardTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[s * s];
            float c = s * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (s * 0.48f);
                    Color32 col;
                    if (d > 1.0f) col = new Color32(0, 0, 0, 0);
                    else if (d <= 0.12f) col = new Color32(20, 22, 26, 255);
                    else if (d <= 0.30f) col = new Color32(242, 198, 32, 255);
                    else if (d <= 0.56f) col = new Color32(214, 42, 36, 255);
                    else if (d <= 0.78f) col = new Color32(36, 118, 214, 255);
                    else if (d <= 0.96f) col = new Color32(26, 28, 34, 255);
                    else col = new Color32(236, 234, 226, 255);
                    px[y * s + x] = col;
                }
            }
            _targetCardTex.SetPixels32(px);
            _targetCardTex.Apply(false, false);
            _targetCardSprite = Sprite.Create(_targetCardTex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));

            GameObject discGo = new GameObject("TargetDisc");
            discGo.transform.SetParent(_targetCardRoot.transform, false);
            _targetCardDisc = discGo.AddComponent<RectTransform>();
            _targetCardDisc.anchorMin = _targetCardDisc.anchorMax = new Vector2(0.5f, 0.5f);
            _targetCardDisc.sizeDelta = new Vector2(126f, 126f);
            _targetCardDisc.anchoredPosition = new Vector2(0f, -10f);
            Image dImg = discGo.AddComponent<Image>();
            dImg.sprite = _targetCardSprite;
            dImg.raycastTarget = false;
        }

        private void BuildModeAndActionStrip(Transform parent)
        {
            // Top-left non-overlapping strip below corner margin: 4 mode pills + Armory button
            GameObject strip = MakePanel("ModeStrip", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -194f), new Vector2(534f, -124f),
                new Color(0.05f, 0.08f, 0.13f, 0.72f), raycast: false);

            string[] modeNames = { "Hedef", "IPSC", "Trap", "100m" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                float x0 = 8f + i * 92f;
                Button btn = MakeButton($"ModeBtn_{i}", strip.transform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(x0 + 42f, 0f), new Vector2(84f, 52f),
                    modeNames[i], 19, new Color(0.08f, 0.12f, 0.18f, 0.88f), Color.white,
                    () => { if (_range != null) { _range.IsArmoryOpen = false; _armoryRoot?.SetActive(false); _range.SelectMode((ShootingModeId)idx); } });
                _modeBtnBgs[i] = btn.GetComponent<Image>();
            }

            // Armory & Upgrades button right beside the 4 mode pills!
            MakeButton("ArmoryOpenBtn", strip.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(442f, 0f), new Vector2(128f, 52f),
                "CEPHANELİK", 17, new Color(0.18f, 0.58f, 0.34f, 0.95f), Color.white,
                ToggleArmoryDrawer);

            // Contextual "ÇEK!" Clay Launcher button (visible in Trap mode)
            Button pullBtn = MakeButton("TrapPullBtn", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(118f, -242f), new Vector2(190f, 56f),
                "PLAK FIRLAT (C)", 20, new Color(0.92f, 0.42f, 0.10f, 0.96f), Color.white,
                () => _range?.LaunchClayPigeon());
            _pullClayBtnGo = pullBtn.gameObject;
            _pullClayBtnGo.SetActive(false);

            // Contextual Hold Breath button (visible when ADS with Sniper/Carbine)
            Button breathBtn = MakeButton("HoldBreathBtn", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(288f, -242f), new Vector2(180f, 54f),
                "NEFES TUT", 19, new Color(0.10f, 0.24f, 0.36f, 0.90f), Color.white,
                () => _range?.ToggleBreathHold());
            _breathBtnGo = breathBtn.gameObject;
            _breathBtnBg = breathBtn.GetComponent<Image>();
            _breathBtnLabel = breathBtn.GetComponentInChildren<Text>();
            _breathBtnGo.SetActive(false);
        }

        private void BuildBottomWeaponStrip(Transform parent)
        {
            // Bottom-center sleek weapon selector + ammo readout + Reload button (cleanly between Left Joystick & Right Attack/ADS!)
            GameObject bar = MakePanel("WeaponBar", parent,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-345f, 18f), new Vector2(345f, 128f),
                new Color(0.05f, 0.08f, 0.13f, 0.84f), raycast: false);

            // Top row inside bar: Weapon title & big ammo count + Reload button
            _weaponSubText = MakeText("WeaponSubText", bar.transform, "9mm Tabanca · Kişisel", 18, TextAnchor.MiddleLeft, new Color(0.76f, 0.88f, 0.98f));
            RectTransform wrt = _weaponSubText.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0f, 0.56f);
            wrt.anchorMax = new Vector2(0.56f, 0.96f);
            wrt.offsetMin = new Vector2(16f, 0f);
            wrt.offsetMax = Vector2.zero;

            _ammoBigText = MakeText("AmmoBigText", bar.transform, "15 / 15", 26, TextAnchor.MiddleRight, new Color(1.0f, 0.95f, 0.80f));
            RectTransform art = _ammoBigText.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.54f, 0.54f);
            art.anchorMax = new Vector2(0.80f, 0.98f);
            art.offsetMin = Vector2.zero;
            art.offsetMax = new Vector2(-10f, 0f);

            MakeButton("ReloadBtn", bar.transform,
                new Vector2(1f, 0.76f), new Vector2(1f, 0.76f),
                new Vector2(-68f, 0f), new Vector2(120f, 40f),
                "ŞARJÖR (R)", 16, new Color(0.24f, 0.34f, 0.46f, 0.95f), Color.white,
                () => _range?.StartReload());

            // Bottom row: 4 Weapon quick-switch pills
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                float cx = -252f + i * 168f;
                Button wb = MakeButton($"WeaponBtn_{i}", bar.transform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(cx, 31f), new Vector2(158f, 46f),
                    $"{i + 1}", 18, new Color(0.08f, 0.12f, 0.18f, 0.88f), Color.white,
                    () => _range?.EquipWeapon((ShootingWeaponId)idx));
                _weaponBtnBgs[i] = wb.GetComponent<Image>();
                _weaponBtnLabels[i] = wb.GetComponentInChildren<Text>();
            }
        }

        private void BuildArmoryDrawer(Transform parent)
        {
            _armoryRoot = MakePanel("ArmoryModalBackdrop", parent,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.04f, 0.07f, 0.78f), raycast: true);

            GameObject card = MakePanel("ArmoryCard", _armoryRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-470f, -380f), new Vector2(470f, 380f),
                new Color(0.08f, 0.11f, 0.17f, 0.97f), raycast: true);

            Text title = MakeText("ArmoryTitle", card.transform, "AYDOS CEPHANELİK & 3D SİLAH YÜKSELTME", 26, TextAnchor.MiddleLeft, new Color(1.0f, 0.86f, 0.34f));
            RectTransform trt = title.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(0.72f, 1f);
            trt.offsetMin = new Vector2(26f, -68f);
            trt.offsetMax = new Vector2(0f, -12f);

            _armoryWalletText = MakeText("ArmoryWallet", card.transform, "Bakiye: 0 ₺", 24, TextAnchor.MiddleRight, new Color(0.42f, 0.96f, 0.58f));
            RectTransform wr = _armoryWalletText.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.65f, 1f);
            wr.anchorMax = new Vector2(0.88f, 1f);
            wr.offsetMin = new Vector2(0f, -68f);
            wr.offsetMax = new Vector2(-10f, -12f);

            MakeButton("CloseArmoryBtn", card.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-54f, -40f), new Vector2(82f, 44f),
                "KAPAT", 18, new Color(0.78f, 0.20f, 0.18f, 0.96f), Color.white,
                ToggleArmoryDrawer);

            // ScrollRect for weapons + 3D attachments
            GameObject scrollGo = new GameObject("ArmoryScroll");
            scrollGo.transform.SetParent(card.transform, false);
            RectTransform srect = scrollGo.AddComponent<RectTransform>();
            srect.anchorMin = Vector2.zero;
            srect.anchorMax = Vector2.one;
            srect.offsetMin = new Vector2(20f, 20f);
            srect.offsetMax = new Vector2(-20f, -78f);
            ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 36f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform vrt = viewport.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            sr.viewport = vrt;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, 900f);
            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(6, 6, 6, 12);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            _armoryListContent = content.transform;
            _armoryRoot.SetActive(false);
        }

        private void ToggleArmoryDrawer()
        {
            if (_range == null || _armoryRoot == null) return;
            bool next = !_armoryRoot.activeSelf;
            _armoryRoot.SetActive(next);
            _range.IsArmoryOpen = next;
            GameFeelAudio.PlayUiClick();
            if (next) RefreshArmoryDrawer();
        }

        private void RefreshArmoryDrawer()
        {
            if (_armoryListContent == null || _range == null) return;
            for (int i = 0; i < _armoryRows.Count; i++)
            {
                if (_armoryRows[i] != null) Destroy(_armoryRows[i]);
            }
            _armoryRows.Clear();

            int money = SaveManager.Instance != null ? SaveManager.Instance.Data.money : 0;
            if (_armoryWalletText != null) _armoryWalletText.text = $"Bakiye: {money} ₺";

            // Section 1: 4 Firearms
            AddArmoryHeader("SİLAHLAR (Satın Al -> 3D Eklentiler Açılır + %30 Ödül Bonusu)");
            for (int i = 0; i < LiveShootingRange.Weapons.Length; i++)
            {
                WeaponSpec w = LiveShootingRange.Weapons[i];
                bool owned = ShootingRangeStore.IsWeaponOwned(w.Id);
                bool equipped = _range.CurrentWeapon == w.Id;
                string sub = $"Şarjör: {w.BaseMag} · {(w.IsFullAuto ? "Seri Atış" : (w.UsesFullScreenScope ? "Dürbünlü" : "Yarı-Oto"))}";
                string btnLabel = owned ? (equipped ? "KUŞANILDI" : "KUŞAN") : $"SATIN AL ({w.BuyCost} ₺)";
                Color btnCol = owned
                    ? (equipped ? new Color(0.18f, 0.62f, 0.36f, 0.95f) : new Color(0.18f, 0.46f, 0.78f, 0.95f))
                    : new Color(0.86f, 0.56f, 0.10f, 0.95f);

                ShootingWeaponId capturedId = w.Id;
                AddArmoryRow(w.NameTr, sub, btnLabel, btnCol, () =>
                {
                    if (ShootingRangeStore.IsWeaponOwned(capturedId)) _range.EquipWeapon(capturedId);
                    else _range.TryBuyWeapon(capturedId);
                    RefreshArmoryDrawer();
                });
            }

            // Section 2: 11 Visible 3D Attachments for Active Weapon (and all others)
            AddArmoryHeader($"3D EKLENTİLER · ({_range.GetCurrentSpec().NameTr} öncelikli)");
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < LiveShootingRange.Upgrades.Length; i++)
                {
                    UpgradeSpec u = LiveShootingRange.Upgrades[i];
                    bool matchesCurrent = u.Weapon == _range.CurrentWeapon;
                    if ((pass == 0 && !matchesCurrent) || (pass == 1 && matchesCurrent)) continue;

                    bool wOwned = ShootingRangeStore.IsWeaponOwned(u.Weapon);
                    bool uOwned = ShootingRangeStore.IsUpgradeOwned(u.Id);
                    string weaponName = LiveShootingRange.Weapons[(int)u.Weapon].NameTr;
                    string title = $"{u.NameTr}  [{weaponName}]";
                    string btnLabel = uOwned ? "TAKILI ✓" : (wOwned ? $"AL ({u.Cost} ₺)" : "SİLAH KİLİTLİ");
                    Color btnCol = uOwned
                        ? new Color(0.18f, 0.62f, 0.36f, 0.92f)
                        : (wOwned ? new Color(0.18f, 0.50f, 0.84f, 0.95f) : new Color(0.28f, 0.30f, 0.34f, 0.85f));

                    ShootingUpgradeId capU = u.Id;
                    AddArmoryRow(title, u.EffectTr, btnLabel, btnCol, () =>
                    {
                        if (!ShootingRangeStore.IsUpgradeOwned(capU))
                        {
                            _range.TryBuyUpgrade(capU);
                            RefreshArmoryDrawer();
                        }
                    });
                }
            }
        }

        private void AddArmoryHeader(string text)
        {
            GameObject go = new GameObject("ArmorySectionHeader");
            go.transform.SetParent(_armoryListContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            Text t = MakeText("HeaderTxt", go.transform, text, 19, TextAnchor.MiddleLeft, new Color(0.96f, 0.82f, 0.32f));
            t.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 0f);
            _armoryRows.Add(go);
        }

        private void AddArmoryRow(string title, string sub, string btnLabel, Color btnColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject row = new GameObject("ArmoryRow");
            row.transform.SetParent(_armoryListContent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 64f;
            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.24f, 0.92f);

            Text t1 = MakeText("RowTitle", row.transform, title, 20, TextAnchor.UpperLeft, Color.white);
            RectTransform rt1 = t1.GetComponent<RectTransform>();
            rt1.anchorMin = new Vector2(0f, 0.42f);
            rt1.anchorMax = new Vector2(0.72f, 1f);
            rt1.offsetMin = new Vector2(16f, 0f);
            rt1.offsetMax = new Vector2(0f, -6f);

            Text t2 = MakeText("RowSub", row.transform, sub, 16, TextAnchor.LowerLeft, new Color(0.70f, 0.82f, 0.92f));
            RectTransform rt2 = t2.GetComponent<RectTransform>();
            rt2.anchorMin = new Vector2(0f, 0f);
            rt2.anchorMax = new Vector2(0.72f, 0.48f);
            rt2.offsetMin = new Vector2(16f, 6f);
            rt2.offsetMax = Vector2.zero;

            MakeButton("RowActionBtn", row.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-114f, 0f), new Vector2(204f, 44f),
                btnLabel, 17, btnColor, Color.white, onClick);

            _armoryRows.Add(row);
        }

        // =====================================================================
        // TINY UI BUILDERS
        // =====================================================================
        private GameObject MakePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color col, bool raycast)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            Image img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = raycast;
            return go;
        }

        private GameObject MakeCenteredBox(string name, Transform parent, Vector2 pos, Vector2 size, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return go;
        }

        private Text MakeText(string name, Transform parent, string text, int size, TextAnchor anchor, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = col;
            t.raycastTarget = false;
            return t;
        }

        private Button MakeButton(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, string label, int fontSize, Color bgCol, Color txtCol, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = bgCol;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            MakeText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter, txtCol);
            return btn;
        }
    }
}
