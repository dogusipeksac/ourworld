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
    public sealed partial class MainMenuController : MonoBehaviour
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
            },
            new SportQuickOption
            {
                BadgeTr = "4 SİLAH · +120 ₺",
                BadgeEn = "4 GUNS  · +120 ₺",
                NameTr = "Atış Poligonu",
                NameEn = "Shooting Range",
                TagTr = "Tabanca · Pompalı · Karabin · Sniper\nHedef · IPSC · Trap · 100m",
                TagEn = "Pistol · Shotgun · Carbine · Sniper\nBullseye · IPSC · Trap · 100m",
                ExitPos = new Vector3(14.0f, 0.14f, -190.5f),
                ExitYaw = 180f,
                PreviewCamPos = new Vector3(2.0f, 5.4f, -178.0f),
                PreviewLookAt = new Vector3(14.0f, 2.0f, -214.0f)
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
        private UnityEngine.UI.Text _setVoiceCaption;
        private UnityEngine.UI.Image _voiceOffBg;
        private UnityEngine.UI.Text _voiceOffText;
        private UnityEngine.UI.Image _voiceOnBg;
        private UnityEngine.UI.Text _voiceOnText;
        private UnityEngine.UI.Text _setSensCaption;
        private UnityEngine.UI.Image _sensLowBg;
        private UnityEngine.UI.Text _sensLowText;
        private UnityEngine.UI.Image _sensMidBg;
        private UnityEngine.UI.Text _sensMidText;
        private UnityEngine.UI.Image _sensHighBg;
        private UnityEngine.UI.Text _sensHighText;
        private UnityEngine.UI.Text _setInvertYCaption;
        private UnityEngine.UI.Image _invertYOffBg;
        private UnityEngine.UI.Text _invertYOffText;
        private UnityEngine.UI.Image _invertYOnBg;
        private UnityEngine.UI.Text _invertYOnText;
        private UnityEngine.UI.Text _setSubtitleCaption;
        private UnityEngine.UI.Image _subNormBg;
        private UnityEngine.UI.Text _subNormText;
        private UnityEngine.UI.Image _subLargeBg;
        private UnityEngine.UI.Text _subLargeText;
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

            // Android Back / Escape closes the topmost open modal panel cleanly before toggling Pause Menu
            if (PhonePanelController.Instance != null && PhonePanelController.Instance.IsOpen)
            {
                PhonePanelController.Instance.Close();
                return;
            }
            if (ShopMenuPanel.Instance != null && ShopMenuPanel.Instance.IsOpen)
            {
                ShopMenuPanel.Instance.Close();
                return;
            }
            if (AutoGaragePanelController.Instance != null && AutoGaragePanelController.Instance.IsOpen)
            {
                AutoGaragePanelController.Instance.Close();
                return;
            }
            if (FarmPanelController.Instance != null && FarmPanelController.Instance.IsOpen)
            {
                FarmPanelController.Instance.Close();
                return;
            }
            if (HomeDecorPanelController.Instance != null && HomeDecorPanelController.Instance.IsOpen)
            {
                HomeDecorPanelController.Instance.Close();
                return;
            }
            if (PaintPalettePanel.Instance != null && PaintPalettePanel.Instance.IsOpen)
            {
                PaintPalettePanel.Instance.Close();
                return;
            }
            if (ShootingRangeHud.Instance != null && ShootingRangeHud.Instance.IsArmoryOpen)
            {
                ShootingRangeHud.Instance.CloseArmory();
                return;
            }
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

    }
}
