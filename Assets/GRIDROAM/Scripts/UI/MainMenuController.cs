using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    public enum MenuDrawer
    {
        None = 0,
        Districts = 1,
        Settings = 2,
        Sports = 3
    }

    /// <summary>
    /// Living Open-World Main Menu & Pause Hub for Our World (Bizim Dünyamız).
    /// Launches over a live, smoothly orbiting scenic camera tour of Istanbul · Sultanbeyli · Hamidiye Mahallesi,
    /// seamlessly swooping down behind the player with zero loading screen when entering or fast-starting into any district.
    /// Also replaces the legacy top-right utility buttons with a clean HUD Menu button and supports ESC/Back pause toggling.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }
        public static bool IsMenuActive => Instance != null && (Instance.IsOpen || Instance.IsTransitioning);

        // --- Design System Color Tokens (Istanbul Night Slate + Turquoise + Golden Amber) ---
        private static readonly Color PanelDark = new Color(0.052f, 0.078f, 0.125f, 0.94f);
        private static readonly Color PanelGlass = new Color(0.072f, 0.106f, 0.168f, 0.88f);
        private static readonly Color CardSurface = new Color(0.102f, 0.146f, 0.224f, 0.92f);
        private static readonly Color CardSelected = new Color(0.105f, 0.280f, 0.295f, 0.96f);
        private static readonly Color BorderSubtle = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color BorderActive = new Color(0.20f, 0.82f, 0.75f, 0.92f);
        private static readonly Color AccentTeal = new Color(0.12f, 0.68f, 0.61f, 1f);
        private static readonly Color AccentTealDark = new Color(0.09f, 0.50f, 0.46f, 1f);
        private static readonly Color AccentGold = new Color(0.98f, 0.76f, 0.28f, 1f);
        private static readonly Color AccentSky = new Color(0.32f, 0.72f, 0.98f, 1f);
        private static readonly Color AccentMint = new Color(0.32f, 0.90f, 0.68f, 1f);
        private static readonly Color BadgeFoundBg = new Color(0.07f, 0.25f, 0.19f, 0.96f);
        private static readonly Color BadgeExploreBg = new Color(0.24f, 0.18f, 0.07f, 0.96f);
        private static readonly Color DangerRed = new Color(0.84f, 0.29f, 0.27f, 1f);
        private static readonly Color DangerSoft = new Color(0.38f, 0.13f, 0.14f, 0.94f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.97f, 0.99f, 1f);
        private static readonly Color TextSecondary = new Color(0.77f, 0.83f, 0.91f, 1f);
        private static readonly Color TextMuted = new Color(0.64f, 0.72f, 0.82f, 1f);
        private static readonly Color SegInactive = new Color(0.13f, 0.18f, 0.26f, 0.95f);

        private const string PrefMasterVolumeKey = "ow_master_volume";
        private const int DefaultTotalStoryMissions = 26;

        private struct ScenicVista
        {
            public string NameTr;
            public string NameEn;
            public Vector3 BasePos;
            public Vector3 LookAt;
            public float Fov;
            public float SwayRadius;
            public float SwaySpeed;
        }

        private struct DistrictOption
        {
            public string LandmarkId;
            public string NameTr;
            public string NameEn;
            public string TagTr;
            public string TagEn;
            public Vector3 SpawnPos;
            public float SpawnYaw;
            public Vector3 PreviewCamPos;
            public Vector3 PreviewLookAt;
        }

        // All coordinates verified clear & unobstructed in the live Hamidiye Mahallesi scene
        private static readonly ScenicVista[] ScenicVistas =
        {
            new ScenicVista
            {
                NameTr = "Fatih Bulvarı · Saat Kulesi",
                NameEn = "Fatih Blvd · Clock Tower",
                BasePos = new Vector3(-12.2f, 5.4f, 128.4f),
                LookAt = new Vector3(1.8f, 5.2f, 142.0f),
                Fov = 54f,
                SwayRadius = 1.05f,
                SwaySpeed = 0.18f
            },
            new ScenicVista
            {
                NameTr = "Selman Sokağı · Mahalle Merkezi",
                NameEn = "Selman Street · Heart of Hamidiye",
                BasePos = new Vector3(-56.0f, 3.8f, -2.5f),
                LookAt = new Vector3(-16.0f, 2.2f, 3.2f),
                Fov = 56f,
                SwayRadius = 0.90f,
                SwaySpeed = 0.16f
            },
            new ScenicVista
            {
                NameTr = "Hz. Osman Camii Avlusu",
                NameEn = "Hz. Osman Mosque Courtyard",
                BasePos = new Vector3(64.0f, 5.4f, -18.5f),
                LookAt = new Vector3(82.0f, 6.0f, -43.5f),
                Fov = 54f,
                SwayRadius = 1.10f,
                SwaySpeed = 0.16f
            },
            new ScenicVista
            {
                NameTr = "Hamidiye Spor Sahaları",
                NameEn = "Hamidiye Sports Arenas",
                BasePos = new Vector3(-31.5f, 4.6f, -5.5f),
                LookAt = new Vector3(-40.0f, 1.5f, -20.0f),
                Fov = 55f,
                SwayRadius = 1.05f,
                SwaySpeed = 0.18f
            },
            new ScenicVista
            {
                NameTr = "Petrol Yolu & Bulvar Panoraması",
                NameEn = "Petrol Yolu Avenue Panorama",
                BasePos = new Vector3(-46.0f, 9.6f, -66.5f),
                LookAt = new Vector3(4.0f, 3.0f, -84.0f),
                Fov = 56f,
                SwayRadius = 1.35f,
                SwaySpeed = 0.15f
            },
            new ScenicVista
            {
                NameTr = "Aydos Yeşil Vadi · Çiftlik & Bostan",
                NameEn = "Aydos Green Valley · Farm & Ranch",
                BasePos = new Vector3(132.0f, 6.2f, -14.0f),
                LookAt = new Vector3(182.0f, 2.5f, 4.0f),
                Fov = 56f,
                SwayRadius = 1.25f,
                SwaySpeed = 0.15f
            },
            new ScenicVista
            {
                NameTr = "Sultanbeyli Göleti & Çam Korusu",
                NameEn = "Sultanbeyli Lake & Pine Grove",
                BasePos = new Vector3(-156.0f, 6.6f, -18.0f),
                LookAt = new Vector3(-196.0f, 1.0f, 0.0f),
                Fov = 56f,
                SwayRadius = 1.25f,
                SwaySpeed = 0.14f
            }
        };

        private static readonly DistrictOption[] Districts =
        {
            new DistrictOption
            {
                LandmarkId = "lm_selman",
                NameTr = "Benim Evim · No:48",
                NameEn = "My Home · No:48",
                TagTr = "Selman Sokağı · Bahçeli Ev",
                TagEn = "Selman Street · Home Base",
                SpawnPos = new Vector3(-62.0f, 0.12f, 4.5f),
                SpawnYaw = 0f,
                PreviewCamPos = new Vector3(-56.5f, 4.2f, -3.2f),
                PreviewLookAt = new Vector3(-68.0f, 2.3f, 8.2f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_saatkulesi",
                NameTr = "Saat Kulesi Meydanı",
                NameEn = "Clock Tower Plaza",
                TagTr = "Fatih Bulvarı · Ana Meydan",
                TagEn = "Fatih Blvd · Central Plaza",
                SpawnPos = new Vector3(0.0f, 0.18f, 134.0f),
                SpawnYaw = 0f,
                PreviewCamPos = new Vector3(-13.5f, 5.2f, 126.0f),
                PreviewLookAt = new Vector3(1.5f, 5.4f, 142.0f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_camii",
                NameTr = "Hz. Osman Camii",
                NameEn = "Hz. Osman Mosque",
                TagTr = "Avlu · Şadırvan & Huzur",
                TagEn = "Courtyard & Landmark",
                SpawnPos = new Vector3(82.0f, 0.12f, -24.0f),
                SpawnYaw = 180f,
                PreviewCamPos = new Vector3(64.0f, 5.4f, -18.5f),
                PreviewLookAt = new Vector3(82.0f, 6.0f, -43.5f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_selman",
                NameTr = "Selman Bakkal & Çarşı",
                NameEn = "Selman Market & Shops",
                TagTr = "Bakkal · ATM · Oto Tamir & Modifiye",
                TagEn = "Market · ATM · Auto Repair & Tuning",
                SpawnPos = new Vector3(6.5f, 0.12f, 4.5f),
                SpawnYaw = 0f,
                PreviewCamPos = new Vector3(-3.0f, 3.6f, -3.2f),
                PreviewLookAt = new Vector3(14.5f, 2.2f, 6.5f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_tenis",
                NameTr = "Hamidiye Spor Sahaları",
                NameEn = "Hamidiye Sports Courts",
                TagTr = "Tenis · Futbol · Basketbol · Voleybol",
                TagEn = "Tennis · Football · Basketball · Volley",
                SpawnPos = new Vector3(-14.0f, 0.12f, -29.0f),
                SpawnYaw = 180f,
                PreviewCamPos = new Vector3(-31.5f, 4.6f, -5.5f),
                PreviewLookAt = new Vector3(-40.0f, 1.5f, -20.0f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_havuz",
                NameTr = "Açık Yüzme Havuzu",
                NameEn = "Open Swimming Pool",
                TagTr = "Serin Su · Yaz Atmosferi",
                TagEn = "Poolside · Summer Spot",
                SpawnPos = new Vector3(16.0f, 0.12f, -29.5f),
                SpawnYaw = 180f,
                PreviewCamPos = new Vector3(5.5f, 5.6f, -26.0f),
                PreviewLookAt = new Vector3(16.5f, 1.4f, -41.0f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_ciftlik",
                NameTr = "Aydos Çiftliği & Bostan",
                NameEn = "Aydos Farm & Ranch",
                TagTr = "Tarım · Tavuk · İnek · Koyun",
                TagEn = "Crops · Chickens · Cows · Sheep",
                SpawnPos = new Vector3(136.0f, 0.12f, 0.0f),
                SpawnYaw = 90f,
                PreviewCamPos = new Vector3(132.0f, 6.2f, -14.0f),
                PreviewLookAt = new Vector3(182.0f, 2.5f, 4.0f)
            },
            new DistrictOption
            {
                LandmarkId = "lm_golet",
                NameTr = "Sultanbeyli Göleti",
                NameEn = "Sultanbeyli Lake & Grove",
                TagTr = "Balıkçılık İskelesi · Oto Yıkama · Koru",
                TagEn = "Fishing Pier · Car Wash · Pine Grove",
                SpawnPos = new Vector3(-164.0f, 0.12f, 0.0f),
                SpawnYaw = -90f,
                PreviewCamPos = new Vector3(-156.0f, 6.6f, -18.0f),
                PreviewLookAt = new Vector3(-196.0f, 1.0f, 0.0f)
            }
        };

        private struct SportQuickOption
        {
            public string BadgeTr;
            public string BadgeEn;
            public string NameTr;
            public string NameEn;
            public string TagTr;
            public string TagEn;
            public Vector3 ExitPos;
            public float ExitYaw;
            public Vector3 PreviewCamPos;
            public Vector3 PreviewLookAt;
        }

        private static readonly SportQuickOption[] QuickSports =
        {
            new SportQuickOption
            {
                BadgeTr = "1v1  ·  +75 ₺",
                BadgeEn = "1v1  ·  +75 ₺",
                NameTr = "Tenis Kortu",
                NameEn = "Tennis Court",
                TagTr = "Gürsu Sk. Kortu\nİlk 3 puan kazanır",
                TagEn = "Gürsu St. Clay Court\nFirst to 3 points wins",
                ExitPos = new Vector3(-46.25f, 0.12f, -27.2f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(-48.5f, 5.8f, -28.0f),
                PreviewLookAt = new Vector3(-41.0f, 1.2f, -42.2f)
            },
            new SportQuickOption
            {
                BadgeTr = "3v3  ·  +90 ₺",
                BadgeEn = "3v3  ·  +90 ₺",
                NameTr = "Halı Saha Futbol",
                NameEn = "3v3 Street Football",
                TagTr = "Paslaş · Top Kap · Şut\nİlk 3 gol kazanır",
                TagEn = "Pass · Steal · Shoot\nFirst to 3 goals wins",
                ExitPos = new Vector3(-14.0f, 0.12f, -29.4f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(-23.0f, 6.8f, -29.0f),
                PreviewLookAt = new Vector3(-14.0f, 1.2f, -44.0f)
            },
            new SportQuickOption
            {
                BadgeTr = "2v2  ·  +90 ₺",
                BadgeEn = "2v2  ·  +90 ₺",
                NameTr = "Sokak Basketbolu",
                NameEn = "2v2 Streetball",
                TagTr = "2'lik & 3'lük Şutlar\nİlk 10 sayı kazanır",
                TagEn = "2pt & 3pt Jump Shots\nFirst to 10 points wins",
                ExitPos = new Vector3(41.0f, 0.12f, -32.2f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(33.0f, 6.2f, -32.0f),
                PreviewLookAt = new Vector3(41.0f, 1.8f, -44.0f)
            },
            new SportQuickOption
            {
                BadgeTr = "2v2  ·  +85 ₺",
                BadgeEn = "2v2  ·  +85 ₺",
                NameTr = "Plaj Voleybolu",
                NameEn = "2v2 Sand Volleyball",
                TagTr = "Manşet · Pas · Smaç\nİlk 5 puan kazanır",
                TagEn = "Dig · Set · Spike\nFirst to 5 points wins",
                ExitPos = new Vector3(59.0f, 0.12f, -34.0f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(52.0f, 5.8f, -33.5f),
                PreviewLookAt = new Vector3(59.0f, 1.8f, -44.0f)
            },
            new SportQuickOption
            {
                BadgeTr = "50m  ·  +95 ₺",
                BadgeEn = "50m  ·  +95 ₺",
                NameTr = "Yüzme Yarışı & Dalış",
                NameEn = "50m Swim Race",
                TagTr = "3 Kulvar · Kulaç Ritmi · Takla\nTramplen Dalışı",
                TagEn = "3 Lanes · Stroke Rhythm · Flip Turn\nSpringboard Dive",
                ExitPos = new Vector3(16.0f, 1.45f, -34.0f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(16.0f, 7.6f, -26.0f),
                PreviewLookAt = new Vector3(16.0f, 1.2f, -40.0f)
            }
        };

        public bool IsOpen { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool IsPauseMode { get; private set; }
        public MenuDrawer ActiveDrawer { get; private set; } = MenuDrawer.None;

        private readonly object _modalLockToken = new object();

        // Procedural UI sprites & icons (cleaned up on destroy)
        private Sprite _roundedCardSprite;
        private Sprite _roundedOutlineSprite;
        private Sprite _pillSprite;
        private Sprite _pillOutlineSprite;
        private Sprite _leftVignetteSprite;
        private Sprite _bottomVignetteSprite;
        private Sprite _iconPlaySprite;
        private Sprite _iconPinSprite;
        private Sprite _iconSlidersSprite;
        private Sprite _iconSparkleSprite;
        private Sprite _iconMapSprite;
        private Sprite _iconMenuBarsSprite;
        private Sprite _brandIconSprite;
        private bool _brandIconCreatedAtRuntime;
        private Font _uiFont;

        // Cached scene refs
        private ThirdPersonCameraRig _cameraRig;
        private Camera _mainCamera;
        private GameHUDController _hud;

        // Canvas & layout roots
        private GameObject _canvasObj;
        private Canvas _menuCanvas;
        private CanvasGroup _menuCanvasGroup;
        private RectTransform _leftRailRect;
        private GameObject _rightInfoRoot;
        private CanvasGroup _rightInfoGroup;
        private GameObject _drawerHost;
        private CanvasGroup _drawerGroup;
        private GameObject _districtsPanel;
        private GameObject _settingsPanel;
        private GameObject _sportsPanel;

        // Left rail widgets
        private UnityEngine.UI.Text _eyebrowText;
        private UnityEngine.UI.Text _brandTitleText;
        private UnityEngine.UI.Text _brandSubtitleText;
        private GameObject _pauseBadgeObj;
        private UnityEngine.UI.Text _pauseBadgeText;
        private UnityEngine.UI.Button _primaryPlayBtn;
        private UnityEngine.UI.Text _primaryPlayLabel;
        private UnityEngine.UI.Text _primaryPlaySubLabel;
        private UnityEngine.UI.Button _navDistrictsBtn;
        private UnityEngine.UI.Image _navDistrictsBg;
        private UnityEngine.UI.Text _navDistrictsLabel;
        private UnityEngine.UI.Button _navSportsBtn;
        private UnityEngine.UI.Image _navSportsBg;
        private UnityEngine.UI.Text _navSportsLabel;
        private UnityEngine.UI.Button _navSettingsBtn;
        private UnityEngine.UI.Image _navSettingsBg;
        private UnityEngine.UI.Text _navSettingsLabel;
        private UnityEngine.UI.Button _navWhatsNewBtn;
        private UnityEngine.UI.Text _navWhatsNewLabel;
        private UnityEngine.UI.Button _navGuideBtn;
        private UnityEngine.UI.Text _navGuideLabel;
        private UnityEngine.UI.Text _footerText;

        // Top-right vista & weather pill
        private UnityEngine.UI.Text _liveClockText;
        private UnityEngine.UI.Text _liveWeatherText;
        private UnityEngine.UI.Text _vistaNameText;

        // Bottom-right world profile card
        private UnityEngine.UI.Text _profileHeaderText;
        private UnityEngine.UI.Text _rankBadgeText;
        private UnityEngine.UI.Image _rankProgressFill;
        private UnityEngine.UI.Text _statCashLabel;
        private UnityEngine.UI.Text _statCashValue;
        private UnityEngine.UI.Text _statMissionsLabel;
        private UnityEngine.UI.Text _statMissionsValue;
        private UnityEngine.UI.Text _statLandmarksLabel;
        private UnityEngine.UI.Text _statLandmarksValue;
        private UnityEngine.UI.Text _statMemoriesLabel;
        private UnityEngine.UI.Text _statMemoriesValue;

        // Districts drawer widgets
        private UnityEngine.UI.Text _districtsHeaderText;
        private UnityEngine.UI.Text _districtsHintText;
        private readonly List<UnityEngine.UI.Image> _districtCardBgs = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Image> _districtCardBorders = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Text> _districtTitleTexts = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Text> _districtTagTexts = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Text> _districtBadgeTexts = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Image> _districtBadgeBgs = new List<UnityEngine.UI.Image>();
        private UnityEngine.UI.Button _spawnDistrictBtn;
        private UnityEngine.UI.Text _spawnDistrictBtnLabel;
        private int _selectedDistrictIndex;

        // Sports Quick-Play drawer widgets
        private UnityEngine.UI.Text _sportsHeaderText;
        private UnityEngine.UI.Text _sportsHintText;
        private readonly List<UnityEngine.UI.Image> _sportCardBgs = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Image> _sportCardBorders = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Text> _sportTitleTexts = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Text> _sportTagTexts = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Text> _sportBadgeTexts = new List<UnityEngine.UI.Text>();
        private UnityEngine.UI.Button _startSportBtn;
        private UnityEngine.UI.Text _startSportBtnLabel;
        private int _selectedSportIndex;

        // Settings drawer widgets
        private UnityEngine.UI.Text _settingsHeaderText;
        private UnityEngine.UI.Text _setGfxCaption;
        private UnityEngine.UI.Image _gfxLowBg;
        private UnityEngine.UI.Text _gfxLowText;
        private UnityEngine.UI.Image _gfxMedBg;
        private UnityEngine.UI.Text _gfxMedText;
        private UnityEngine.UI.Text _setLangCaption;
        private UnityEngine.UI.Image _langTrBg;
        private UnityEngine.UI.Image _langEnBg;
        private UnityEngine.UI.Text _setAudioCaption;
        private UnityEngine.UI.Slider _volumeSlider;
        private UnityEngine.UI.Text _volumeValueText;
        private UnityEngine.UI.Text _setSensCaption;
        private UnityEngine.UI.Image _sensLowBg;
        private UnityEngine.UI.Text _sensLowText;
        private UnityEngine.UI.Image _sensMidBg;
        private UnityEngine.UI.Text _sensMidText;
        private UnityEngine.UI.Image _sensHighBg;
        private UnityEngine.UI.Text _sensHighText;
        private UnityEngine.UI.Text _setTimeCaption;
        private readonly UnityEngine.UI.Image[] _timeChipBgs = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Text[] _timeChipTexts = new UnityEngine.UI.Text[4];
        private static readonly float[] TimePresetHours = { 9.0f, 13.0f, 18.75f, 21.5f };
        private UnityEngine.UI.Text _setFpsCaption;
        private UnityEngine.UI.Image _fpsOffBg;
        private UnityEngine.UI.Text _fpsOffText;
        private UnityEngine.UI.Image _fpsOnBg;
        private UnityEngine.UI.Text _fpsOnText;
        private GameObject _resetInitialRow;
        private UnityEngine.UI.Text _resetInitialLabel;
        private GameObject _resetConfirmRow;
        private UnityEngine.UI.Text _resetConfirmLabel;
        private UnityEngine.UI.Text _resetCancelLabel;

        // HUD Pause Button
        private GameObject _hudMenuButtonObj;
        private UnityEngine.UI.Text _hudMenuButtonLabel;

        // Camera tour state
        private int _currentVistaIndex;
        private float _vistaTimer;
        private float _orbitClock;
        private bool _usingCustomPreview;
        private Vector3 _customPreviewPos;
        private Vector3 _customPreviewLookAt;
        private string _customPreviewNameTr;
        private string _customPreviewNameEn;
        private bool _usingPlayerOrbitVista;
        private Vector3 _playerOrbitBasePos;
        private Vector3 _playerOrbitLookAt;
        private Vector3 _camSmoothPos;
        private Vector3 _camSmoothLookAt;
        private float _camSmoothFov = 55f;
        private bool _camInitialized;
        private float _snapshotRefreshTimer;
        private Coroutine _transitionRoutine;
        private Coroutine _drawerAnimRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<MainMenuController>() != null) return;

            GameObject go = new GameObject("OurWorld_MainMenuController");
            go.AddComponent<MainMenuController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            float savedVol = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMasterVolumeKey, 1f));
            AudioListener.volume = savedVol;
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            _mainCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            _hud = FindAnyObjectByType<GameHUDController>();

            BuildProceduralSprites();
            BuildMenuCanvas();
            InstallHudMenuButton();

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnMenuTogglePressed += HandleEscapePressed;
            }
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleGraphicsChanged;
            }

            ResponsiveTabletLayout tabletLayout = FindAnyObjectByType<ResponsiveTabletLayout>();
            if (tabletLayout != null)
            {
                tabletLayout.ApplyResponsiveLayout(force: true);
            }

            OpenMenu(asPauseMenu: false, instant: true);
        }

        private void OnDestroy()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnMenuTogglePressed -= HandleEscapePressed;
            }
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleGraphicsChanged;
            }

            ReleaseGameplayLock();
            if (_cameraRig != null) _cameraRig.ExternalCameraControl = false;

            DestroySpriteAndTex(_roundedCardSprite);
            DestroySpriteAndTex(_roundedOutlineSprite);
            DestroySpriteAndTex(_pillSprite);
            DestroySpriteAndTex(_pillOutlineSprite);
            DestroySpriteAndTex(_leftVignetteSprite);
            DestroySpriteAndTex(_bottomVignetteSprite);
            DestroySpriteAndTex(_iconPlaySprite);
            DestroySpriteAndTex(_iconPinSprite);
            DestroySpriteAndTex(_iconSlidersSprite);
            DestroySpriteAndTex(_iconSparkleSprite);
            DestroySpriteAndTex(_iconMapSprite);
            DestroySpriteAndTex(_iconMenuBarsSprite);
            if (_brandIconCreatedAtRuntime && _brandIconSprite != null) Destroy(_brandIconSprite);

            if (Instance == this) Instance = null;
        }

        private static void DestroySpriteAndTex(Sprite s)
        {
            if (s == null) return;
            Texture2D tex = s.texture;
            Destroy(s);
            if (tex != null) Destroy(tex);
        }

        private void Update()
        {
            if (!IsOpen || IsTransitioning) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 0.25f) dt = 0.016f;

            _orbitClock += dt;

            if (!_usingCustomPreview && !_usingPlayerOrbitVista && ActiveDrawer == MenuDrawer.None)
            {
                _vistaTimer += dt;
                if (_vistaTimer >= 12f)
                {
                    _vistaTimer = 0f;
                    _currentVistaIndex = (_currentVistaIndex + 1) % ScenicVistas.Length;
                    UpdateVistaLabel();
                    SnapCameraToActiveVista();
                }
            }

            UpdateScenicCamera(dt);

            _snapshotRefreshTimer += dt;
            if (_snapshotRefreshTimer >= 0.5f)
            {
                _snapshotRefreshTimer = 0f;
                RefreshWorldSnapshot();
            }
        }

        private void HandleEscapePressed()
        {
            if (IsTransitioning) return;
            if (AppUpdateOverlay.IsShowing || WhatsNewOverlay.IsShowing) return;

            if (IsOpen)
            {
                if (ActiveDrawer != MenuDrawer.None)
                {
                    OpenDrawer(MenuDrawer.None);
                }
                else if (IsPauseMode)
                {
                    ResumeOrEnterWorld();
                }
                return;
            }

            if (PhonePanelController.Instance != null && PhonePanelController.Instance.IsOpen) return;
            if (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            OpenMenu(asPauseMenu: true, instant: false);
        }

        private void HandleLanguageChanged(string _)
        {
            RefreshLocalizedText();
            RefreshSettingsUiState();
            RefreshDistrictCards();
            RefreshSportCards();
            RefreshWorldSnapshot();
        }

        private void HandleGraphicsChanged(GraphicsTier _)
        {
            RefreshSettingsUiState();
        }

        // =====================================================================
        // PUBLIC MENU STATE API
        // =====================================================================

        public void OpenMenu(bool asPauseMenu, bool instant = false)
        {
            if (_canvasObj == null) return;
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
            if (_drawerAnimRoutine != null)
            {
                StopCoroutine(_drawerAnimRoutine);
                _drawerAnimRoutine = null;
            }

            IsPauseMode = asPauseMenu;
            IsOpen = true;
            IsTransitioning = false;
            ActiveDrawer = MenuDrawer.None;
            _usingCustomPreview = false;
            _vistaTimer = 0f;

            if (asPauseMenu && SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.ClearAllTouchInputs();
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(_modalLockToken, true);
            }

            if (_hud != null)
            {
                _hud.SetHudVisible(false, instant ? 0f : 0.22f);
            }

            if (_cameraRig != null)
            {
                _cameraRig.ExternalCameraControl = true;
            }

            ConfigureInitialVistaForOpen(asPauseMenu);

            _canvasObj.SetActive(true);
            _drawerHost.SetActive(false);
            _drawerGroup.alpha = 0f;
            _districtsPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            if (_sportsPanel != null) _sportsPanel.SetActive(false);
            if (_rightInfoRoot != null)
            {
                _rightInfoRoot.SetActive(true);
                if (_rightInfoGroup != null) _rightInfoGroup.alpha = 1f;
            }
            ResetConfirmRowVisibility(confirming: false);

            RefreshLocalizedText();
            RefreshWorldSnapshot();
            RefreshSettingsUiState();
            RefreshDistrictCards();
            RefreshSportCards();
            UpdateNavHighlight();

            if (instant)
            {
                _menuCanvasGroup.alpha = 1f;
                _menuCanvasGroup.interactable = true;
                _menuCanvasGroup.blocksRaycasts = true;
                if (_leftRailRect != null) _leftRailRect.anchoredPosition = Vector2.zero;
                SnapCameraToActiveVista();
            }
            else
            {
                _transitionRoutine = StartCoroutine(FadeMenuInRoutine());
            }
        }

        public void ResumeOrEnterWorld()
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();
            StartEnterWorldTransition(teleportToDistrictIndex: -1, openGuideAfter: false);
        }

        public void EnterWorldAtDistrict(int districtIndex)
        {
            if (!IsOpen || IsTransitioning) return;
            if (districtIndex < 0 || districtIndex >= Districts.Length) return;
            GameFeelAudio.PlayUiClick();
            StartEnterWorldTransition(teleportToDistrictIndex: districtIndex, openGuideAfter: false);
        }

        public void EnterWorldAndStartSport(int sportIndex)
        {
            if (!IsOpen || IsTransitioning) return;
            if (sportIndex < 0 || sportIndex >= QuickSports.Length) return;
            GameFeelAudio.PlayUiClick();
            StartEnterWorldTransition(teleportToDistrictIndex: -1, openGuideAfter: false, launchSportIndex: sportIndex);
        }

        public void OpenDrawer(MenuDrawer drawer, bool instant = false)
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();

            if (ActiveDrawer == drawer && !instant)
            {
                drawer = MenuDrawer.None;
            }

            ActiveDrawer = drawer;
            ResetConfirmRowVisibility(confirming: false);

            if (drawer == MenuDrawer.Districts)
            {
                RefreshDistrictCards();
                PreviewSelectedDistrict(snapCamera: true);
            }
            else if (drawer == MenuDrawer.Sports)
            {
                RefreshSportCards();
                PreviewSelectedSport(snapCamera: true);
            }
            else
            {
                bool wasCustom = _usingCustomPreview;
                _usingCustomPreview = false;
                UpdateVistaLabel();
                if (wasCustom) SnapCameraToActiveVista();
            }

            if (drawer == MenuDrawer.Settings)
            {
                RefreshSettingsUiState();
            }

            UpdateNavHighlight();

            if (_drawerAnimRoutine != null)
            {
                StopCoroutine(_drawerAnimRoutine);
                _drawerAnimRoutine = null;
            }

            if (instant)
            {
                ApplyDrawerVisibilityImmediate(drawer);
            }
            else
            {
                _drawerAnimRoutine = StartCoroutine(AnimateDrawerRoutine(drawer));
            }
        }

        public void CycleVista(int direction)
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();
            _usingCustomPreview = false;
            _usingPlayerOrbitVista = false;
            _vistaTimer = 0f;
            int len = ScenicVistas.Length;
            _currentVistaIndex = (_currentVistaIndex + direction + len) % len;
            UpdateVistaLabel();
            SnapCameraToActiveVista();
        }

        public void SelectVista(int index, bool instant = false)
        {
            _usingCustomPreview = false;
            _usingPlayerOrbitVista = false;
            _vistaTimer = 0f;
            _currentVistaIndex = Mathf.Clamp(index, 0, ScenicVistas.Length - 1);
            UpdateVistaLabel();
            if (instant) SnapCameraToActiveVista();
        }

        public void SetPreviewTimePreset(float hour)
        {
            GameFeelAudio.PlayUiClick();
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.SetTimeOfDay(hour);
            }
            RefreshSettingsUiState();
            RefreshWorldSnapshot();
        }

        // =====================================================================
        // CAMERA TOUR & SEAMLESS SWOOP TRANSITION
        // =====================================================================

        private void ConfigureInitialVistaForOpen(bool asPauseMenu)
        {
            _usingPlayerOrbitVista = false;

            if (asPauseMenu && PlayerModeController.Instance != null)
            {
                Vector3 focus = PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.35f;
                bool indoors = (_cameraRig != null && _cameraRig.IsIndoors) ||
                               Physics.Raycast(focus, Vector3.up, 4.5f, ~0, QueryTriggerInteraction.Ignore);
                if (!indoors)
                {
                    _usingPlayerOrbitVista = true;
                    Vector3 camBack = _mainCamera != null ? -_mainCamera.transform.forward : Vector3.back;
                    camBack.y = 0f;
                    if (camBack.sqrMagnitude < 0.01f) camBack = Vector3.back;
                    camBack.Normalize();
                    Vector3 camRight = Vector3.Cross(Vector3.up, -camBack).normalized;
                    // Frame the player slightly to the right of center so they sit cleanly in the open window
                    _playerOrbitLookAt = focus - camRight * 1.4f;
                    _playerOrbitBasePos = focus + camBack * 10.0f + camRight * 1.8f + Vector3.up * 5.2f;
                    _camInitialized = false;
                    return;
                }
            }

            if (!asPauseMenu)
            {
                _currentVistaIndex = 0;
            }
            _camInitialized = false;
        }

        private void GetDesiredMenuCameraTarget(out Vector3 pos, out Vector3 lookAt, out float fov)
        {
            if (_usingCustomPreview)
            {
                Vector3 forward = (_customPreviewLookAt - _customPreviewPos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                pos = _customPreviewPos
                      + right * (Mathf.Sin(_orbitClock * 0.24f) * 0.95f)
                      + Vector3.up * (Mathf.Cos(_orbitClock * 0.19f) * 0.28f);
                lookAt = _customPreviewLookAt;
                fov = 54f;
                return;
            }

            if (_usingPlayerOrbitVista)
            {
                float angle = Mathf.Sin(_orbitClock * 0.16f) * 16f;
                Vector3 offset = _playerOrbitBasePos - _playerOrbitLookAt;
                Vector3 rotated = Quaternion.Euler(0f, angle, 0f) * offset;
                pos = _playerOrbitLookAt + rotated + Vector3.up * (Mathf.Sin(_orbitClock * 0.25f) * 0.28f);
                lookAt = _playerOrbitLookAt;
                fov = 55f;
                return;
            }

            ScenicVista v = ScenicVistas[_currentVistaIndex];
            Vector3 vFwd = (v.LookAt - v.BasePos).normalized;
            Vector3 vRight = Vector3.Cross(Vector3.up, vFwd).normalized;
            float t = _orbitClock * v.SwaySpeed;
            pos = v.BasePos
                  + vRight * (Mathf.Sin(t) * v.SwayRadius)
                  + Vector3.up * (Mathf.Sin(t * 0.72f) * 0.35f);
            lookAt = v.LookAt;
            fov = v.Fov;
        }

        private void SnapCameraToActiveVista()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            GetDesiredMenuCameraTarget(out Vector3 pos, out Vector3 lookAt, out float fov);
            _camSmoothPos = pos;
            _camSmoothLookAt = lookAt;
            _camSmoothFov = fov;
            _camInitialized = true;

            _mainCamera.transform.position = pos;
            _mainCamera.transform.LookAt(lookAt);
            _mainCamera.fieldOfView = fov;
        }

        private void UpdateScenicCamera(float dt)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            GetDesiredMenuCameraTarget(out Vector3 targetPos, out Vector3 targetLook, out float targetFov);

            // If target is far away (vista switch), snap cleanly instead of flying through buildings
            if (!_camInitialized || Vector3.Distance(_camSmoothPos, targetPos) > 14f)
            {
                _camSmoothPos = targetPos;
                _camSmoothLookAt = targetLook;
                _camSmoothFov = targetFov;
                _camInitialized = true;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-3.2f * dt);
                _camSmoothPos = Vector3.Lerp(_camSmoothPos, targetPos, blend);
                _camSmoothLookAt = Vector3.Lerp(_camSmoothLookAt, targetLook, blend);
                _camSmoothFov = Mathf.Lerp(_camSmoothFov, targetFov, blend);
            }

            _mainCamera.transform.position = _camSmoothPos;
            Vector3 dir = _camSmoothLookAt - _camSmoothPos;
            if (dir.sqrMagnitude > 0.001f)
            {
                _mainCamera.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            _mainCamera.fieldOfView = _camSmoothFov;
        }

        private IEnumerator FadeMenuInRoutine()
        {
            _menuCanvasGroup.alpha = 0f;
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = true;

            const float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _menuCanvasGroup.alpha = k;
                if (_leftRailRect != null)
                {
                    _leftRailRect.anchoredPosition = new Vector2(Mathf.Lerp(-36f, 0f, k), 0f);
                }
                yield return null;
            }

            _menuCanvasGroup.alpha = 1f;
            _menuCanvasGroup.interactable = true;
            if (_leftRailRect != null) _leftRailRect.anchoredPosition = Vector2.zero;
            _transitionRoutine = null;
        }

        private void StartEnterWorldTransition(int teleportToDistrictIndex, bool openGuideAfter, int launchSportIndex = -1)
        {
            if (_drawerAnimRoutine != null)
            {
                StopCoroutine(_drawerAnimRoutine);
                _drawerAnimRoutine = null;
            }
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(EnterWorldSwoopRoutine(teleportToDistrictIndex, openGuideAfter, launchSportIndex));
        }

        private IEnumerator EnterWorldSwoopRoutine(int districtIndex, bool openGuideAfter, int launchSportIndex = -1)
        {
            IsTransitioning = true;
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;

            string arrivalToast = null;
            if (launchSportIndex >= 0 && launchSportIndex < QuickSports.Length)
            {
                SportQuickOption sport = QuickSports[launchSportIndex];
                LaunchQuickSport(launchSportIndex, sport);
                bool isEn = IsEnglish();
                arrivalToast = isEn ? $"Match Started: {sport.NameEn}" : $"Maç Başladı: {sport.NameTr}";
            }
            else if (districtIndex >= 0 && districtIndex < Districts.Length)
            {
                DistrictOption opt = Districts[districtIndex];
                TeleportPlayerToDistrict(opt);
                bool isEn = IsEnglish();
                arrivalToast = isEn ? $"Arrived: {opt.NameEn}" : $"Başlangıç Noktası: {opt.NameTr}";
            }

            if (_cameraRig != null && launchSportIndex < 0)
            {
                _cameraRig.SnapBehindTargetImmediate();
            }

            Vector3 startPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;
            Quaternion startRot = _mainCamera != null ? _mainCamera.transform.rotation : Quaternion.identity;
            float startFov = _mainCamera != null ? _mainCamera.fieldOfView : 56f;

            Vector3 endPos = startPos;
            Quaternion endRot = startRot;
            float endFov = 58f;
            bool hasTargetPose = _cameraRig != null && _cameraRig.TryGetGameplayPose(out endPos, out endRot, out endFov);

            // If coming from a faraway landmark vista, lift cleanly over rooftops into a crisp aerial dive
            float travelDist = hasTargetPose ? Vector3.Distance(startPos, endPos) : 0f;
            if (hasTargetPose && travelDist > 26f)
            {
                // Start the dive from an elevated neighborhood overlook above the player so it never clips through blocks
                Vector3 diveOffset = (endPos - (endPos + (endRot * Vector3.forward) * 4.5f)).normalized * 12f;
                startPos = endPos + diveOffset + Vector3.up * 11f;
                startRot = Quaternion.LookRotation(((endPos + (endRot * Vector3.forward) * 4f) - startPos).normalized, Vector3.up);
                startFov = 52f;
            }

            float swoopDuration = hasTargetPose ? (openGuideAfter ? 0.32f : 0.72f) : 0.24f;
            float fadeDuration = 0.24f;
            float arcLift = hasTargetPose ? Mathf.Clamp(Vector3.Distance(startPos, endPos) * 0.12f, 0.35f, 3.2f) : 0f;

            float elapsed = 0f;
            while (elapsed < swoopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / swoopDuration);
                float smooth = u * u * (3f - 2f * u);

                float uiAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                _menuCanvasGroup.alpha = uiAlpha;
                if (_leftRailRect != null)
                {
                    _leftRailRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, -32f, 1f - uiAlpha), 0f);
                }

                if (_mainCamera != null && hasTargetPose)
                {
                    Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
                    pos.y += Mathf.Sin(smooth * Mathf.PI) * arcLift;
                    _mainCamera.transform.position = pos;
                    _mainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, smooth);
                    _mainCamera.fieldOfView = Mathf.Lerp(startFov, endFov, smooth);
                }

                yield return null;
            }

            _menuCanvasGroup.alpha = 0f;
            _canvasObj.SetActive(false);

            if (_cameraRig != null)
            {
                _cameraRig.ExternalCameraControl = false;
            }

            IsOpen = false;
            IsTransitioning = false;
            ActiveDrawer = MenuDrawer.None;
            ReleaseGameplayLock();

            if (_hud != null)
            {
                _hud.SetHudVisible(true, 0.24f);
            }

            if (!string.IsNullOrEmpty(arrivalToast) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(arrivalToast);
                GameFeelAudio.SpawnBurst(
                    PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.2f,
                    AccentTeal,
                    count: 10,
                    spread: 0.6f);
            }

            if (openGuideAfter && PhonePanelController.Instance != null && !PhonePanelController.Instance.IsOpen)
            {
                PhonePanelController.Instance.Open();
            }

            _transitionRoutine = null;
        }

        private static void LaunchQuickSport(int sportIndex, SportQuickOption sport)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;
            Quaternion exitRot = Quaternion.Euler(0f, sport.ExitYaw, 0f);

            switch (sportIndex)
            {
                case 0:
                {
                    TennisCourt tennis = FindAnyObjectByType<TennisCourt>();
                    if (tennis != null) mode.StartTennisMatch(tennis, sport.ExitPos, exitRot);
                    break;
                }
                case 1:
                {
                    LiveFootballMatch football = FindAnyObjectByType<LiveFootballMatch>();
                    if (football != null) mode.StartArenaSport(football, sport.ExitPos, exitRot);
                    break;
                }
                case 2:
                {
                    LiveBasketballMatch basket = FindAnyObjectByType<LiveBasketballMatch>();
                    if (basket != null) mode.StartArenaSport(basket, sport.ExitPos, exitRot);
                    break;
                }
                case 3:
                {
                    LiveVolleyballMatch volley = FindAnyObjectByType<LiveVolleyballMatch>();
                    if (volley != null) mode.StartArenaSport(volley, sport.ExitPos, exitRot);
                    break;
                }
                case 4:
                {
                    LiveSwimRace swimRace = SwimWorldInstaller.ActiveSwimRace ?? FindAnyObjectByType<LiveSwimRace>();
                    if (swimRace != null) mode.StartArenaSport(swimRace, sport.ExitPos, exitRot);
                    break;
                }
            }
        }

        private static void TeleportPlayerToDistrict(DistrictOption opt)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;

            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();

            if (mode.CurrentMode != ControlMode.OnFoot)
            {
                mode.SetOnFootMode(snapCamera: false);
            }

            if (mode.FootController != null)
            {
                mode.FootController.TeleportTo(opt.SpawnPos, Quaternion.Euler(0f, opt.SpawnYaw, 0f));
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
        }

        private void ReleaseGameplayLock()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(_modalLockToken, false);
            }
        }

        // =====================================================================
        // HUD PAUSE / MENU BUTTON INTEGRATION
        // =====================================================================

        private void InstallHudMenuButton()
        {
            if (_hud == null) return;

            _hud.SetLegacyTopRightUtilityVisible(false);

            Transform safeArea = _hud.transform.Find("SafeAreaRoot");
            Transform parent = safeArea != null ? safeArea : _hud.transform;

            if (parent.Find("Btn_MainMenu") != null) return;

            GameObject btnGo = CreateRoundedPanel("Btn_MainMenu", parent, _roundedCardSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -110f), new Vector2(178f, 60f),
                new Color(0.055f, 0.082f, 0.130f, 0.92f), addBorder: true);
            UnityEngine.UI.Shadow btnSh = btnGo.AddComponent<UnityEngine.UI.Shadow>();
            btnSh.effectColor = new Color(0f, 0f, 0f, 0.42f);
            btnSh.effectDistance = new Vector2(0f, -3f);

            UnityEngine.UI.Image bg = btnGo.GetComponent<UnityEngine.UI.Image>();
            bg.raycastTarget = true;

            UnityEngine.UI.Button btn = btnGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                if (IsOpen || IsTransitioning) return;
                if (PhonePanelController.Instance != null && PhonePanelController.Instance.IsOpen) return;
                GameFeelAudio.PlayUiClick();
                OpenMenu(asPauseMenu: true, instant: false);
            });

            UiButtonFeedback fb = btnGo.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);

            // Crisp procedural 3-bar hamburger icon
            GameObject iconGo = new GameObject("MenuIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(btnGo.transform, false);
            RectTransform irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(20f, 0f);
            irt.sizeDelta = new Vector2(28f, 28f);
            UnityEngine.UI.Image iconImg = iconGo.GetComponent<UnityEngine.UI.Image>();
            iconImg.sprite = _iconMenuBarsSprite;
            iconImg.color = AccentTeal;
            iconImg.raycastTarget = false;

            _hudMenuButtonLabel = CreateText("Label", btnGo.transform, "Menü", 22, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform lrt = _hudMenuButtonLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(60f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);
            _hudMenuButtonObj = btnGo;
        }

        // =====================================================================
        // UI HIERARCHY BUILDER (uGUI · 1920x1080 Reference · Safe Area + Tablet)
        // =====================================================================

        private void BuildMenuCanvas()
        {
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 24);

            _canvasObj = new GameObject("Canvas_OurWorld_MainMenu");
            _canvasObj.transform.SetParent(transform, false);

            _menuCanvas = _canvasObj.AddComponent<Canvas>();
            _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _menuCanvas.sortingOrder = 300;

            CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObj.AddComponent<GraphicRaycaster>();
            _menuCanvasGroup = _canvasObj.AddComponent<CanvasGroup>();

            // Full-bleed atmospheric gradient scrims (seamless edge bleed on notched devices)
            GameObject leftScrim = new GameObject("Scrim_LeftGradient", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            leftScrim.transform.SetParent(_canvasObj.transform, false);
            RectTransform lrt = (RectTransform)leftScrim.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = new Vector2(0.66f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image lImg = leftScrim.GetComponent<UnityEngine.UI.Image>();
            lImg.sprite = _leftVignetteSprite;
            lImg.color = Color.white;
            lImg.raycastTarget = false;

            GameObject bottomScrim = new GameObject("Scrim_BottomGradient", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bottomScrim.transform.SetParent(_canvasObj.transform, false);
            RectTransform brt = (RectTransform)bottomScrim.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = new Vector2(1f, 0.34f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image bImg = bottomScrim.GetComponent<UnityEngine.UI.Image>();
            bImg.sprite = _bottomVignetteSprite;
            bImg.color = Color.white;
            bImg.raycastTarget = false;

            // Safe-Area container for all interactive content
            GameObject safeRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeRoot.transform.SetParent(_canvasObj.transform, false);
            StretchFull((RectTransform)safeRoot.transform, 0f, 0f);
            safeRoot.AddComponent<SafeAreaFitter>();

            BuildLeftHeroRail(safeRoot.transform);
            BuildRightWorldInfo(safeRoot.transform);
            BuildDrawerHost(safeRoot.transform);
        }

        private void BuildLeftHeroRail(Transform parent)
        {
            GameObject rail = new GameObject("LeftHeroRail", typeof(RectTransform));
            rail.transform.SetParent(parent, false);
            _leftRailRect = (RectTransform)rail.transform;
            _leftRailRect.anchorMin = new Vector2(0f, 0f);
            _leftRailRect.anchorMax = new Vector2(0f, 1f);
            _leftRailRect.pivot = new Vector2(0f, 0.5f);
            _leftRailRect.anchoredPosition = Vector2.zero;
            _leftRailRect.sizeDelta = new Vector2(612f, 0f);

            // --- Brand Header (Top-Left) ---
            GameObject header = new GameObject("BrandHeader", typeof(RectTransform));
            header.transform.SetParent(rail.transform, false);
            RectTransform hrt = (RectTransform)header.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(54f, -52f);
            hrt.sizeDelta = new Vector2(-64f, 152f);

            // Official App Icon Emblem with rounded mask and golden frame
            GameObject iconFrame = CreateRoundedPanel("AppIconBadge", header.transform, _roundedCardSprite,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -6f), new Vector2(92f, 92f),
                new Color(0.10f, 0.16f, 0.24f, 0.96f), addBorder: false);

            if (_brandIconSprite != null)
            {
                GameObject maskHolder = CreateRoundedPanel("IconMask", iconFrame.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, Color.white, addBorder: false);
                StretchFull((RectTransform)maskHolder.transform, 4f, 4f);
                UnityEngine.UI.Mask iconMask = maskHolder.AddComponent<UnityEngine.UI.Mask>();
                iconMask.showMaskGraphic = false;

                GameObject iconImgObj = new GameObject("IconImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconImgObj.transform.SetParent(maskHolder.transform, false);
                StretchFull((RectTransform)iconImgObj.transform, 0f, 0f);
                UnityEngine.UI.Image iconImg = iconImgObj.GetComponent<UnityEngine.UI.Image>();
                iconImg.sprite = _brandIconSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                UnityEngine.UI.Text fallbackGlyph = CreateText("IconGlyph", iconFrame.transform, "OW", 34, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
                StretchFull(fallbackGlyph.rectTransform, 0f, 0f);
            }
            AddOutlineOverlay(iconFrame.transform, new Color(0.98f, 0.76f, 0.28f, 0.72f));

            // Eyebrow + Display Title + Subtitle
            _eyebrowText = CreateText("Eyebrow", header.transform, "İSTANBUL  ·  SULTANBEYLİ  ·  HAMİDİYE",
                15, FontStyle.Bold, AccentGold, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_eyebrowText.rectTransform, 112f, -4f, 440f, 24f);

            _brandTitleText = CreateText("BrandTitle", header.transform, "BİZİM DÜNYAMIZ",
                46, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_brandTitleText.rectTransform, 110f, -28f, 450f, 56f);

            _brandSubtitleText = CreateText("BrandSubtitle", header.transform, "Yaşayan Açık Dünya · Mahalle Yaşamı",
                19, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_brandSubtitleText.rectTransform, 112f, -84f, 440f, 28f);

            // Pause Badge (shown only when opened mid-session)
            _pauseBadgeObj = CreateRoundedPanel("PauseBadge", header.transform, _pillSprite,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(112f, -118f), new Vector2(174f, 30f),
                BadgeExploreBg, addBorder: false);
            AddOutlineOverlay(_pauseBadgeObj.transform, new Color(0.98f, 0.76f, 0.28f, 0.72f), _pillOutlineSprite);
            _pauseBadgeText = CreateText("Label", _pauseBadgeObj.transform, "●  DURAKLATILDI", 14, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            StretchFull(_pauseBadgeText.rectTransform, 8f, 0f);
            _pauseBadgeObj.SetActive(false);

            // --- Action Stack ---
            GameObject actionStack = new GameObject("ActionStack", typeof(RectTransform));
            actionStack.transform.SetParent(rail.transform, false);
            RectTransform srt = (RectTransform)actionStack.transform;
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(54f, -46f);
            srt.sizeDelta = new Vector2(516f, 512f);

            VerticalLayoutGroup vlg = actionStack.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 1. Primary Hero CTA Button
            GameObject playGo = CreateRoundedPanel("Btn_PrimaryPlay", actionStack.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(516f, 92f), AccentTeal, addBorder: false);
            playGo.AddComponent<LayoutElement>().preferredHeight = 92f;
            AddOutlineOverlay(playGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.56f));

            UnityEngine.UI.Image playImg = playGo.GetComponent<UnityEngine.UI.Image>();
            playImg.raycastTarget = true;
            _primaryPlayBtn = playGo.AddComponent<UnityEngine.UI.Button>();
            _primaryPlayBtn.targetGraphic = playImg;
            _primaryPlayBtn.onClick.AddListener(ResumeOrEnterWorld);
            UiButtonFeedback playFb = playGo.AddComponent<UiButtonFeedback>();
            playFb.Configure(_primaryPlayBtn, playImg);

            GameObject playGlyphBg = CreateRoundedPanel("PlayGlyphCircle", playGo.transform, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(52f, 52f), new Color(0.03f, 0.16f, 0.16f, 0.36f), addBorder: false);
            CreateCenteredIcon("PlayIcon", playGlyphBg.transform, _iconPlaySprite, 23f, Color.white);

            _primaryPlayLabel = CreateText("Title", playGo.transform, "MAHALLEYE GİR", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_primaryPlayLabel.rectTransform, 90f, -12f, 390f, 36f);

            _primaryPlaySubLabel = CreateText("Sub", playGo.transform, "Selman Sokağı No:48 · Yeni Hayat", 16, FontStyle.Normal, new Color(0.88f, 0.99f, 0.97f, 0.96f), TextAnchor.MiddleLeft);
            PlaceTopLeft(_primaryPlaySubLabel.rectTransform, 90f, -49f, 390f, 26f);

            // 2. Quick-Play Sports & Mini-Games Button
            (_navSportsBtn, _navSportsBg, _navSportsLabel) = CreateMenuNavButton(
                "Btn_Sports", actionStack.transform, _iconPlaySprite, AccentMint, "Spor & Mini Oyunlar",
                () => OpenDrawer(MenuDrawer.Sports));

            // 3. Districts / Fast-Start Button (crisp Pin icon)
            (_navDistrictsBtn, _navDistrictsBg, _navDistrictsLabel) = CreateMenuNavButton(
                "Btn_Districts", actionStack.transform, _iconPinSprite, AccentTeal, "Bölge Seç & Başla",
                () => OpenDrawer(MenuDrawer.Districts));

            // 4. Settings & World Atmosphere Button (crisp Sliders icon)
            (_navSettingsBtn, _navSettingsBg, _navSettingsLabel) = CreateMenuNavButton(
                "Btn_Settings", actionStack.transform, _iconSlidersSprite, AccentGold, "Ayarlar & Dünya",
                () => OpenDrawer(MenuDrawer.Settings));

            // 5. What's New & Community Ideas Button (crisp Sparkle Star icon)
            (_navWhatsNewBtn, _, _navWhatsNewLabel) = CreateMenuNavButton(
                "Btn_WhatsNew", actionStack.transform, _iconSparkleSprite, AccentSky, "Yenilikler & Topluluk",
                HandleOpenWhatsNew);

            // 6. Town Guide / Map Button (visible in Pause mode — crisp Map Grid icon)
            (_navGuideBtn, _, _navGuideLabel) = CreateMenuNavButton(
                "Btn_TownGuide", actionStack.transform, _iconMapSprite, AccentMint, "Mahalle Rehberi & Harita",
                HandleOpenTownGuide);
            _navGuideBtn.gameObject.SetActive(false);

            // --- Footer (Bottom-Left) ---
            string version = string.IsNullOrEmpty(Application.version) ? "1.0.0" : Application.version;
            _footerText = CreateText("FooterVersion", rail.transform, $"Our World v{version}  ·  Doğuş İpeksaç",
                15, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft, addShadow: true);
            RectTransform frt = _footerText.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0f, 0f);
            frt.anchoredPosition = new Vector2(54f, 32f);
            frt.sizeDelta = new Vector2(-64f, 26f);
        }

        private (UnityEngine.UI.Button btn, UnityEngine.UI.Image bg, UnityEngine.UI.Text lbl) CreateMenuNavButton(
            string name, Transform parent, Sprite iconSprite, Color iconAccent, string defaultLabel, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(516f, 64f), PanelGlass, addBorder: true);
            go.AddComponent<LayoutElement>().preferredHeight = 64f;

            UnityEngine.UI.Image bg = go.GetComponent<UnityEngine.UI.Image>();
            bg.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);

            // Distinct tinted icon chip on left
            GameObject chip = CreateRoundedPanel("IconChip", go.transform, _roundedCardSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 0f), new Vector2(44f, 44f),
                new Color(iconAccent.r, iconAccent.g, iconAccent.b, 0.20f), addBorder: false);
            CreateCenteredIcon("Icon", chip.transform, iconSprite, 24f, iconAccent);

            UnityEngine.UI.Text lbl = CreateText("Label", go.transform, defaultLabel, 22, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(74f, 0f);
            lrt.offsetMax = new Vector2(-46f, 0f);

            UnityEngine.UI.Text chev = CreateText("Chevron", go.transform, "›", 28, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            RectTransform crt = chev.rectTransform;
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-18f, 1f);
            crt.sizeDelta = new Vector2(28f, 40f);

            return (btn, bg, lbl);
        }

        private void BuildRightWorldInfo(Transform parent)
        {
            _rightInfoRoot = new GameObject("RightWorldInfo", typeof(RectTransform));
            _rightInfoRoot.transform.SetParent(parent, false);
            StretchFull((RectTransform)_rightInfoRoot.transform, 0f, 0f);
            _rightInfoGroup = _rightInfoRoot.AddComponent<CanvasGroup>();

            // --- Top-Right Live World Clock, Weather & Scenic Camera Switcher ---
            GameObject topPill = CreateRoundedPanel("TopRight_LiveVistaCard", _rightInfoRoot.transform, _roundedCardSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-48f, -44f), new Vector2(548f, 86f), PanelDark, addBorder: true);

            _liveClockText = CreateText("Clock", topPill.transform, "09:00", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_liveClockText.rectTransform, 24f, -12f, 120f, 34f);

            _liveWeatherText = CreateText("Weather", topPill.transform, "Açık & Güneşli", 15, FontStyle.Bold, AccentGold, TextAnchor.MiddleLeft);
            PlaceTopLeft(_liveWeatherText.rectTransform, 24f, -48f, 154f, 24f);

            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            divider.transform.SetParent(topPill.transform, false);
            RectTransform drt = (RectTransform)divider.transform;
            drt.anchorMin = new Vector2(0f, 0.20f);
            drt.anchorMax = new Vector2(0f, 0.80f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(178f, 0f);
            drt.sizeDelta = new Vector2(1.5f, 0f);
            UnityEngine.UI.Image divImg = divider.GetComponent<UnityEngine.UI.Image>();
            divImg.color = BorderSubtle;
            divImg.raycastTarget = false;

            CreateCompactIconButton("Btn_VistaPrev", topPill.transform, "‹", new Vector2(194f, 0f), () => CycleVista(-1));

            _vistaNameText = CreateText("VistaName", topPill.transform, "Fatih Bulvarı · Saat Kulesi", 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            RectTransform vrt = _vistaNameText.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(242f, 8f);
            vrt.offsetMax = new Vector2(-62f, -8f);
            _vistaNameText.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateCompactIconButton("Btn_VistaNext", topPill.transform, "›", new Vector2(488f, 0f), () => CycleVista(1));

            // --- Bottom-Right Living Neighborhood Profile Card ---
            GameObject profileCard = CreateRoundedPanel("BottomRight_WorldProfileCard", _rightInfoRoot.transform, _roundedCardSprite,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-48f, 44f), new Vector2(548f, 238f), PanelDark, addBorder: true);

            // Subtle warm gold top accent highlight
            AddTopAccentStrip(profileCard.transform, AccentGold);

            _profileHeaderText = CreateText("ProfileHeader", profileCard.transform, "MAHALLE PROFİLİN", 15, FontStyle.Bold, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_profileHeaderText.rectTransform, 24f, -20f, 236f, 24f);

            GameObject rankPill = CreateRoundedPanel("RankPill", profileCard.transform, _pillSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, -15f), new Vector2(228f, 34f),
                BadgeExploreBg, addBorder: false);
            AddOutlineOverlay(rankPill.transform, new Color(0.98f, 0.76f, 0.28f, 0.65f), _pillOutlineSprite);
            _rankBadgeText = CreateText("RankLabel", rankPill.transform, "★  Yeni Komşu", 15, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            StretchFull(_rankBadgeText.rectTransform, 10f, 0f);

            GameObject xpTrack = CreateRoundedPanel("RankXpTrack", profileCard.transform, _pillSprite,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(-48f, 8f), new Color(1f, 1f, 1f, 0.11f), addBorder: false);
            GameObject xpFillGo = CreateRoundedPanel("Fill", xpTrack.transform, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, AccentGold, addBorder: false);
            StretchFull((RectTransform)xpFillGo.transform, 0f, 0f);
            _rankProgressFill = xpFillGo.GetComponent<UnityEngine.UI.Image>();
            _rankProgressFill.type = UnityEngine.UI.Image.Type.Filled;
            _rankProgressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _rankProgressFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            _rankProgressFill.fillAmount = 0.15f;

            GameObject gridGo = new GameObject("StatGrid", typeof(RectTransform));
            gridGo.transform.SetParent(profileCard.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(22f, 20f);
            grt.offsetMax = new Vector2(-22f, -80f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(244f, 62f);
            glg.spacing = new Vector2(16f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            (_statCashLabel, _statCashValue) = CreateStatTile(gridGo.transform, "Stat_Cash", "NAKİT", "₺ 250", AccentMint);
            (_statMissionsLabel, _statMissionsValue) = CreateStatTile(gridGo.transform, "Stat_Missions", "GÖREV", "0 / 13", AccentTeal);
            (_statLandmarksLabel, _statLandmarksValue) = CreateStatTile(gridGo.transform, "Stat_Landmarks", "KEŞİF", "0 / 10", AccentSky);
            (_statMemoriesLabel, _statMemoriesValue) = CreateStatTile(gridGo.transform, "Stat_Memories", "HATIRA", "0 / 12", AccentGold);
        }

        private (UnityEngine.UI.Text cap, UnityEngine.UI.Text val) CreateStatTile(
            Transform parent, string name, string caption, string value, Color valueColor)
        {
            GameObject tile = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, CardSurface, addBorder: true);

            UnityEngine.UI.Text cap = CreateText("Caption", tile.transform, caption, 15, FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
            PlaceTopLeft(cap.rectTransform, 16f, -7f, 212f, 20f);

            UnityEngine.UI.Text val = CreateText("Value", tile.transform, value, 24, FontStyle.Bold, valueColor, TextAnchor.MiddleLeft);
            PlaceTopLeft(val.rectTransform, 16f, -29f, 212f, 28f);

            return (cap, val);
        }

        private void CreateCompactIconButton(string name, Transform parent, string glyph, Vector2 leftCenterPos, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                leftCenterPos, new Vector2(44f, 44f), CardSurface, addBorder: true);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, img);

            UnityEngine.UI.Text txt = CreateText("Glyph", go.transform, glyph, 24, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform, 0f, 0f);
        }

        // =====================================================================
        // INLINE OPEN-WORLD DRAWERS (DISTRICTS + SETTINGS)
        // =====================================================================

        private void BuildDrawerHost(Transform parent)
        {
            _drawerHost = CreateRoundedPanel("RightDrawerHost", parent, _roundedCardSprite,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-48f, 0f), new Vector2(724f, 780f), PanelDark, addBorder: true);
            AddTopAccentStrip(_drawerHost.transform, AccentTeal);
            _drawerGroup = _drawerHost.AddComponent<CanvasGroup>();

            BuildDistrictsDrawer(_drawerHost.transform);
            BuildSportsDrawer(_drawerHost.transform);
            BuildSettingsDrawer(_drawerHost.transform);
            _drawerHost.SetActive(false);
        }

        private void BuildSportsDrawer(Transform parent)
        {
            _sportsPanel = new GameObject("SportsPanel", typeof(RectTransform));
            _sportsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_sportsPanel.transform, 0f, 0f);

            _sportsHeaderText = CreateText("Header", _sportsPanel.transform, "SPOR & MİNİ OYUNLAR", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_sportsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            _sportsHintText = CreateText("Hint", _sportsPanel.transform, "İstediğin zaman bir spor seç, canlı sahayı gör ve hemen maça başla!", 17, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_sportsHintText.rectTransform, 32f, -68f, 640f, 28f);

            CreateDrawerCloseButton(_sportsPanel.transform);

            GameObject gridGo = new GameObject("SportsGrid", typeof(RectTransform));
            gridGo.transform.SetParent(_sportsPanel.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(30f, 118f);
            grt.offsetMax = new Vector2(-30f, -118f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(322f, 250f);
            glg.spacing = new Vector2(20f, 20f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            for (int i = 0; i < QuickSports.Length; i++)
            {
                int idx = i;
                SportQuickOption opt = QuickSports[i];
                GameObject card = CreateRoundedPanel($"SportCard_{i}", gridGo.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, CardSurface, addBorder: false);
                UnityEngine.UI.Image bg = card.GetComponent<UnityEngine.UI.Image>();
                bg.raycastTarget = true;

                UnityEngine.UI.Image border = AddOutlineOverlay(card.transform, BorderSubtle);
                UnityEngine.UI.Button btn = card.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectSportCard(idx));

                GameObject badgeGo = CreateRoundedPanel("RewardBadge", card.transform, _pillSprite,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f, -20f), new Vector2(184f, 36f),
                    BadgeFoundBg, addBorder: false);
                UnityEngine.UI.Text badgeLbl = CreateText("BadgeLabel", badgeGo.transform, opt.BadgeTr, 17, FontStyle.Bold, AccentMint, TextAnchor.MiddleCenter);
                StretchFull(badgeLbl.rectTransform, 8f, 0f);

                UnityEngine.UI.Text title = CreateText("Title", card.transform, opt.NameTr, 25, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
                PlaceTopLeft(title.rectTransform, 20f, -72f, 282f, 36f);

                UnityEngine.UI.Text tag = CreateText("Tag", card.transform, opt.TagTr, 18, FontStyle.Normal, TextSecondary, TextAnchor.UpperLeft);
                PlaceTopLeft(tag.rectTransform, 20f, -116f, 282f, 102f);
                tag.horizontalOverflow = HorizontalWrapMode.Wrap;
                tag.lineSpacing = 1.25f;

                _sportCardBgs.Add(bg);
                _sportCardBorders.Add(border);
                _sportTitleTexts.Add(title);
                _sportTagTexts.Add(tag);
                _sportBadgeTexts.Add(badgeLbl);
            }

            GameObject playBtnGo = CreateRoundedPanel("Btn_StartSelectedSport", _sportsPanel.transform, _roundedCardSprite,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(-60f, 68f), AccentTeal, addBorder: false);
            AddOutlineOverlay(playBtnGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.50f));
            UnityEngine.UI.Image sImg = playBtnGo.GetComponent<UnityEngine.UI.Image>();
            sImg.raycastTarget = true;
            _startSportBtn = playBtnGo.AddComponent<UnityEngine.UI.Button>();
            _startSportBtn.targetGraphic = sImg;
            _startSportBtn.onClick.AddListener(() => EnterWorldAndStartSport(_selectedSportIndex));
            UiButtonFeedback sFb = playBtnGo.AddComponent<UiButtonFeedback>();
            sFb.Configure(_startSportBtn, sImg);

            _startSportBtnLabel = CreateText("Label", playBtnGo.transform, "▶   MAÇA BAŞLA", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, addShadow: true);
            StretchFull(_startSportBtnLabel.rectTransform, 16f, 0f);
        }

        private void BuildDistrictsDrawer(Transform parent)
        {
            _districtsPanel = new GameObject("DistrictsPanel", typeof(RectTransform));
            _districtsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_districtsPanel.transform, 0f, 0f);

            _districtsHeaderText = CreateText("Header", _districtsPanel.transform, "BAŞLANGIÇ BÖLGESİ", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_districtsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            _districtsHintText = CreateText("Hint", _districtsPanel.transform, "Bir mahalleye dokun, canlı kameradan incele ve oradan başla.", 17, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_districtsHintText.rectTransform, 32f, -68f, 640f, 28f);

            CreateDrawerCloseButton(_districtsPanel.transform);

            GameObject gridGo = new GameObject("DistrictGrid", typeof(RectTransform));
            gridGo.transform.SetParent(_districtsPanel.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(30f, 114f);
            grt.offsetMax = new Vector2(-30f, -112f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(322f, 128f);
            glg.spacing = new Vector2(20f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            for (int i = 0; i < Districts.Length; i++)
            {
                int idx = i;
                GameObject card = CreateRoundedPanel($"DistrictCard_{i}", gridGo.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, CardSurface, addBorder: false);
                UnityEngine.UI.Image bg = card.GetComponent<UnityEngine.UI.Image>();
                bg.raycastTarget = true;

                UnityEngine.UI.Image border = AddOutlineOverlay(card.transform, BorderSubtle);

                UnityEngine.UI.Button btn = card.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectDistrictCard(idx));

                GameObject badgeGo = CreateRoundedPanel("StatusBadge", card.transform, _pillSprite,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, -12f), new Vector2(142f, 26f),
                    BadgeExploreBg, addBorder: false);
                UnityEngine.UI.Text badgeLbl = CreateText("BadgeLabel", badgeGo.transform, "•  KEŞFET", 14, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
                StretchFull(badgeLbl.rectTransform, 6f, 0f);

                UnityEngine.UI.Text title = CreateText("Title", card.transform, Districts[i].NameTr, 20, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
                PlaceTopLeft(title.rectTransform, 16f, -44f, 290f, 28f);

                UnityEngine.UI.Text tag = CreateText("Tag", card.transform, Districts[i].TagTr, 14, FontStyle.Normal, TextSecondary, TextAnchor.UpperLeft);
                PlaceTopLeft(tag.rectTransform, 16f, -75f, 290f, 44f);
                tag.horizontalOverflow = HorizontalWrapMode.Wrap;

                _districtCardBgs.Add(bg);
                _districtCardBorders.Add(border);
                _districtTitleTexts.Add(title);
                _districtTagTexts.Add(tag);
                _districtBadgeTexts.Add(badgeLbl);
                _districtBadgeBgs.Add(badgeGo.GetComponent<UnityEngine.UI.Image>());
            }

            GameObject spawnBtnGo = CreateRoundedPanel("Btn_SpawnSelectedDistrict", _districtsPanel.transform, _roundedCardSprite,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(-60f, 68f), AccentTeal, addBorder: false);
            AddOutlineOverlay(spawnBtnGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.50f));
            UnityEngine.UI.Image sImg = spawnBtnGo.GetComponent<UnityEngine.UI.Image>();
            sImg.raycastTarget = true;
            _spawnDistrictBtn = spawnBtnGo.AddComponent<UnityEngine.UI.Button>();
            _spawnDistrictBtn.targetGraphic = sImg;
            _spawnDistrictBtn.onClick.AddListener(() => EnterWorldAtDistrict(_selectedDistrictIndex));
            UiButtonFeedback sFb = spawnBtnGo.AddComponent<UiButtonFeedback>();
            sFb.Configure(_spawnDistrictBtn, sImg);

            _spawnDistrictBtnLabel = CreateText("Label", spawnBtnGo.transform, "▶   BURADA BAŞLA", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, addShadow: true);
            StretchFull(_spawnDistrictBtnLabel.rectTransform, 16f, 0f);
        }

        private void BuildSettingsDrawer(Transform parent)
        {
            _settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            _settingsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_settingsPanel.transform, 0f, 0f);

            _settingsHeaderText = CreateText("Header", _settingsPanel.transform, "AYARLAR & ATMOSFER", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_settingsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            CreateDrawerCloseButton(_settingsPanel.transform);

            GameObject listGo = new GameObject("SettingsList", typeof(RectTransform));
            listGo.transform.SetParent(_settingsPanel.transform, false);
            RectTransform lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(30f, 26f);
            lrt.offsetMax = new Vector2(-30f, -86f);

            VerticalLayoutGroup vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 1. Graphics Quality Row
            GameObject gfxRow = CreateSettingRow(listGo.transform, "Row_Graphics", 76f, out _setGfxCaption, "Grafik Kalitesi");
            GameObject gfxSegs = CreateRightSegmentContainer(gfxRow.transform, 290f);
            (_gfxLowBg, _gfxLowText) = CreateSegmentButton(gfxSegs.transform, "Btn_GfxLow", "Düşük", () =>
            {
                GameFeelAudio.PlayUiClick();
                if (GraphicsProfileManager.Instance != null) GraphicsProfileManager.Instance.ApplyTier(GraphicsTier.Low, persist: true);
                RefreshSettingsUiState();
            });
            (_gfxMedBg, _gfxMedText) = CreateSegmentButton(gfxSegs.transform, "Btn_GfxMed", "Orta", () =>
            {
                GameFeelAudio.PlayUiClick();
                if (GraphicsProfileManager.Instance != null) GraphicsProfileManager.Instance.ApplyTier(GraphicsTier.Medium, persist: true);
                RefreshSettingsUiState();
            });

            // 2. Language Row
            GameObject langRow = CreateSettingRow(listGo.transform, "Row_Language", 76f, out _setLangCaption, "Dil");
            GameObject langSegs = CreateRightSegmentContainer(langRow.transform, 290f);
            (_langTrBg, _) = CreateSegmentButton(langSegs.transform, "Btn_LangTR", "Türkçe", () => ApplyLanguage("tr"));
            (_langEnBg, _) = CreateSegmentButton(langSegs.transform, "Btn_LangEN", "English", () => ApplyLanguage("en"));

            // 3. Audio Volume Row
            GameObject volRow = CreateSettingRow(listGo.transform, "Row_Audio", 76f, out _setAudioCaption, "Ses Seviyesi");
            BuildVolumeSlider(volRow.transform);

            // 4. Camera Sensitivity Row
            GameObject sensRow = CreateSettingRow(listGo.transform, "Row_Sensitivity", 76f, out _setSensCaption, "Kamera Hassasiyeti");
            GameObject sensSegs = CreateRightSegmentContainer(sensRow.transform, 356f);
            (_sensLowBg, _sensLowText) = CreateSegmentButton(sensSegs.transform, "Btn_SensLow", "Yavaş", () => ApplySensitivity(0.70f));
            (_sensMidBg, _sensMidText) = CreateSegmentButton(sensSegs.transform, "Btn_SensMid", "Normal", () => ApplySensitivity(1.00f));
            (_sensHighBg, _sensHighText) = CreateSegmentButton(sensSegs.transform, "Btn_SensHigh", "Hızlı", () => ApplySensitivity(1.40f));

            // 5. Time of Day Live Preview Row
            GameObject timeRow = CreateRoundedPanel("Row_TimeOfDay", listGo.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 114f), CardSurface, addBorder: true);
            timeRow.AddComponent<LayoutElement>().preferredHeight = 114f;

            _setTimeCaption = CreateText("Caption", timeRow.transform, "Günün Saati (Canlı Önizleme)", 18, FontStyle.Bold, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_setTimeCaption.rectTransform, 20f, -12f, 520f, 28f);

            GameObject timeGrid = new GameObject("TimeChips", typeof(RectTransform));
            timeGrid.transform.SetParent(timeRow.transform, false);
            RectTransform trt = (RectTransform)timeGrid.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 14f);
            trt.sizeDelta = new Vector2(-40f, 48f);

            HorizontalLayoutGroup thlg = timeGrid.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 10f;
            thlg.childControlWidth = true;
            thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true;
            thlg.childForceExpandHeight = true;

            string[] defaultTimeLabels = { "Sabah", "Öğle", "Akşam", "Gece" };
            for (int i = 0; i < 4; i++)
            {
                float presetHour = TimePresetHours[i];
                (_timeChipBgs[i], _timeChipTexts[i]) = CreateSegmentButton(
                    timeGrid.transform, $"Btn_Time_{i}", defaultTimeLabels[i],
                    () => SetPreviewTimePreset(presetHour));
            }

            // 6. FPS Telemetry Row
            GameObject fpsRow = CreateSettingRow(listGo.transform, "Row_FPS", 74f, out _setFpsCaption, "FPS Göstergesi");
            GameObject fpsSegs = CreateRightSegmentContainer(fpsRow.transform, 250f);
            (_fpsOffBg, _fpsOffText) = CreateSegmentButton(fpsSegs.transform, "Btn_FpsOff", "Kapalı", () => SetTelemetry(false));
            (_fpsOnBg, _fpsOnText) = CreateSegmentButton(fpsSegs.transform, "Btn_FpsOn", "Açık", () => SetTelemetry(true));

            // 7. Safe 2-Step Reset Save / New Game Row
            GameObject resetContainer = new GameObject("Row_ResetSave", typeof(RectTransform));
            resetContainer.transform.SetParent(listGo.transform, false);
            resetContainer.AddComponent<LayoutElement>().preferredHeight = 64f;

            _resetInitialRow = CreateRoundedPanel("Btn_ResetInitial", resetContainer.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DangerSoft, addBorder: false);
            StretchFull((RectTransform)_resetInitialRow.transform, 0f, 0f);
            AddOutlineOverlay(_resetInitialRow.transform, new Color(0.90f, 0.38f, 0.36f, 0.52f));
            UnityEngine.UI.Image riImg = _resetInitialRow.GetComponent<UnityEngine.UI.Image>();
            riImg.raycastTarget = true;
            UnityEngine.UI.Button riBtn = _resetInitialRow.AddComponent<UnityEngine.UI.Button>();
            riBtn.targetGraphic = riImg;
            riBtn.onClick.AddListener(() =>
            {
                GameFeelAudio.PlayUiClick();
                ResetConfirmRowVisibility(confirming: true);
            });
            _resetInitialLabel = CreateText("Label", _resetInitialRow.transform, "Yeni Oyun (Kaydı Sıfırla)", 19, FontStyle.Bold, new Color(1f, 0.82f, 0.82f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_resetInitialLabel.rectTransform, 12f, 0f);

            _resetConfirmRow = new GameObject("ResetConfirmRow", typeof(RectTransform));
            _resetConfirmRow.transform.SetParent(resetContainer.transform, false);
            StretchFull((RectTransform)_resetConfirmRow.transform, 0f, 0f);
            HorizontalLayoutGroup rhlg = _resetConfirmRow.AddComponent<HorizontalLayoutGroup>();
            rhlg.spacing = 12f;
            rhlg.childControlWidth = true;
            rhlg.childControlHeight = true;
            rhlg.childForceExpandWidth = true;
            rhlg.childForceExpandHeight = true;

            GameObject confirmBtnGo = CreateRoundedPanel("Btn_ConfirmReset", _resetConfirmRow.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, DangerRed, addBorder: false);
            UnityEngine.UI.Image cImg = confirmBtnGo.GetComponent<UnityEngine.UI.Image>();
            cImg.raycastTarget = true;
            UnityEngine.UI.Button cBtn = confirmBtnGo.AddComponent<UnityEngine.UI.Button>();
            cBtn.targetGraphic = cImg;
            cBtn.onClick.AddListener(HandleConfirmResetSave);
            _resetConfirmLabel = CreateText("Label", confirmBtnGo.transform, "EMİN MİSİN? SIFIRLA", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_resetConfirmLabel.rectTransform, 8f, 0f);

            GameObject cancelBtnGo = CreateRoundedPanel("Btn_CancelReset", _resetConfirmRow.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, SegInactive, addBorder: true);
            UnityEngine.UI.Image xImg = cancelBtnGo.GetComponent<UnityEngine.UI.Image>();
            xImg.raycastTarget = true;
            UnityEngine.UI.Button xBtn = cancelBtnGo.AddComponent<UnityEngine.UI.Button>();
            xBtn.targetGraphic = xImg;
            xBtn.onClick.AddListener(() =>
            {
                GameFeelAudio.PlayUiClick();
                ResetConfirmRowVisibility(confirming: false);
            });
            _resetCancelLabel = CreateText("Label", cancelBtnGo.transform, "Vazgeç", 19, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(_resetCancelLabel.rectTransform, 8f, 0f);

            _resetConfirmRow.SetActive(false);
        }

        private GameObject CreateSettingRow(Transform parent, string name, float height, out UnityEngine.UI.Text captionOut, string defaultCaption)
        {
            GameObject row = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, height), CardSurface, addBorder: true);
            row.AddComponent<LayoutElement>().preferredHeight = height;

            captionOut = CreateText("Caption", row.transform, defaultCaption, 19, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform crt = captionOut.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0.48f, 1f);
            crt.offsetMin = new Vector2(20f, 0f);
            crt.offsetMax = Vector2.zero;
            return row;
        }

        private GameObject CreateRightSegmentContainer(Transform rowParent, float width)
        {
            GameObject seg = new GameObject("Segments", typeof(RectTransform));
            seg.transform.SetParent(rowParent, false);
            RectTransform srt = (RectTransform)seg.transform;
            srt.anchorMin = new Vector2(1f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-18f, 0f);
            srt.sizeDelta = new Vector2(width, 46f);

            HorizontalLayoutGroup hlg = seg.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return seg;
        }

        private (UnityEngine.UI.Image bg, UnityEngine.UI.Text lbl) CreateSegmentButton(
            Transform parent, string name, string label, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, SegInactive, addBorder: false);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            UnityEngine.UI.Text txt = CreateText("Label", go.transform, label, 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform, 6f, 0f);
            return (img, txt);
        }

        private void BuildVolumeSlider(Transform rowParent)
        {
            GameObject sliderGo = new GameObject("VolumeSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(rowParent, false);
            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(1f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-92f, 0f);
            srt.sizeDelta = new Vector2(236f, 36f);

            // Track background
            CreateRoundedPanel("Background", sliderGo.transform, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 10f), new Color(1f, 1f, 1f, 0.16f), addBorder: false);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0f, 0.5f);
            fart.anchorMax = new Vector2(1f, 0.5f);
            fart.offsetMin = new Vector2(0f, -5f);
            fart.offsetMax = new Vector2(0f, 5f);

            GameObject fillGo = CreateRoundedPanel("Fill", fillArea.transform, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, AccentTeal, addBorder: false);
            StretchFull((RectTransform)fillGo.transform, 0f, 0f);

            // Constrain Handle Slide Area height strictly to 26px so Unity Slider's vertical stretch produces a crisp 26x26 circle
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = (RectTransform)handleArea.transform;
            hart.anchorMin = new Vector2(0f, 0.5f);
            hart.anchorMax = new Vector2(1f, 0.5f);
            hart.pivot = new Vector2(0.5f, 0.5f);
            hart.anchoredPosition = Vector2.zero;
            hart.sizeDelta = new Vector2(-24f, 26f);

            GameObject handleGo = CreateRoundedPanel("Handle", handleArea.transform, _pillSprite,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(26f, 0f), Color.white, addBorder: false);
            UnityEngine.UI.Image handleImg = handleGo.GetComponent<UnityEngine.UI.Image>();
            handleImg.raycastTarget = true;

            _volumeSlider = sliderGo.AddComponent<UnityEngine.UI.Slider>();
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 1f;
            _volumeSlider.fillRect = (RectTransform)fillGo.transform;
            _volumeSlider.handleRect = (RectTransform)handleGo.transform;
            _volumeSlider.targetGraphic = handleImg;
            _volumeSlider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            _volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeSliderChanged);

            _volumeValueText = CreateText("VolumePct", rowParent, $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%", 18, FontStyle.Bold, AccentTeal, TextAnchor.MiddleRight);
            RectTransform vrt = _volumeValueText.rectTransform;
            vrt.anchorMin = new Vector2(1f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = new Vector2(-20f, 0f);
            vrt.sizeDelta = new Vector2(64f, 32f);
        }

        private void CreateDrawerCloseButton(Transform drawerRoot)
        {
            GameObject closeGo = CreateRoundedPanel("Btn_CloseDrawer", drawerRoot, _pillSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -22f), new Vector2(44f, 44f), CardSurface, addBorder: true);
            UnityEngine.UI.Image img = closeGo.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = closeGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OpenDrawer(MenuDrawer.None));
            UiButtonFeedback fb = closeGo.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, img);

            UnityEngine.UI.Text xTxt = CreateText("X", closeGo.transform, "✕", 20, FontStyle.Bold, TextSecondary, TextAnchor.MiddleCenter);
            StretchFull(xTxt.rectTransform, 0f, 0f);
        }

        // =====================================================================
        // INTERACTION HANDLERS & STATE REFRESH
        // =====================================================================

        private void SelectSportCard(int index)
        {
            if (index < 0 || index >= QuickSports.Length) return;
            GameFeelAudio.PlayUiClick();
            _selectedSportIndex = index;
            RefreshSportCards();
            PreviewSelectedSport(snapCamera: true);
        }

        private void PreviewSelectedSport(bool snapCamera)
        {
            if (_selectedSportIndex < 0 || _selectedSportIndex >= QuickSports.Length) return;
            SportQuickOption opt = QuickSports[_selectedSportIndex];
            _usingCustomPreview = true;
            _customPreviewPos = opt.PreviewCamPos;
            _customPreviewLookAt = opt.PreviewLookAt;
            _customPreviewNameTr = opt.NameTr;
            _customPreviewNameEn = opt.NameEn;
            UpdateVistaLabel();
            if (snapCamera) SnapCameraToActiveVista();
        }

        private void SelectDistrictCard(int index)
        {
            if (index < 0 || index >= Districts.Length) return;
            GameFeelAudio.PlayUiClick();
            _selectedDistrictIndex = index;
            RefreshDistrictCards();
            PreviewSelectedDistrict(snapCamera: true);
        }

        private void PreviewSelectedDistrict(bool snapCamera)
        {
            if (_selectedDistrictIndex < 0 || _selectedDistrictIndex >= Districts.Length) return;
            DistrictOption opt = Districts[_selectedDistrictIndex];
            _usingCustomPreview = true;
            _customPreviewPos = opt.PreviewCamPos;
            _customPreviewLookAt = opt.PreviewLookAt;
            _customPreviewNameTr = opt.NameTr;
            _customPreviewNameEn = opt.NameEn;
            UpdateVistaLabel();
            if (snapCamera) SnapCameraToActiveVista();
        }

        private void ApplyLanguage(string langCode)
        {
            GameFeelAudio.PlayUiClick();
            if (LocalizationService.Instance != null && LocalizationService.Instance.LoadLanguage(langCode))
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.SetLanguageCode(langCode);
                }
            }
            RefreshLocalizedText();
            RefreshSettingsUiState();
            RefreshDistrictCards();
            RefreshWorldSnapshot();
        }

        private void HandleVolumeSliderChanged(float value)
        {
            float clamped = Mathf.Clamp01(value);
            AudioListener.volume = clamped;
            PlayerPrefs.SetFloat(PrefMasterVolumeKey, clamped);
            if (_volumeValueText != null)
            {
                _volumeValueText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        private void ApplySensitivity(float mult)
        {
            GameFeelAudio.PlayUiClick();
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetSensitivityMultiplier(mult);
            }
            RefreshSettingsUiState();
        }

        private void SetTelemetry(bool visible)
        {
            GameFeelAudio.PlayUiClick();
            if (_hud != null)
            {
                _hud.SetTelemetryVisible(visible);
            }
            RefreshSettingsUiState();
        }

        private void HandleConfirmResetSave()
        {
            GameFeelAudio.PlayUiClick();
            if (JobManager.Instance != null && JobManager.Instance.HasActiveJob)
            {
                JobManager.Instance.CancelActiveJob(silent: true);
            }
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.ResetProgressAndRestart();
            }
            else if (SaveManager.Instance != null)
            {
                SaveManager.Instance.ResetSave();
            }

            IsPauseMode = false;
            ResetConfirmRowVisibility(confirming: false);
            OpenDrawer(MenuDrawer.None);
            RefreshLocalizedText();
            RefreshWorldSnapshot();
            RefreshDistrictCards();
        }

        private void HandleOpenWhatsNew()
        {
            GameFeelAudio.PlayUiClick();
            if (ReleaseNotesService.Instance != null)
            {
                ReleaseNotesService.Instance.ShowNow(markSeen: false);
            }
        }

        private void HandleOpenTownGuide()
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();
            StartEnterWorldTransition(teleportToDistrictIndex: -1, openGuideAfter: true);
        }

        private void ResetConfirmRowVisibility(bool confirming)
        {
            if (_resetInitialRow != null) _resetInitialRow.SetActive(!confirming);
            if (_resetConfirmRow != null) _resetConfirmRow.SetActive(confirming);
        }

        private void ApplyDrawerVisibilityImmediate(MenuDrawer drawer)
        {
            bool showDrawer = drawer != MenuDrawer.None;
            _districtsPanel.SetActive(drawer == MenuDrawer.Districts);
            if (_sportsPanel != null) _sportsPanel.SetActive(drawer == MenuDrawer.Sports);
            _settingsPanel.SetActive(drawer == MenuDrawer.Settings);
            _drawerHost.SetActive(showDrawer);
            _drawerGroup.alpha = showDrawer ? 1f : 0f;
            ((RectTransform)_drawerHost.transform).anchoredPosition = new Vector2(-48f, 0f);
            if (_rightInfoRoot != null)
            {
                _rightInfoRoot.SetActive(!showDrawer);
                if (_rightInfoGroup != null) _rightInfoGroup.alpha = showDrawer ? 0f : 1f;
            }
        }

        private IEnumerator AnimateDrawerRoutine(MenuDrawer drawer)
        {
            bool showDrawer = drawer != MenuDrawer.None;
            if (showDrawer)
            {
                _districtsPanel.SetActive(drawer == MenuDrawer.Districts);
                if (_sportsPanel != null) _sportsPanel.SetActive(drawer == MenuDrawer.Sports);
                _settingsPanel.SetActive(drawer == MenuDrawer.Settings);
                _drawerHost.SetActive(true);
                _drawerGroup.alpha = 0f;
            }
            else if (_rightInfoRoot != null)
            {
                _rightInfoRoot.SetActive(true);
            }

            RectTransform drt = (RectTransform)_drawerHost.transform;
            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (showDrawer)
                {
                    _drawerGroup.alpha = k;
                    drt.anchoredPosition = new Vector2(Mathf.Lerp(-16f, -48f, k), 0f);
                    if (_rightInfoGroup != null) _rightInfoGroup.alpha = 1f - k;
                }
                else
                {
                    _drawerGroup.alpha = 1f - k;
                    drt.anchoredPosition = new Vector2(Mathf.Lerp(-48f, -16f, k), 0f);
                    if (_rightInfoGroup != null) _rightInfoGroup.alpha = k;
                }
                yield return null;
            }

            ApplyDrawerVisibilityImmediate(drawer);
            _drawerAnimRoutine = null;
        }

        public void RefreshLocalizedText()
        {
            if (_eyebrowText != null) _eyebrowText.text = Tr("ui.menu.eyebrow", "İSTANBUL  ·  SULTANBEYLİ  ·  HAMİDİYE");
            if (_brandTitleText != null) _brandTitleText.text = Tr("ui.menu.title", "BİZİM DÜNYAMIZ");
            if (_brandSubtitleText != null) _brandSubtitleText.text = Tr("ui.menu.subtitle", "Yaşayan Açık Dünya · Mahalle Yaşamı");
            if (_pauseBadgeText != null) _pauseBadgeText.text = $"●  {Tr("ui.menu.pause_badge", "DURAKLATILDI")}";
            if (_pauseBadgeObj != null) _pauseBadgeObj.SetActive(IsPauseMode);

            bool hasProgress = HasReturningProgress();
            if (_primaryPlayLabel != null)
            {
                if (IsPauseMode)
                    _primaryPlayLabel.text = Tr("ui.menu.play_resume", "OYUNA DÖN");
                else if (hasProgress)
                    _primaryPlayLabel.text = Tr("ui.menu.play_continue", "DEVAM ET");
                else
                    _primaryPlayLabel.text = Tr("ui.menu.play_new", "MAHALLEYE GİR");
            }

            if (_navSportsLabel != null) _navSportsLabel.text = Tr("ui.menu.nav_sports", "Spor & Mini Oyunlar");
            if (_navDistrictsLabel != null)
            {
                _navDistrictsLabel.text = IsPauseMode
                    ? Tr("ui.menu.nav_fast_travel", "Bölgeye Işınlan")
                    : Tr("ui.menu.nav_districts", "Bölge Seç & Başla");
            }
            if (_navSettingsLabel != null) _navSettingsLabel.text = Tr("ui.menu.nav_settings", "Ayarlar & Dünya");
            if (_navWhatsNewLabel != null) _navWhatsNewLabel.text = Tr("ui.menu.nav_whatsnew", "Yenilikler & Topluluk");
            if (_navGuideLabel != null) _navGuideLabel.text = Tr("ui.menu.nav_guide", "Mahalle Rehberi & Harita");
            if (_navGuideBtn != null) _navGuideBtn.gameObject.SetActive(IsPauseMode);

            if (_districtsHeaderText != null) _districtsHeaderText.text = Tr("ui.menu.districts_title", "BAŞLANGIÇ BÖLGESİ");
            if (_districtsHintText != null) _districtsHintText.text = Tr("ui.menu.districts_hint", "Bir mahalleye dokun, canlı kameradan incele ve oradan başla.");
            if (_sportsHeaderText != null) _sportsHeaderText.text = Tr("ui.menu.sports_title", "SPOR & MİNİ OYUNLAR");
            if (_sportsHintText != null) _sportsHintText.text = Tr("ui.menu.sports_hint", "İstediğin zaman bir spor seç, canlı sahayı gör ve hemen maça başla!");
            if (_settingsHeaderText != null) _settingsHeaderText.text = Tr("ui.menu.settings_title", "AYARLAR & ATMOSFER");
            if (_setGfxCaption != null) _setGfxCaption.text = Tr("ui.menu.set_graphics", "Grafik Kalitesi");
            if (_gfxLowText != null) _gfxLowText.text = Tr("ui.menu.set_gfx_low", "Düşük");
            if (_gfxMedText != null) _gfxMedText.text = Tr("ui.menu.set_gfx_med", "Orta");
            if (_setLangCaption != null) _setLangCaption.text = Tr("ui.menu.set_language", "Dil");
            if (_setAudioCaption != null) _setAudioCaption.text = Tr("ui.menu.set_audio", "Ses Seviyesi");
            if (_setSensCaption != null) _setSensCaption.text = Tr("ui.menu.set_sens", "Kamera Hassasiyeti");
            if (_sensLowText != null) _sensLowText.text = Tr("ui.menu.set_sens_low", "Yavaş");
            if (_sensMidText != null) _sensMidText.text = Tr("ui.menu.set_sens_mid", "Normal");
            if (_sensHighText != null) _sensHighText.text = Tr("ui.menu.set_sens_high", "Hızlı");
            if (_setTimeCaption != null) _setTimeCaption.text = Tr("ui.menu.set_time", "Günün Saati (Canlı Önizleme)");
            if (_timeChipTexts[0] != null) _timeChipTexts[0].text = Tr("ui.menu.time_morning", "Sabah");
            if (_timeChipTexts[1] != null) _timeChipTexts[1].text = Tr("ui.menu.time_noon", "Öğle");
            if (_timeChipTexts[2] != null) _timeChipTexts[2].text = Tr("ui.menu.time_sunset", "Akşam");
            if (_timeChipTexts[3] != null) _timeChipTexts[3].text = Tr("ui.menu.time_night", "Gece");
            if (_setFpsCaption != null) _setFpsCaption.text = Tr("ui.menu.set_fps", "FPS Göstergesi");
            if (_fpsOffText != null) _fpsOffText.text = Tr("ui.menu.off", "Kapalı");
            if (_fpsOnText != null) _fpsOnText.text = Tr("ui.menu.on", "Açık");
            if (_resetInitialLabel != null) _resetInitialLabel.text = Tr("ui.menu.reset_btn", "Yeni Oyun (Kaydı Sıfırla)");
            if (_resetConfirmLabel != null) _resetConfirmLabel.text = Tr("ui.menu.reset_confirm", "EMİN MİSİN? SIFIRLA");
            if (_resetCancelLabel != null) _resetCancelLabel.text = Tr("ui.menu.reset_cancel", "Vazgeç");

            if (_profileHeaderText != null) _profileHeaderText.text = Tr("ui.menu.profile_header", "MAHALLE PROFİLİN");
            if (_statCashLabel != null) _statCashLabel.text = Tr("ui.menu.stat_cash", "NAKİT");
            if (_statMissionsLabel != null) _statMissionsLabel.text = Tr("ui.menu.stat_missions", "GÖREV");
            if (_statLandmarksLabel != null) _statLandmarksLabel.text = Tr("ui.menu.stat_landmarks", "KEŞİF");
            if (_statMemoriesLabel != null) _statMemoriesLabel.text = Tr("ui.menu.stat_memories", "HATIRA");

            if (_hudMenuButtonLabel != null) _hudMenuButtonLabel.text = Tr("ui.menu.hud_btn", "Menü");

            UpdateVistaLabel();
        }

        public void RefreshWorldSnapshot()
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int cash = data != null ? data.money : 250;
            int missionsDone = data != null && data.completedMissionIds != null ? data.completedMissionIds.Count : 0;
            int landmarks = LandmarkDiscoverySystem.DiscoveredCount;
            int memories = NeighborhoodCollectibleManager.CollectedCount;

            if (_liveClockText != null)
            {
                _liveClockText.text = DayNightCycle.Instance != null ? DayNightCycle.Instance.GetFormattedTime() : "09:00";
            }

            if (_liveWeatherText != null)
            {
                NeighborhoodWeatherState ws = NeighborhoodWeatherSystem.Instance != null
                    ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                    : NeighborhoodWeatherState.Clear;
                _liveWeatherText.text = ws switch
                {
                    NeighborhoodWeatherState.BreezyOvercast => Tr("ui.menu.weather_breezy", "Serin & Esintili"),
                    NeighborhoodWeatherState.SummerShower => Tr("ui.menu.weather_rain", "Yaz Yağmuru"),
                    _ => Tr("ui.menu.weather_clear", "Açık & Güneşli")
                };
            }

            if (_primaryPlaySubLabel != null)
            {
                if (HasReturningProgress() || IsPauseMode)
                {
                    StreetNameTracker street = FindAnyObjectByType<StreetNameTracker>();
                    string loc = (street != null && !string.IsNullOrEmpty(street.CurrentStreetName))
                        ? street.CurrentStreetName
                        : "Selman Sokağı";
                    _primaryPlaySubLabel.text = $"{loc}  ·  ₺ {cash}";
                }
                else
                {
                    _primaryPlaySubLabel.text = Tr("ui.menu.play_new_sub", "Selman Sokağı No:48 · Yeni Hayat");
                }
            }

            if (_rankBadgeText != null)
            {
                _rankBadgeText.text = $"★  {NeighborhoodReputation.GetRankTitle()}";
            }

            if (_rankProgressFill != null)
            {
                int xp = NeighborhoodReputation.CurrentXp;
                int next = Mathf.Max(1, NeighborhoodReputation.GetNextRankThreshold());
                _rankProgressFill.fillAmount = xp >= next ? 1f : Mathf.Clamp01((float)xp / next);
            }

            int totalMissions = (MissionManager.Instance != null && MissionManager.Instance.TotalMissionCount > 0)
                ? MissionManager.Instance.TotalMissionCount
                : DefaultTotalStoryMissions;
            if (_statCashValue != null) _statCashValue.text = $"₺ {cash}";
            if (_statMissionsValue != null) _statMissionsValue.text = $"{missionsDone} / {totalMissions}";
            if (_statLandmarksValue != null) _statLandmarksValue.text = $"{landmarks} / {LandmarkDiscoverySystem.TotalCount}";
            if (_statMemoriesValue != null) _statMemoriesValue.text = $"{memories} / {NeighborhoodCollectibleManager.TotalCount}";
        }

        private void RefreshDistrictCards()
        {
            bool isEn = IsEnglish();
            List<string> discovered = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? SaveManager.Instance.Data.discoveredLandmarkIds
                : null;

            for (int i = 0; i < Districts.Length && i < _districtCardBgs.Count; i++)
            {
                DistrictOption d = Districts[i];
                bool selected = i == _selectedDistrictIndex;
                bool isDiscovered = discovered != null && discovered.Contains(d.LandmarkId);

                _districtCardBgs[i].color = selected ? CardSelected : CardSurface;
                _districtCardBorders[i].color = selected ? BorderActive : BorderSubtle;
                _districtTitleTexts[i].text = isEn ? d.NameEn : d.NameTr;
                _districtTagTexts[i].text = isEn ? d.TagEn : d.TagTr;

                _districtBadgeTexts[i].text = isDiscovered
                    ? $"✓  {Tr("ui.menu.discovered", "KEŞFEDİLDİ")}"
                    : $"•  {Tr("ui.menu.unexplored", "KEŞFET")}";
                _districtBadgeTexts[i].color = isDiscovered ? AccentMint : AccentGold;
                _districtBadgeBgs[i].color = isDiscovered ? BadgeFoundBg : BadgeExploreBg;
            }

            if (_spawnDistrictBtnLabel != null && _selectedDistrictIndex >= 0 && _selectedDistrictIndex < Districts.Length)
            {
                DistrictOption sel = Districts[_selectedDistrictIndex];
                string action = IsPauseMode
                    ? Tr("ui.menu.travel_here", "BURAYA IŞINLAN")
                    : Tr("ui.menu.start_here", "BURADA BAŞLA");
                string place = isEn ? sel.NameEn : sel.NameTr;
                _spawnDistrictBtnLabel.text = $"▶   {action}   ·   {place}";
            }
        }

        private void RefreshSettingsUiState()
        {
            GraphicsTier tier = GraphicsProfileManager.Instance != null
                ? GraphicsProfileManager.Instance.CurrentTier
                : GraphicsTier.Medium;
            SetSegColor(_gfxLowBg, tier == GraphicsTier.Low);
            SetSegColor(_gfxMedBg, tier == GraphicsTier.Medium);

            bool isEn = IsEnglish();
            SetSegColor(_langTrBg, !isEn);
            SetSegColor(_langEnBg, isEn);

            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            if (_volumeValueText != null) _volumeValueText.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";

            float sens = InputHub.Instance != null ? InputHub.Instance.SensitivityMultiplier : 1f;
            SetSegColor(_sensLowBg, sens < 0.85f);
            SetSegColor(_sensMidBg, sens >= 0.85f && sens <= 1.20f);
            SetSegColor(_sensHighBg, sens > 1.20f);

            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 9f;
            int activeTimeIdx = hour < 11.5f ? 0 : (hour < 16.5f ? 1 : (hour < 20.0f ? 2 : 3));
            for (int i = 0; i < 4; i++)
            {
                SetSegColor(_timeChipBgs[i], i == activeTimeIdx);
            }

            bool fpsOn = _hud != null && _hud.IsTelemetryVisible;
            SetSegColor(_fpsOffBg, !fpsOn);
            SetSegColor(_fpsOnBg, fpsOn);
        }

        private void RefreshSportCards()
        {
            bool isEn = IsEnglish();
            for (int i = 0; i < QuickSports.Length && i < _sportCardBgs.Count; i++)
            {
                SportQuickOption opt = QuickSports[i];
                bool selected = i == _selectedSportIndex;
                _sportCardBgs[i].color = selected ? CardSelected : CardSurface;
                _sportCardBorders[i].color = selected ? BorderActive : BorderSubtle;
                _sportTitleTexts[i].text = isEn ? opt.NameEn : opt.NameTr;
                _sportTagTexts[i].text = isEn ? opt.TagEn : opt.TagTr;
                _sportBadgeTexts[i].text = isEn ? opt.BadgeEn : opt.BadgeTr;
            }

            if (_startSportBtnLabel != null && _selectedSportIndex >= 0 && _selectedSportIndex < QuickSports.Length)
            {
                SportQuickOption sel = QuickSports[_selectedSportIndex];
                string action = Tr("ui.menu.play_sport_now", "MAÇA BAŞLA");
                string sportName = isEn ? sel.NameEn : sel.NameTr;
                _startSportBtnLabel.text = $"▶   {action}   ·   {sportName}";
            }
        }

        private void UpdateNavHighlight()
        {
            UpdateNavBg(_navSportsBtn, _navSportsBg, ActiveDrawer == MenuDrawer.Sports);
            UpdateNavBg(_navDistrictsBtn, _navDistrictsBg, ActiveDrawer == MenuDrawer.Districts);
            UpdateNavBg(_navSettingsBtn, _navSettingsBg, ActiveDrawer == MenuDrawer.Settings);
        }

        private static void UpdateNavBg(UnityEngine.UI.Button btn, UnityEngine.UI.Image bg, bool active)
        {
            if (bg == null) return;
            bg.color = active ? CardSelected : PanelGlass;
            if (btn != null)
            {
                UiButtonFeedback fb = btn.GetComponent<UiButtonFeedback>();
                if (fb != null) fb.Configure(btn, bg);
            }
        }

        private void UpdateVistaLabel()
        {
            if (_vistaNameText == null) return;
            bool isEn = IsEnglish();
            if (_usingCustomPreview)
            {
                _vistaNameText.text = isEn ? _customPreviewNameEn : _customPreviewNameTr;
                return;
            }
            if (_usingPlayerOrbitVista)
            {
                _vistaNameText.text = Tr("ui.menu.vista_player", "Bulunduğun Konum");
                return;
            }
            ScenicVista v = ScenicVistas[_currentVistaIndex];
            _vistaNameText.text = isEn ? v.NameEn : v.NameTr;
        }

        private static void SetSegColor(UnityEngine.UI.Image bg, bool active)
        {
            if (bg != null)
            {
                bg.color = active ? AccentTealDark : SegInactive;
            }
        }

        private static bool HasReturningProgress()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return false;
            return d.totalPlayTimeSeconds > 8f ||
                   d.hasSeenStoryIntro ||
                   (d.completedMissionIds != null && d.completedMissionIds.Count > 0) ||
                   (d.discoveredLandmarkIds != null && d.discoveredLandmarkIds.Count > 0);
        }

        private static bool IsEnglish()
        {
            return LocalizationService.Instance != null &&
                   string.Equals(LocalizationService.Instance.CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase);
        }

        private static string Tr(string key, string fallback) => LocalizationService.Tr(key, fallback);

        // =====================================================================
        // PROCEDURAL UI SPRITES, VECTOR SDF ICONS & UGUI HELPERS
        // =====================================================================

        private void BuildProceduralSprites()
        {
            _roundedCardSprite = CreateRoundedRectSprite(96, 96, 20, outlineOnly: false, outlineThickness: 0f);
            _roundedOutlineSprite = CreateRoundedRectSprite(96, 96, 20, outlineOnly: true, outlineThickness: 2.0f);
            _pillSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: false, outlineThickness: 0f);
            _pillOutlineSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: true, outlineThickness: 2.0f);

            _leftVignetteSprite = CreateHorizontalGradientSprite(192,
                new Color(0.028f, 0.045f, 0.082f, 0.88f),
                new Color(0.028f, 0.045f, 0.082f, 0.52f),
                new Color(0.028f, 0.045f, 0.082f, 0.0f));
            _bottomVignetteSprite = CreateVerticalGradientSprite(96,
                new Color(0.028f, 0.045f, 0.082f, 0.64f),
                new Color(0.028f, 0.045f, 0.082f, 0.0f));

            // Crisp 64x64 procedural SDF vector icons (100% consistent across iOS, Android, macOS)
            _iconPlaySprite = CreateVectorIconSprite(64, SamplePlayTriangle);
            _iconPinSprite = CreateVectorIconSprite(64, SampleMapPin);
            _iconSlidersSprite = CreateVectorIconSprite(64, SampleSlidersIcon);
            _iconSparkleSprite = CreateVectorIconSprite(64, SampleSparkleStar);
            _iconMapSprite = CreateVectorIconSprite(64, SampleMapGrid);
            _iconMenuBarsSprite = CreateVectorIconSprite(64, SampleHamburgerBars);

            _brandIconSprite = Resources.Load<Sprite>("UI/Icons/ourworld_menu_icon");
            _brandIconCreatedAtRuntime = false;
            if (_brandIconSprite == null)
            {
                Texture2D iconTex = Resources.Load<Texture2D>("UI/Icons/ourworld_menu_icon");
                if (iconTex != null)
                {
                    _brandIconSprite = Sprite.Create(
                        iconTex,
                        new Rect(0, 0, iconTex.width, iconTex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    _brandIconCreatedAtRuntime = true;
                }
            }
        }

        private static Sprite CreateVectorIconSprite(int size, Func<float, float, float> alphaFn)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float a = Mathf.Clamp01(alphaFn(nx, ny));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float SmoothEdge(float signedDist, float aa = 0.055f) => Mathf.Clamp01(0.5f - signedDist / aa);

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        private static float SamplePlayTriangle(float x, float y)
        {
            // Right-pointing rounded triangle centered slightly right for optical balance
            float left = -0.46f;
            float right = 0.62f;
            float slope = 0.62f;
            float maxY = Mathf.Max(0f, (right - x) * slope);
            float dLeft = left - x;
            float dDiag = (Mathf.Abs(y) - maxY) / Mathf.Sqrt(1f + slope * slope);
            return SmoothEdge(Mathf.Max(dLeft, dDiag) - 0.06f);
        }

        private static float SampleMapPin(float x, float y)
        {
            // Teardrop location pin with crisp inner circular cutout
            Vector2 headCenter = new Vector2(0f, 0.24f);
            float dCircle = (new Vector2(x, y) - headCenter).magnitude - 0.44f;
            float tipT = Mathf.Clamp01((0.24f - y) / 0.96f);
            float coneHalfW = Mathf.Lerp(0.42f, 0.04f, tipT);
            float dCone = y <= 0.24f && y >= -0.74f ? (Mathf.Abs(x) - coneHalfW) : 1f;
            float dPin = Mathf.Min(dCircle, dCone);
            float dHole = (new Vector2(x, y) - headCenter).magnitude - 0.17f;
            float pinAlpha = SmoothEdge(dPin);
            float holeAlpha = SmoothEdge(dHole);
            return Mathf.Clamp01(pinAlpha - holeAlpha);
        }

        private static float SampleSlidersIcon(float x, float y)
        {
            // 3 horizontal equalizer/setting bars with circular knobs
            Vector2 p = new Vector2(x, y);
            float d = 1f;
            float[] ys = { 0.48f, 0.0f, -0.48f };
            float[] knobs = { -0.22f, 0.26f, -0.10f };
            for (int i = 0; i < 3; i++)
            {
                float bar = SdSegment(p, new Vector2(-0.68f, ys[i]), new Vector2(0.68f, ys[i])) - 0.065f;
                float knob = (p - new Vector2(knobs[i], ys[i])).magnitude - 0.165f;
                d = Mathf.Min(d, Mathf.Min(bar, knob));
            }
            return SmoothEdge(d);
        }

        private static float SampleSparkleStar(float x, float y)
        {
            // Crisp 4-point star sparkle + secondary small accent star
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float star = Mathf.Pow(ax, 0.62f) + Mathf.Pow(ay, 0.62f) - 0.74f;
            float mini = Mathf.Pow(Mathf.Abs(x - 0.52f), 0.65f) + Mathf.Pow(Mathf.Abs(y - 0.52f), 0.65f) - 0.30f;
            return SmoothEdge(Mathf.Min(star, mini), 0.08f);
        }

        private static float SampleMapGrid(float x, float y)
        {
            // Clean folded 3-panel map / compass grid icon
            Vector2 p = new Vector2(x, y);
            float d = 1f;
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, -0.50f), new Vector2(-0.66f, 0.56f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, -0.60f), new Vector2(-0.22f, 0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, -0.46f), new Vector2(0.22f, 0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.66f, -0.56f), new Vector2(0.66f, 0.50f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, 0.56f), new Vector2(-0.22f, 0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, 0.46f), new Vector2(0.22f, 0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, 0.60f), new Vector2(0.66f, 0.50f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, -0.50f), new Vector2(-0.22f, -0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, -0.60f), new Vector2(0.22f, -0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, -0.46f), new Vector2(0.66f, -0.56f)) - 0.065f);
            return SmoothEdge(d);
        }

        private static float SampleHamburgerBars(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d = SdSegment(p, new Vector2(-0.68f, 0.48f), new Vector2(0.68f, 0.48f)) - 0.095f;
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.68f, 0.0f), new Vector2(0.68f, 0.0f)) - 0.095f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.68f, -0.48f), new Vector2(0.40f, -0.48f)) - 0.095f);
            return SmoothEdge(d);
        }

        private static Sprite CreateRoundedRectSprite(int w, int h, int radius, bool outlineOnly, float outlineThickness)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[w * h];
            float r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f - 1f);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float bx = halfW - r;
            float by = halfH - r;

            for (int y = 0; y < h; y++)
            {
                float py = Mathf.Abs(y + 0.5f - halfH) - by;
                for (int x = 0; x < w; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - halfW) - bx;
                    float dist = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f))
                                 + Mathf.Min(Mathf.Max(px, py), 0f) - r;

                    float alpha;
                    if (outlineOnly)
                    {
                        float outer = Mathf.Clamp01(0.5f - dist);
                        float inner = Mathf.Clamp01(0.5f - (dist + outlineThickness));
                        alpha = Mathf.Clamp01(outer - inner);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(0.5f - dist);
                    }

                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            int b = Mathf.Min(radius + 2, Mathf.Min(w, h) / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite CreateHorizontalGradientSprite(int width, Color left, Color mid, Color right)
        {
            Texture2D tex = new Texture2D(width, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[width * 2];
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                Color c = u < 0.52f
                    ? Color.Lerp(left, mid, Mathf.SmoothStep(0f, 1f, u / 0.52f))
                    : Color.Lerp(mid, right, Mathf.SmoothStep(0f, 1f, (u - 0.52f) / 0.48f));
                cols[x] = c;
                cols[width + x] = c;
            }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateVerticalGradientSprite(int height, Color bottom, Color top)
        {
            Texture2D tex = new Texture2D(2, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[height * 2];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                cols[y * 2] = c;
                cols[y * 2 + 1] = c;
            }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void CreateCenteredIcon(string name, Transform parent, Sprite sprite, float sizePx, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private void AddTopAccentStrip(Transform parent, Color accent)
        {
            GameObject strip = new GameObject("TopAccentStrip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            strip.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)strip.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -1f);
            rt.sizeDelta = new Vector2(-48f, 3f);
            UnityEngine.UI.Image img = strip.GetComponent<UnityEngine.UI.Image>();
            img.sprite = _pillSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = new Color(accent.r, accent.g, accent.b, 0.78f);
            img.raycastTarget = false;
        }

        private GameObject CreateRoundedPanel(
            string name, Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color, bool addBorder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;

            if (addBorder)
            {
                AddOutlineOverlay(go.transform, BorderSubtle);
            }

            return go;
        }

        private UnityEngine.UI.Image AddOutlineOverlay(Transform parent, Color color, Sprite customOutline = null)
        {
            GameObject go = new GameObject("OutlineRim", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            StretchFull((RectTransform)go.transform, 0f, 0f);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = customOutline != null ? customOutline : _roundedOutlineSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private UnityEngine.UI.Text CreateText(
            string name, Transform parent, string text, int fontSize, FontStyle style, Color color,
            TextAnchor alignment, bool addShadow = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            UnityEngine.UI.Text t = go.GetComponent<UnityEngine.UI.Text>();
            t.font = _uiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            if (addShadow)
            {
                UnityEngine.UI.Shadow sh = go.AddComponent<UnityEngine.UI.Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(0f, -2f);
            }

            return t;
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void StretchFull(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
