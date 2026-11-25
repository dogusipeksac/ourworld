using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Realistic 2v2 Sand Volleyball arena:
    /// - Hold-to-charge Jump Serve with golden ACE sweet-spot & ball trail
    /// - True physics-driven court positioning (ZERO random coin-flip points — rivals physically chase shots!)
    /// - 3-tier contact timing quality (Mükemmel / İyi / Geç) with visual timing bar & shrinking landing ring
    /// - Attack variety: aimed high-velocity Spike (Ana Tuş) vs smart fingertip Plase / Drop Tip (İkinci Tuş) over rival blocker!
    /// - Defensive skill moves: Double-hand Net Roof Block (Blok!) & Sand Pancake Dive Save (Uç!) with stamina
    /// - 3 League Tiers (Acemi / Mahalle / Şampiyonlar), live km/h speed & rally counter!
    /// </summary>
    public sealed class LiveVolleyballMatch : MonoBehaviour, IPlayableSportArena
    {
        private const float HalfW = 4.2f;
        private const float HalfL = 7.6f;
        private const int PointsToWin = 5;
        private const float PlayerHitWindow = 0.72f;

        private ProceduralHumanoid[] _teal;
        private ProceduralHumanoid[] _burgundy;
        private Transform _ball;
        private Transform _ballShadow;
        private TrailRenderer _ballTrail;
        private Material _ballTrailMat;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _hitClip;

        private int _targetTeam = 1;
        private int _targetIdx = 1;
        private int _touchStep; // 0 = receive/dig, 1 = set to front, 2 = spike/tip over net
        private int _rallyCount;
        private float _flightElapsed;
        private float _flightDuration = 1.15f;
        private Vector3 _flightStart;
        private Vector3 _flightEnd;
        private float _flightArc = 2.6f;
        private float _pauseTimer;
        private int _tealScore;
        private int _burgundyScore;

        // Player match state
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;
        private Vector3 _playerLocalPos;
        private bool _awaitingPlayerServe;
        private bool _isChargingServe;
        private float _serveCharge;
        private bool _playerHitQueued;
        private bool _playerEarlyBuffered;
        private bool _playerQueuedTip; // true = Plase / Tip over block, false = Spike or Bump
        private int _queuedTimingQuality; // 0=Late/Weak, 1=Good, 2=Perfect Gold!
        private bool _playerInHitWindow;
        private bool _playerAnimPending;
        private SportAnimKind _playerPendingAnim;
        private float _playerPendingAnimDur;
        private float _playerPendingAnimLead;
        private bool _npcSwingTriggered;
        private bool _playerServeTossing;
        private float _playerServeTossTimer;
        private float _playerServePendingCharge;
        private Vector3 _playerServeTossStart;
        private bool _matchOver;
        private bool _playerWonMatch;
        private bool _lastPointTeal;

        // Realistic defensive & offensive mechanics
        private int _leagueTier; // 0=Acemi, 1=Mahalle, 2=Şampiyonlar
        private float _stamina = 100f;
        private float _blockActiveTimer;
        private float _blockCooldown;
        private float _diveTimer;
        private Vector3 _diveDir;
        private float _rivalBlockerJumpTimer;
        private float _rivalBlockerX;
        private Vector2 _aimTargetLocal = new Vector2(0f, 4.8f); // Rival court aim reticle!
        private float _lastHitKmh;
        private float _calloutTimer;
        private string _calloutText;
        private Color _calloutColor = new Color(0.24f, 0.92f, 0.76f);
        private int _playerBlocksScored;
        private int _playerAcesScored;

        private Transform _landingRing;
        private Renderer _landingRingRenderer;
        private Material _landingRingMat;
        private Transform _aimReticle;
        private Material _aimReticleMat;
        private Material _aimReticleCoreMat;
        private MaterialPropertyBlock _ringProps;
        private int _lastHudHash;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool HasSecondaryAction => _player != null && !_matchOver && !_awaitingPlayerServe && _pauseTimer <= 0f;
        public bool AcceptsPrimaryAction => _player != null && (_matchOver || _awaitingPlayerServe || _playerInHitWindow || (_targetTeam == 0 && _targetIdx == 1));

        private bool IsPlayerAttackSet => _player != null && _targetTeam == 0 && _targetIdx == 1 && _touchStep >= 1;
        private bool IsRivalAboutToSpike => _player != null && _targetTeam == 1 && _touchStep >= 1 && _pauseTimer <= 0f && !_awaitingPlayerServe;
        private bool CanPlayerNetBlock => IsRivalAboutToSpike && _playerLocalPos.z >= -3.1f && _blockCooldown <= 0f;

        public void Configure(
            ProceduralHumanoid[] tealTeam,
            ProceduralHumanoid[] burgundyTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip hitClip)
        {
            _teal = tealTeam;
            _burgundy = burgundyTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _hitClip = hitClip;

            StartServe(0);
            UpdateScoreboard();
        }

        private void OnDestroy()
        {
            if (_landingRingMat != null) Destroy(_landingRingMat);
            if (_aimReticleMat != null) Destroy(_aimReticleMat);
            if (_aimReticleCoreMat != null) Destroy(_aimReticleCoreMat);
            if (_ballTrailMat != null) Destroy(_ballTrailMat);
        }

        private void EnsureVisualHelpers()
        {
            if (_ball != null && _ballTrail == null)
            {
                _ballTrail = _ball.gameObject.GetComponent<TrailRenderer>();
                if (_ballTrail == null) _ballTrail = _ball.gameObject.AddComponent<TrailRenderer>();
                _ballTrail.time = 0.28f;
                _ballTrail.minVertexDistance = 0.08f;
                _ballTrail.startWidth = 0.24f;
                _ballTrail.endWidth = 0.02f;
                _ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ballTrail.receiveShadows = false;
                Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (sh != null)
                {
                    _ballTrailMat = new Material(sh);
                    _ballTrail.sharedMaterial = _ballTrailMat;
                }
                SetTrailColor(new Color(1.0f, 0.86f, 0.24f, 0.86f));
                _ballTrail.emitting = false;
            }

            if (_landingRing == null)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Volleyball_LandingRing";
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(transform, false);
                ring.transform.localScale = new Vector3(1.1f, 0.012f, 1.1f);
                _landingRing = ring.transform;
                _landingRingRenderer = ring.GetComponent<MeshRenderer>();
                if (_landingRingRenderer != null)
                {
                    _landingRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                    if (sh != null)
                    {
                        _landingRingMat = new Material(sh) { color = new Color(0.24f, 0.92f, 0.72f, 1f) };
                        _landingRingMat.EnableKeyword("_EMISSION");
                        _landingRingMat.SetColor("_EmissionColor", new Color(0.14f, 0.55f, 0.42f));
                        _landingRingRenderer.sharedMaterial = _landingRingMat;
                    }
                }
                _ringProps = new MaterialPropertyBlock();
                ring.SetActive(false);
            }

            if (_aimReticle == null)
            {
                // High-contrast crimson-red + bright golden bullseye target on the rival sand court
                GameObject aimRoot = new GameObject("Volleyball_RivalAimReticle");
                aimRoot.transform.SetParent(transform, false);

                GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outer.name = "OuterTarget";
                Destroy(outer.GetComponent<Collider>());
                outer.transform.SetParent(aimRoot.transform, false);
                outer.transform.localScale = new Vector3(1.55f, 0.024f, 1.55f);

                GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                inner.name = "InnerBullseye";
                Destroy(inner.GetComponent<Collider>());
                inner.transform.SetParent(aimRoot.transform, false);
                inner.transform.localPosition = new Vector3(0f, 0.020f, 0f);
                inner.transform.localScale = new Vector3(0.68f, 0.024f, 0.68f);

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (unlit != null)
                {
                    _aimReticleMat = new Material(unlit);
                    _aimReticleMat.color = new Color(0.96f, 0.20f, 0.12f, 1f);
                    if (_aimReticleMat.HasProperty("_BaseColor")) _aimReticleMat.SetColor("_BaseColor", new Color(0.96f, 0.20f, 0.12f, 1f));
                    MeshRenderer mrOut = outer.GetComponent<MeshRenderer>();
                    if (mrOut != null) { mrOut.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrOut.sharedMaterial = _aimReticleMat; }

                    _aimReticleCoreMat = new Material(unlit);
                    _aimReticleCoreMat.color = new Color(1.0f, 0.88f, 0.20f, 1f);
                    if (_aimReticleCoreMat.HasProperty("_BaseColor")) _aimReticleCoreMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.20f, 1f));
                    MeshRenderer mrIn = inner.GetComponent<MeshRenderer>();
                    if (mrIn != null) { mrIn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrIn.sharedMaterial = _aimReticleCoreMat; }
                }

                _aimReticle = aimRoot.transform;
                aimRoot.SetActive(false);
            }
        }

        private void SetTrailColor(Color c)
        {
            if (_ballTrail == null) return;
            _ballTrail.startColor = c;
            _ballTrail.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        private string GetLeagueLabel()
        {
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            return _leagueTier switch
            {
                2 => isEn ? "Champions" : "Şampiyonlar",
                1 => isEn ? "District League" : "Mahalle Ligi",
                _ => isEn ? "Rookie League" : "Acemi Lig"
            };
        }

        public void BeginPlayerMatch(PlayerFootController player)
        {
            if (player == null) return;
            _player = player;
            _beginPos = player.transform.position;
            _beginRot = player.transform.rotation;
            _tealScore = 0;
            _burgundyScore = 0;
            _matchOver = false;
            _playerWonMatch = false;
            _stamina = 100f;
            _blockActiveTimer = 0f;
            _blockCooldown = 0f;
            _diveTimer = 0f;
            _calloutTimer = 0f;
            _calloutText = null;
            _playerBlocksScored = 0;
            _playerAcesScored = 0;
            _leagueTier = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? Mathf.Clamp(SaveManager.Instance.Data.volleyballLeagueTier, 0, 2)
                : 0;

            EnsureVisualHelpers();

            if (_teal != null && _teal.Length > 1 && _teal[1] != null)
            {
                _teal[1].gameObject.SetActive(false);
            }

            player.SetSeatedState(true);
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(true, distance: 8.6f, pitch: 26f, fov: 61f, lockYaw: transform.eulerAngles.y);
            }

            SetupPlayerServe();
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
            SetCallout(SportLoc.L($"{GetLeagueLabel()} · Servis Sende!", $"{GetLeagueLabel()} · Your Serve!"), new Color(0.24f, 0.94f, 0.80f), 1.6f);
            UpdateScoreboard();
            HudRevision++;
        }

        public void EndPlayerMatch()
        {
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(false);
                _cameraRig = null;
            }
            if (_landingRing != null) _landingRing.gameObject.SetActive(false);
            if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);
            if (_ballTrail != null) _ballTrail.emitting = false;

            if (_player != null)
            {
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
                _player.Humanoid?.SetLookAtPoint(false, Vector3.zero);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
                _player.SetSeatedState(false);
            }
            _player = null;
            _matchOver = false;
            _awaitingPlayerServe = false;
            _isChargingServe = false;
            _playerAnimPending = false;
            _playerServeTossing = false;
            _npcSwingTriggered = false;

            if (_teal != null && _teal.Length > 1 && _teal[1] != null)
            {
                _teal[1].gameObject.SetActive(true);
            }

            _tealScore = 0;
            _burgundyScore = 0;
            StartServe(0);
            UpdateScoreboard();
        }

        public string ControlsHintKey() => "sports.volleyball.controls";

        public string PrimaryButtonLabel()
        {
            if (_matchOver) return LocalizationService.Tr("sports.btn.rematch", "Tekrar");
            if (_isChargingServe) return SportLoc.L("BIRAK!", "RELEASE!");
            if (_awaitingPlayerServe) return SportLoc.L("Servis (Tut)", "Serve (Hold)");
            if (_targetTeam == 0 && _targetIdx == 1)
            {
                return _touchStep >= 1
                    ? LocalizationService.Tr("sports.btn.spike", "Smaç!")
                    : LocalizationService.Tr("sports.btn.bump", "Manşet");
            }
            return SportLoc.L("Hazır", "Ready");
        }

        public string SecondaryButtonLabel()
        {
            if (IsPlayerAttackSet) return SportLoc.L("Plase!", "Drop Tip!");
            if (CanPlayerNetBlock) return SportLoc.L("Blok!", "Block!");
            if (_targetTeam == 0 && _targetIdx == 1 && _touchStep == 0) return SportLoc.L("Uç!", "Dive!");
            return SportLoc.L("Blok!", "Block!");
        }

        public string HudText()
        {
            string title = LocalizationService.Tr("sports.volleyball.title", "Voleybol");
            string you = LocalizationService.Tr("sports.volleyball.team_you", "Sen");
            string rival = LocalizationService.Tr("sports.volleyball.team_rival", "Bordo");
            string rallyInfo = _rallyCount > 0 ? SportLoc.L($" · Ralli {_rallyCount}", $" · Rally {_rallyCount}") : string.Empty;
            string status;

            if (_matchOver)
            {
                status = _playerWonMatch
                    ? SportLoc.L($"ZAFER! ({_playerBlocksScored} Blok · {_playerAcesScored} Ace) — Tekrar için Vur", $"VICTORY! ({_playerBlocksScored} Blocks · {_playerAcesScored} Aces) — Tap to Rematch")
                    : SportLoc.L("Maç Bitti — Rövanş için Vur!", "Match Over — Tap for Rematch!");
            }
            else if (_pauseTimer > 0f)
            {
                status = _lastPointTeal ? SportLoc.L("SAYI SENİN!", "YOUR POINT!") : SportLoc.L("Bordo Sayı Aldı · Hazırlan!", "Burgundy Scored · Get Ready!");
            }
            else if (_isChargingServe)
            {
                status = SportLoc.L("Servis Gücünü Ayarla · Altın bölgede bırak → Sıçramalı ACE!", "Charge Serve · Release in gold zone → Jump ACE!");
            }
            else if (_awaitingPlayerServe)
            {
                status = SportLoc.L("Servis (Basılı Tut) · Yön tuşuyla hedefle!", "Serve (Hold) · Aim with joystick!");
            }
            else if (IsPlayerAttackSet)
            {
                status = SportLoc.L("Smaç! (Vur) veya Blok Üstü Plase! (İkinci Tuş) · Joystick ile hedefle", "Spike! (Primary) or Drop Tip! (Secondary) · Aim with joystick");
            }
            else if (CanPlayerNetBlock)
            {
                status = SportLoc.L("Rakip Smaç Hazırlıyor! Filede Blok! (İkinci Tuş) zıpla!", "Rival Spiking! Jump for a Net Block! (Secondary)");
            }
            else if (_targetTeam == 0 && _targetIdx == 1)
            {
                status = _playerInHitWindow
                    ? SportLoc.L("ŞİMDİ MANŞET! (Uzak topta Uç! ile dalış yap)", "BUMP NOW! (Tap Dive! for far balls)")
                    : SportLoc.L("Halkaya yaklaş · Altın zamanlamada Manşet vur!", "Move into the ring · Time your Bump in the gold zone!");
            }
            else if (_targetTeam == 0)
            {
                status = SportLoc.L("Pasörün topu kaldırıyor · Fileye yaklaşıp smaca hazırlan!", "Teammate setting · Approach the net for a Spike!");
            }
            else
            {
                status = SportLoc.L("Fileye koşup Blok kur veya arka sahayı koru!", "Rush the net to Block or cover the back court!");
            }

            return $"{title} [{GetLeagueLabel()}]  ·  {you} {_tealScore} - {_burgundyScore} {rival}{rallyInfo}\n{status}";
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = _player != null && !_matchOver,
                StaminaFill01 = Mathf.Clamp01(_stamina / 100f),
                CalloutBadge = _calloutTimer > 0f ? _calloutText : null,
                CalloutColor = _calloutColor
            };

            if (_isChargingServe)
            {
                bool sweet = _serveCharge >= 0.58f && _serveCharge <= 0.86f;
                bool over = _serveCharge > 0.92f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _serveCharge;
                state.PrimaryColor = over
                    ? new Color(0.96f, 0.30f, 0.24f)
                    : (sweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.92f, 0.78f));
                state.PrimaryLabel = over ? SportLoc.L("FİLE RİSKİ!", "NET RISK!") : (sweet ? SportLoc.L("ALTIN ACE SERVİS!", "GOLDEN ACE SERVE!") : SportLoc.L("SERVİS GÜCÜ", "SERVE POWER"));
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.58f;
                state.SweetSpotMax01 = 0.86f;
            }
            else if (_player != null && !_matchOver && _pauseTimer <= 0f && !_awaitingPlayerServe && _targetTeam == 0 && _targetIdx == 1)
            {
                float progress = Mathf.Clamp01(_flightElapsed / Mathf.Max(0.25f, _flightDuration));
                bool inGold = progress >= 0.72f && progress <= 0.94f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = progress;
                state.PrimaryColor = _playerHitQueued
                    ? new Color(1.0f, 0.86f, 0.24f)
                    : (inGold ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.92f, 0.70f));
                state.PrimaryLabel = _touchStep >= 1 ? SportLoc.L("SMAÇ / PLASE ZAMANI", "SPIKE / TIP TIMING") : SportLoc.L("MANŞET ZAMANI", "BUMP TIMING");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.72f;
                state.SweetSpotMax01 = 0.94f;
            }
            else if (CanPlayerNetBlock)
            {
                float progress = Mathf.Clamp01(_flightElapsed / Mathf.Max(0.25f, _flightDuration));
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = progress;
                state.PrimaryColor = new Color(0.32f, 0.82f, 1.0f);
                state.PrimaryLabel = SportLoc.L("FİLE BLOK ZAMANI!", "NET BLOCK TIMING!");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.65f;
                state.SweetSpotMax01 = 0.96f;
            }

            return state;
        }

        public void TriggerPrimaryAction()
        {
            if (_player == null) return;

            if (_matchOver)
            {
                if (_pauseTimer <= 0f)
                {
                    _tealScore = 0;
                    _burgundyScore = 0;
                    _matchOver = false;
                    _playerBlocksScored = 0;
                    _playerAcesScored = 0;
                    _stamina = 100f;
                    SetupPlayerServe();
                    UpdateScoreboard();
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                    HudRevision++;
                }
                return;
            }

            if (_pauseTimer > 0f) return;

            if (_awaitingPlayerServe)
            {
                _isChargingServe = true;
                _serveCharge = 0.08f;
                HudRevision++;
                return;
            }

            HandlePlayerContactTap(wantTip: false);
        }

        public void TriggerPrimaryRelease(float holdSeconds)
        {
            if (!_isChargingServe) return;
            float charge = Mathf.Clamp01(Mathf.Max(_serveCharge, holdSeconds / 0.82f));
            _isChargingServe = false;
            _serveCharge = 0f;
            if (_awaitingPlayerServe && !_matchOver && _pauseTimer <= 0f)
            {
                LaunchPlayerJumpServe(charge);
            }
            HudRevision++;
        }

        public void TriggerSecondaryAction()
        {
            if (_player == null || _matchOver || _pauseTimer > 0f || _awaitingPlayerServe) return;

            // 1. Offense at net: Fingertip Plase (Drop Tip) right over the rival blocker!
            if (IsPlayerAttackSet)
            {
                HandlePlayerContactTap(wantTip: true);
                return;
            }

            // 2. Defense at net: Double-hand Roof Block!
            if (IsRivalAboutToSpike && _blockCooldown <= 0f)
            {
                TryPlayerJumpBlock();
                return;
            }

            // 3. Back-court defense: Athletic Sand Pancake Dive Save for far balls!
            if (_targetTeam == 0 && _targetIdx == 1 && _touchStep == 0)
            {
                TryPlayerDiveSave();
                return;
            }

            // Fallback: jump block if near net
            if (_blockCooldown <= 0f)
            {
                TryPlayerJumpBlock();
            }
        }

        public void TriggerSecondaryRelease(float holdSeconds) { }

        private void TryPlayerJumpBlock()
        {
            if (_stamina < 14f) return;
            _stamina = Mathf.Max(0f, _stamina - 16f);
            _blockActiveTimer = 0.56f;
            _blockCooldown = 0.88f;
            // Snap slightly closer to net for clean vertical block wall
            if (_playerLocalPos.z > -3.4f)
            {
                _playerLocalPos.z = Mathf.Min(-0.82f, _playerLocalPos.z + 0.55f);
                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
            }
            _player.Humanoid?.PlaySportAnim(SportAnimKind.VolleyballBlock, 0.58f);
            GameFeelAudio.PlayUiClick();
            HudRevision++;
        }

        private void TryPlayerDiveSave()
        {
            if (_playerHitQueued) return;
            Vector3 drop = new Vector3(_flightEnd.x, 0f, _flightEnd.z);
            Vector3 toDrop = drop - _playerLocalPos;
            toDrop.y = 0f;
            float dist = toDrop.magnitude;

            if (dist <= 1.55f)
            {
                // Close enough for a normal crisp dig!
                HandlePlayerContactTap(wantTip: false);
                return;
            }

            if (_stamina < 20f || dist > 4.4f) return;
            _stamina = Mathf.Max(0f, _stamina - 22f);
            _diveTimer = 0.44f;
            _diveDir = toDrop.normalized;
            _playerHitQueued = true;
            _playerEarlyBuffered = false;
            _playerQueuedTip = false;
            _queuedTimingQuality = 1; // Clean athletic save!
            _player.Humanoid?.PlaySportAnim(SportAnimKind.VolleyballDiveSave, 0.52f);
            GameFeelAudio.PlaySlideWhoosh();
            GameFeelAudio.SpawnBurst(transform.TransformPoint(_playerLocalPos + Vector3.up * 0.10f), new Color(0.92f, 0.82f, 0.58f), count: 12, spread: 0.55f);
            SetCallout(SportLoc.L("UÇARAK KURTARIŞ!", "DIVING PANCAKE SAVE!"), new Color(0.28f, 0.94f, 0.82f), 1.15f);
        }

        private void HandlePlayerContactTap(bool wantTip)
        {
            if (_targetTeam != 0 || _targetIdx != 1 || _playerHitQueued) return;

            float remaining = Mathf.Max(0f, _flightDuration - _flightElapsed);
            if (remaining <= PlayerHitWindow)
            {
                int quality = (remaining >= 0.04f && remaining <= 0.28f) ? 2 : (remaining <= 0.54f ? 1 : 0);
                LockInPlayerVolley(wantTip, quality);
            }
            else
            {
                _playerEarlyBuffered = true;
                _playerQueuedTip = wantTip;
                HudRevision++;
            }
        }

        private void LockInPlayerVolley(bool tip, int timingQuality)
        {
            _playerHitQueued = true;
            _playerEarlyBuffered = false;
            _playerQueuedTip = tip;
            _queuedTimingQuality = timingQuality;

            SportAnimKind anim = _touchStep >= 1
                ? (tip ? SportAnimKind.VolleyballTip : SportAnimKind.VolleyballSpike)
                : SportAnimKind.VolleyballBump;
            float dur = anim == SportAnimKind.VolleyballSpike ? 0.64f : 0.48f;
            float peakLead = anim == SportAnimKind.VolleyballSpike ? 0.27f : 0.20f;
            float remaining = Mathf.Max(0f, _flightDuration - _flightElapsed);
            if (remaining <= peakLead + 0.04f)
            {
                _playerAnimPending = false;
                _player.Humanoid?.PlaySportAnim(anim, dur);
            }
            else
            {
                // Wait until peakLead before ball arrival so spike/bump peaks right at exact contact!
                _playerAnimPending = true;
                _playerPendingAnim = anim;
                _playerPendingAnimDur = dur;
                _playerPendingAnimLead = peakLead;
            }
            HudRevision++;
        }

        private void SetupPlayerServe()
        {
            _awaitingPlayerServe = true;
            _isChargingServe = false;
            _serveCharge = 0f;
            _playerHitQueued = false;
            _playerEarlyBuffered = false;
            _playerInHitWindow = false;
            _playerAnimPending = false;
            _playerServeTossing = false;
            _npcSwingTriggered = false;
            _blockActiveTimer = 0f;
            _diveTimer = 0f;
            _touchStep = 0;
            _rallyCount = 0;
            _targetTeam = 1;
            _aimTargetLocal = new Vector2(0f, 5.2f);
            if (_ballTrail != null) { _ballTrail.Clear(); _ballTrail.emitting = false; }

            _teal[0].transform.localPosition = new Vector3(-1.6f, 0f, -2.3f);
            _burgundy[0].transform.localPosition = new Vector3(1.5f, 0f, 2.2f);
            _burgundy[1].transform.localPosition = new Vector3(-1.5f, 0f, 5.4f);

            _playerLocalPos = new Vector3(0.4f, 0f, -6.2f);
            _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            if (_landingRing != null) _landingRing.gameObject.SetActive(false);

            _ball.localPosition = _playerLocalPos + new Vector3(0.18f, 1.25f, 0.35f);
            HudRevision++;
        }

        private void LaunchPlayerJumpServe(float charge)
        {
            _awaitingPlayerServe = false;
            _playerServeTossing = true;
            _playerServeTossTimer = 0.24f;
            _playerServePendingCharge = charge;
            _playerServeTossStart = _ball != null ? _ball.localPosition : (_playerLocalPos + new Vector3(0.18f, 1.25f, 0.35f));
            _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
            _player.Humanoid?.PlaySportAnim(SportAnimKind.VolleyballSpike, 0.68f);
            HudRevision++;
        }

        private void ReleasePlayerJumpServe(float charge)
        {
            _playerServeTossing = false;
            _npcSwingTriggered = false;
            bool isGoldenAceServe = charge >= 0.58f && charge <= 0.86f;
            bool isOvercharged = charge > 0.92f;

            PlayHit(isGoldenAceServe ? 0.72f : 0.52f, alwaysHear: true);

            // Risk-reward: overcharging (>92%) can clip the net tape!
            if (isOvercharged && Random.value < 0.55f)
            {
                _flightStart = _playerLocalPos + Vector3.up * 2.15f;
                _flightEnd = new Vector3(Mathf.Clamp(_playerLocalPos.x, -3.2f, 3.2f), 1.85f, -0.25f);
                _flightDuration = 0.45f;
                _flightElapsed = 0f;
                _flightArc = 0.65f;
                SetCallout(SportLoc.L("SERVİS FİLEYE TAKILDI!", "SERVE HIT THE NET!"), new Color(0.96f, 0.38f, 0.32f), 1.35f);
                AwardVolleyPoint(tealWon: false, _flightEnd);
                return;
            }

            float aimX = Mathf.Clamp(_aimTargetLocal.x, -3.55f, 3.55f);
            float aimZ = isGoldenAceServe ? Random.Range(5.4f, 6.9f) : Random.Range(4.0f, 5.8f);

            _targetTeam = 1;
            _targetIdx = 1;
            _touchStep = 0;
            _rallyCount = 1;
            _flightStart = _playerLocalPos + Vector3.up * 2.25f;
            _flightEnd = new Vector3(aimX, 0.95f, aimZ);
            _flightDuration = isGoldenAceServe ? 0.92f : Mathf.Lerp(1.32f, 1.04f, charge);
            _flightElapsed = 0f;
            _flightArc = isGoldenAceServe ? 1.85f : 2.55f;

            float dist = Vector3.Distance(_flightStart, _flightEnd);
            _lastHitKmh = Mathf.Clamp((dist / _flightDuration) * 6.4f, 52f, 108f);

            if (isGoldenAceServe)
            {
                SetTrailColor(new Color(1.0f, 0.86f, 0.24f, 0.92f));
                if (_ballTrail != null) _ballTrail.emitting = true;
                GameFeelAudio.SpawnBurst(transform.TransformPoint(_flightStart), new Color(1.0f, 0.86f, 0.24f), count: 12, spread: 0.48f);
                GameFeelAudio.TriggerHitstop(0.036f);
                _cameraRig?.AddShake(0.13f, 0.16f);
                SetCallout(SportLoc.L($"SIÇRAMALI SERVİS! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", $"JUMP SERVE! ({Mathf.RoundToInt(_lastHitKmh)} km/h)"), new Color(1.0f, 0.86f, 0.24f), 1.25f);
            }
            else
            {
                SetTrailColor(new Color(0.28f, 0.92f, 0.84f, 0.70f));
                if (_ballTrail != null) _ballTrail.emitting = true;
                GameFeelAudio.SpawnBurst(transform.TransformPoint(_flightStart), new Color(0.24f, 0.90f, 0.85f), count: 7, spread: 0.35f);
            }
            HudRevision++;
        }

        private void StartServe(int servingTeam)
        {
            _teal[0].transform.localPosition = new Vector3(-1.6f, 0f, -2.4f);
            _teal[1].transform.localPosition = new Vector3(1.6f, 0f, -5.6f);
            _burgundy[0].transform.localPosition = new Vector3(1.6f, 0f, 2.4f);
            _burgundy[1].transform.localPosition = new Vector3(-1.6f, 0f, 5.6f);

            ProceduralHumanoid server = (servingTeam == 0 ? _teal : _burgundy)[1];
            server.PlaySportAnim(SportAnimKind.VolleyballSpike, 0.60f);
            _targetTeam = 1 - servingTeam;
            _targetIdx = 1;
            _touchStep = 0;
            _rallyCount = 0;
            _playerHitQueued = false;
            _playerEarlyBuffered = false;
            _playerAnimPending = false;
            _playerServeTossing = false;
            _npcSwingTriggered = false;
            _blockActiveTimer = 0f;
            _diveTimer = 0f;

            float recvZ = _targetTeam == 0 ? Random.Range(-5.8f, -3.6f) : Random.Range(4.2f, 6.2f);
            _flightStart = server.transform.localPosition + Vector3.up * 1.95f;
            _flightEnd = new Vector3(Random.Range(-2.9f, 2.9f), 1.02f, recvZ);
            _flightDuration = _player != null ? Mathf.Max(1.08f, 1.38f - _leagueTier * 0.12f) : 1.25f;
            _flightElapsed = 0f;
            _flightArc = 2.55f;
            PlayHit(0.45f, alwaysHear: _player != null);
        }

        private void SetCallout(string text, Color color, float duration)
        {
            _calloutText = text;
            _calloutColor = color;
            _calloutTimer = duration;
            HudRevision++;
        }

        private void Update()
        {
            if (_teal == null || _burgundy == null || _ball == null) return;
            if (_player != null && PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_blockActiveTimer > 0f) _blockActiveTimer -= dt;
            if (_blockCooldown > 0f) _blockCooldown -= dt;
            if (_rivalBlockerJumpTimer > 0f) _rivalBlockerJumpTimer -= dt;
            if (_calloutTimer > 0f)
            {
                _calloutTimer -= dt;
                if (_calloutTimer <= 0f) HudRevision++;
            }

            if (_player != null)
            {
                _stamina = Mathf.Min(100f, _stamina + 18f * dt);
            }

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= dt;
                IdleTeam(_teal);
                IdleTeam(_burgundy);
                if (_player != null) _player.Humanoid?.SetLocomotion(0f, true, 0f);
                if (_landingRing != null) _landingRing.gameObject.SetActive(false);
                if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);

                if (_pauseTimer <= 0f)
                {
                    if (!_matchOver)
                    {
                        if (_player != null && _lastPointTeal) SetupPlayerServe();
                        else StartServe(_player != null ? 1 : (_tealScore + _burgundyScore) % 2);
                    }
                    HudRevision++;
                }
                return;
            }

            if (_matchOver)
            {
                IdleTeam(_teal);
                IdleTeam(_burgundy);
                if (_player != null) _player.Humanoid?.SetLocomotion(0f, true, 0f);
                if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);
                return;
            }

            if (_player != null && _awaitingPlayerServe)
            {
                Vector2 input = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
                if (_isChargingServe)
                {
                    _serveCharge += dt / 0.82f;
                    // Aim live on rival court while charging serve!
                    _aimTargetLocal.x = Mathf.Clamp(_aimTargetLocal.x + input.x * 5.2f * dt, -3.55f, 3.55f);
                    _aimTargetLocal.y = Mathf.Clamp(_aimTargetLocal.y + input.y * 3.8f * dt, 3.6f, 6.8f);
                    if (_serveCharge >= 1.0f)
                    {
                        _serveCharge = 1.0f;
                        TriggerPrimaryRelease(0.85f);
                    }
                }
                else if (Mathf.Abs(input.x) > 0.1f)
                {
                    _playerLocalPos.x = Mathf.Clamp(_playerLocalPos.x + input.x * 3.1f * dt, -2.9f, 2.9f);
                    _aimTargetLocal.x = Mathf.Clamp(_playerLocalPos.x * 0.9f, -3.4f, 3.4f);
                    _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
                    _player.Humanoid?.SetLocomotion(Mathf.Abs(input.x) * 2.5f, true, 0f, transform.right * Mathf.Sign(input.x), sampleGround: false);
                }
                else
                {
                    _player.Humanoid?.SetLocomotion(0f, true, 0f);
                }

                UpdateAimReticle(show: true);
                Vector3 holdPos = _playerLocalPos + new Vector3(0.08f, 1.18f + Mathf.Sin(Time.time * 4f) * 0.03f, 0.38f);
                UpdateBallAndShadow(holdPos, dt);
                Vector3 ballW = _ball.position;
                Vector3 pRight = _player.transform.right;
                _player.Humanoid?.SetDualHandAction(true,
                    ballW - pRight * 0.11f - Vector3.up * 0.04f,
                    ballW + pRight * 0.11f - Vector3.up * 0.04f,
                    0.85f, 0.85f,
                    spineLeanDeg: _isChargingServe ? 10f : 4f,
                    headPitchDeg: -4f,
                    hipsDrop: _isChargingServe ? 0.08f : 0.02f);
                _player.Humanoid?.SetLookAtPoint(true, ballW);
                return;
            }

            if (_player != null && _playerServeTossing)
            {
                _playerServeTossTimer -= dt;
                float tossU = Mathf.Clamp01(1f - _playerServeTossTimer / 0.24f);
                Vector3 tossPeak = _playerLocalPos + new Vector3(0.08f, 2.25f, 0.28f);
                Vector3 tossPos = Vector3.Lerp(_playerServeTossStart, tossPeak, Mathf.SmoothStep(0f, 1f, tossU));
                tossPos.y += Mathf.Sin(tossU * Mathf.PI) * 0.22f;
                UpdateBallAndShadow(tossPos, dt);
                _player.Humanoid?.SetLookAtPoint(true, _ball.position);
                if (_playerServeTossTimer <= 0f)
                {
                    ReleasePlayerJumpServe(_playerServePendingCharge);
                }
                return;
            }

            _flightElapsed += dt;
            float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
            float remaining = Mathf.Max(0f, _flightDuration - _flightElapsed);
            Vector3 ballPos = Vector3.Lerp(_flightStart, _flightEnd, u);
            ballPos.y += 4f * _flightArc * u * (1f - u);

            ProceduralHumanoid[] recvTeam = _targetTeam == 0 ? _teal : _burgundy;
            ProceduralHumanoid[] otherTeam = _targetTeam == 0 ? _burgundy : _teal;

            bool targetIsPlayer = _player != null && _targetTeam == 0 && _targetIdx == 1;

            if (_player != null)
            {
                UpdatePlayerVolleyMovement(dt, targetIsPlayer);
                UpdateLandingRing(targetIsPlayer, remaining);
                UpdateAimReticle(show: IsPlayerAttackSet);

                // While teammate sets for our attack, joystick steers rival court aim reticle!
                if (IsPlayerAttackSet && InputHub.Instance != null)
                {
                    Vector2 aim = InputHub.Instance.MoveInput;
                    if (aim.sqrMagnitude > 0.04f)
                    {
                        _aimTargetLocal.x = Mathf.Clamp(aim.x * 3.65f, -3.65f, 3.65f);
                        _aimTargetLocal.y = Mathf.Clamp(4.4f + aim.y * 2.4f, 1.85f, 6.85f);
                    }
                }

                if (targetIsPlayer)
                {
                    if (_playerEarlyBuffered && !_playerHitQueued && remaining <= 0.24f)
                    {
                        LockInPlayerVolley(_playerQueuedTip, timingQuality: 1);
                    }
                    if (_playerAnimPending && remaining <= _playerPendingAnimLead)
                    {
                        _playerAnimPending = false;
                        _player.Humanoid?.PlaySportAnim(_playerPendingAnim, _playerPendingAnimDur);
                    }
                    _playerInHitWindow = !_playerHitQueued && remaining <= PlayerHitWindow && remaining >= 0.02f;
                }
                else
                {
                    _playerInHitWindow = false;
                }
            }

            // Pre-trigger NPC bump/spike right before arrival so the swing/jump meets the ball in mid-air!
            if (!targetIsPlayer && !_npcSwingTriggered && remaining <= 0.22f && recvTeam[_targetIdx] != null && recvTeam[_targetIdx].gameObject.activeSelf)
            {
                ProceduralHumanoid npcHitter = recvTeam[_targetIdx];
                Vector3 dHitter = npcHitter.transform.localPosition - _flightEnd;
                dHitter.y = 0f;
                float npcReach = (_player != null && _targetTeam == 1 && _touchStep == 0) ? (1.42f + _leagueTier * 0.14f) : 4.5f;
                if (dHitter.magnitude <= npcReach)
                {
                    _npcSwingTriggered = true;
                    bool isNpcSpike = _touchStep >= 1 && !(_player != null && _targetTeam == 0 && _targetIdx == 0);
                    npcHitter.PlaySportAnim(isNpcSpike ? SportAnimKind.VolleyballSpike : SportAnimKind.VolleyballBump, isNpcSpike ? 0.60f : 0.48f);
                }
            }

            // Move NPCs with realistic league-scaled speed (so well-aimed spikes & tips genuinely beat them!)
            float rivalSpeed = _player != null ? (3.55f + _leagueTier * 0.55f) : 4.35f;
            float mateSpeed = 4.65f;

            Vector3 catchPos = new Vector3(_flightEnd.x, 0f, _flightEnd.z);
            if (!targetIsPlayer && recvTeam[_targetIdx] != null && recvTeam[_targetIdx].gameObject.activeSelf)
            {
                float spd = _targetTeam == 1 ? rivalSpeed : mateSpeed;
                MoveVolleyPlayer(recvTeam[_targetIdx], catchPos, spd, dt, _targetTeam == 0 ? 1f : -1f);
            }
            if (!(_player != null && _targetTeam == 0 && (1 - _targetIdx) == 1) && recvTeam[1 - _targetIdx] != null && recvTeam[1 - _targetIdx].gameObject.activeSelf)
            {
                MoveVolleyPlayer(recvTeam[1 - _targetIdx], GetReadyPos(_targetTeam, 1 - _targetIdx), 3.0f, dt, _targetTeam == 0 ? 1f : -1f);
            }

            // Rival front blocker tracks player's horizontal position at the net when player is about to spike!
            if (otherTeam[0] != null && otherTeam[0].gameObject.activeSelf)
            {
                Vector3 frontTarget = (_targetTeam == 0 && _touchStep >= 1 && _player != null)
                    ? new Vector3(Mathf.Clamp(_playerLocalPos.x * 0.85f, -3.1f, 3.1f), 0f, 1.25f)
                    : GetReadyPos(1 - _targetTeam, 0);
                MoveVolleyPlayer(otherTeam[0], frontTarget, _targetTeam == 0 ? (2.6f + _leagueTier * 0.45f) : 2.9f, dt, _targetTeam == 0 ? -1f : 1f);
            }
            if (!(_player != null && _targetTeam == 1) && otherTeam[1] != null && otherTeam[1].gameObject.activeSelf)
            {
                MoveVolleyPlayer(otherTeam[1], GetReadyPos(1 - _targetTeam, 1), 2.9f, dt, _targetTeam == 0 ? -1f : 1f);
            }

            if (u >= 1f)
            {
                ResolveBallArrival(ballPos, targetIsPlayer, recvTeam);
            }

            UpdateBallAndShadow(ballPos, dt);
            if (_player != null)
            {
                _player.Humanoid?.SetLookAtPoint(true, _ball.position);
                int hash = (_targetTeam * 100) + (_targetIdx * 10) + _touchStep
                    + (_playerInHitWindow ? 1000 : 0)
                    + (_playerHitQueued ? 2000 : 0)
                    + (CanPlayerNetBlock ? 4000 : 0);
                if (hash != _lastHudHash)
                {
                    _lastHudHash = hash;
                    HudRevision++;
                }
            }
        }

        private void UpdateAimReticle(bool show)
        {
            if (_aimReticle == null) return;
            if (_aimReticle.gameObject.activeSelf != show) _aimReticle.gameObject.SetActive(show);
            if (!show) return;
            _aimReticle.localPosition = new Vector3(_aimTargetLocal.x, 0.045f, _aimTargetLocal.y);
            float pulse = 1.0f + Mathf.Sin(Time.time * 7f) * 0.10f;
            _aimReticle.localScale = new Vector3(pulse, 1f, pulse);
        }

        private void UpdatePlayerVolleyMovement(float dt, bool targetIsPlayer)
        {
            // Active Sand Dive Save slide motion
            if (_diveTimer > 0f)
            {
                _diveTimer -= dt;
                float spdDive = Mathf.Lerp(1.6f, 7.2f, Mathf.Clamp01(_diveTimer / 0.44f));
                _playerLocalPos += _diveDir * (spdDive * dt);
                _playerLocalPos.x = Mathf.Clamp(_playerLocalPos.x, -HalfW + 0.5f, HalfW - 0.5f);
                _playerLocalPos.z = Mathf.Clamp(_playerLocalPos.z, -HalfL + 0.6f, -0.82f);
                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
                _player.Humanoid?.SetLocomotion(0f, true, 0f, transform.TransformDirection(_diveDir), sampleGround: false);
                return;
            }

            Vector2 move = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            Vector3 desired = _playerLocalPos;
            bool userSteering = move.sqrMagnitude > 0.04f;

            // While setting up an attack spike, steering aims on rival court while gently guiding player to takeoff spot
            if (IsPlayerAttackSet)
            {
                Vector3 drop = new Vector3(_flightEnd.x, 0f, _flightEnd.z);
                desired = Vector3.MoveTowards(desired, drop, 5.8f * dt);
            }
            else if (userSteering)
            {
                desired += new Vector3(move.x, 0f, move.y).normalized * (5.25f * dt);
            }
            else if (targetIsPlayer)
            {
                // Light assist so casual players aren't punished, while still needing real positioning/dive for sharp angles!
                Vector3 drop = new Vector3(_flightEnd.x, 0f, _flightEnd.z);
                desired = Vector3.MoveTowards(desired, drop, 3.1f * dt);
            }

            desired.x = Mathf.Clamp(desired.x, -HalfW + 0.5f, HalfW - 0.5f);
            desired.z = Mathf.Clamp(desired.z, -HalfL + 0.6f, -0.82f);

            Vector3 step = desired - _playerLocalPos;
            float spd = step.magnitude / Mathf.Max(0.001f, dt);
            _playerLocalPos = desired;
            _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
            Vector3 wDir = step.sqrMagnitude > 0.0001f ? transform.TransformDirection(step.normalized) : transform.forward;
            _player.Humanoid?.SetLocomotion(spd, true, 0f, wDir, sampleGround: false);
        }

        private void UpdateLandingRing(bool targetIsPlayer, float remaining)
        {
            if (_landingRing == null) return;
            if (!targetIsPlayer)
            {
                if (_landingRing.gameObject.activeSelf) _landingRing.gameObject.SetActive(false);
                return;
            }

            if (!_landingRing.gameObject.activeSelf) _landingRing.gameObject.SetActive(true);
            _landingRing.localPosition = new Vector3(_flightEnd.x, 0.032f, _flightEnd.z);
            float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.25f, _flightDuration));
            float scale = _playerHitQueued ? 1.05f : Mathf.Lerp(1.85f, 0.85f, progress);
            _landingRing.localScale = new Vector3(scale, 0.012f, scale);

            if (_landingRingRenderer != null && _ringProps != null)
            {
                Color c;
                Color em;
                if (_playerHitQueued)
                {
                    c = new Color(1.0f, 0.86f, 0.24f, 1f);
                    em = new Color(0.65f, 0.50f, 0.08f, 1f);
                }
                else if (remaining <= 0.28f)
                {
                    // Golden Perfect Sweet-Spot!
                    c = new Color(1.0f, 0.85f, 0.18f, 1f);
                    em = new Color(0.70f, 0.54f, 0.06f, 1f);
                }
                else if (remaining <= PlayerHitWindow)
                {
                    c = new Color(0.20f, 0.96f, 0.62f, 1f);
                    em = new Color(0.10f, 0.62f, 0.36f, 1f);
                }
                else
                {
                    c = new Color(0.95f, 0.76f, 0.28f, 1f);
                    em = new Color(0.28f, 0.20f, 0.04f, 1f);
                }
                _ringProps.SetColor(BaseColorId, c);
                _ringProps.SetColor(EmissionColorId, em);
                _landingRingRenderer.SetPropertyBlock(_ringProps);
            }
        }

        private void ResolveBallArrival(Vector3 ballPos, bool targetIsPlayer, ProceduralHumanoid[] recvTeam)
        {
            if (targetIsPlayer)
            {
                Vector3 dp = new Vector3(_flightEnd.x - _playerLocalPos.x, 0f, _flightEnd.z - _playerLocalPos.z);
                float maxReach = _diveTimer > 0f ? 2.85f : 2.15f;
                if (!_playerHitQueued || dp.magnitude > maxReach)
                {
                    // Ball hits the sand on player's side!
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(ballPos), new Color(0.92f, 0.82f, 0.58f), count: 12, spread: 0.45f);
                    AwardVolleyPoint(tealWon: false, ballPos);
                    return;
                }

                bool isAttack = _touchStep >= 1;
                bool doTip = isAttack && _playerQueuedTip;
                int quality = _queuedTimingQuality;
                _playerHitQueued = false;
                _playerEarlyBuffered = false;
                _playerInHitWindow = false;
                PlayHit(isAttack ? (doTip ? 0.38f : 0.72f) : 0.45f, alwaysHear: true);

                if (!isAttack)
                {
                    // Clean receive/dig to front setter teammate _teal[0]!
                    _touchStep = 1;
                    _targetIdx = 0;
                    _flightStart = ballPos;
                    _flightEnd = new Vector3(-1.3f, quality == 2 ? 2.25f : 1.90f, -1.95f);
                    _flightDuration = quality == 2 ? 0.88f : 1.02f;
                    _flightElapsed = 0f;
                    _flightArc = quality == 2 ? 1.95f : 1.50f;
                    if (quality == 2)
                    {
                        SetCallout(SportLoc.L("MÜKEMMEL MANŞET! Smaca çık!", "PERFECT BUMP! Ready to Spike!"), new Color(1.0f, 0.86f, 0.24f), 0.95f);
                    }
                    HudRevision++;
                    return;
                }

                // Player Attack (SPIKE vs PLASE TIP) over the 2.43 m net!
                _rallyCount++;
                float targetX = Mathf.Clamp(_aimTargetLocal.x, -3.65f, 3.65f);
                float targetZ = doTip
                    ? Random.Range(1.75f, 2.85f) // Drops right behind the rival net blocker!
                    : Mathf.Clamp(_aimTargetLocal.y, 3.6f, 6.85f);

                // Check if Rival Front Blocker leaps and intercepts a predictable straight spike!
                if (!doTip && _burgundy[0] != null)
                {
                    float blockerX = _burgundy[0].transform.localPosition.x;
                    float netCrossX = Mathf.Lerp(ballPos.x, targetX, 0.32f);
                    float blockThreshold = 0.72f + _leagueTier * 0.20f;
                    if (Mathf.Abs(blockerX - netCrossX) < blockThreshold && quality < 2)
                    {
                        // Rival jumps & blocks the spike back up on player's side (playable cover dig!)
                        _burgundy[0].PlaySportAnim(SportAnimKind.VolleyballBlock, 0.62f);
                        GameFeelAudio.PlayVolleyBlockThud();
                        GameFeelAudio.SpawnBurst(transform.TransformPoint(new Vector3(netCrossX, 2.45f, 0.1f)), new Color(0.95f, 0.34f, 0.32f), count: 12, spread: 0.45f);
                        _cameraRig?.AddShake(0.14f, 0.18f);
                        _targetTeam = 0;
                        _targetIdx = 1;
                        _touchStep = 0;
                        _flightStart = new Vector3(netCrossX, 2.45f, 0.0f);
                        _flightEnd = new Vector3(Mathf.Clamp(netCrossX + Random.Range(-1.4f, 1.4f), -3.2f, 3.2f), 1.0f, Random.Range(-4.5f, -2.6f));
                        _flightDuration = 1.05f;
                        _flightElapsed = 0f;
                        _flightArc = 1.85f;
                        SetCallout(SportLoc.L("RAKİP BLOKLADI · TOPU KURTAR!", "BLOCKED BY RIVAL · COVER THE BALL!"), new Color(1.0f, 0.46f, 0.28f), 1.25f);
                        HudRevision++;
                        return;
                    }
                    else if (doTip)
                    {
                        // Rival blocker jumps for a fake spike — fooled by the soft fingertip Plase!
                        _burgundy[0].PlaySportAnim(SportAnimKind.VolleyballBlock, 0.56f);
                    }
                }

                // Launch REAL ball flight to rival court — NO coin flip, rival physically has to reach it!
                _targetTeam = 1;
                // Closest rival chases the landing spot
                float d0 = _burgundy[0] != null ? Vector2.Distance(new Vector2(_burgundy[0].transform.localPosition.x, _burgundy[0].transform.localPosition.z), new Vector2(targetX, targetZ)) : 99f;
                float d1 = _burgundy[1] != null ? Vector2.Distance(new Vector2(_burgundy[1].transform.localPosition.x, _burgundy[1].transform.localPosition.z), new Vector2(targetX, targetZ)) : 99f;
                _targetIdx = (doTip && d0 < d1 + 0.8f) ? 0 : 1;
                _touchStep = 0;
                _flightStart = ballPos;
                _flightEnd = new Vector3(targetX, 0.92f, targetZ);

                if (doTip)
                {
                    _flightDuration = 0.84f;
                    _flightArc = 1.45f;
                    SetTrailColor(new Color(0.40f, 0.95f, 0.82f, 0.82f));
                    if (_ballTrail != null) _ballTrail.emitting = true;
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(ballPos), new Color(0.40f, 0.96f, 0.82f), count: 8, spread: 0.35f);
                    SetCallout(SportLoc.L("AKILLI PLASE! (Blok Arkası)", "SMART DROP TIP! (Over the Block)"), new Color(0.36f, 0.96f, 0.84f), 1.25f);
                }
                else
                {
                    _flightDuration = quality == 2 ? 0.66f : (quality == 1 ? 0.86f : 1.08f);
                    _flightArc = quality == 2 ? 1.15f : 1.75f;
                    float dist = Vector3.Distance(_flightStart, _flightEnd);
                    _lastHitKmh = Mathf.Clamp((dist / _flightDuration) * 6.5f, 52f, 106f);
                    SetTrailColor(quality == 2 ? new Color(1.0f, 0.84f, 0.20f, 0.95f) : new Color(0.32f, 0.90f, 0.85f, 0.78f));
                    if (_ballTrail != null) _ballTrail.emitting = true;
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(ballPos), quality == 2 ? new Color(1.0f, 0.85f, 0.22f) : new Color(0.32f, 0.92f, 0.82f), count: quality == 2 ? 14 : 8, spread: 0.48f);
                    if (quality == 2)
                    {
                        GameFeelAudio.TriggerHitstop(0.042f);
                        _cameraRig?.AddShake(0.16f, 0.20f);
                        SetCallout(SportLoc.L($"MÜKEMMEL SMAÇ! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", $"MONSTER SPIKE! ({Mathf.RoundToInt(_lastHitKmh)} km/h)"), new Color(1.0f, 0.86f, 0.22f), 1.30f);
                    }
                }
                _flightElapsed = 0f;
                _playerAnimPending = false;
                _npcSwingTriggered = false;
                HudRevision++;
                return;
            }

            // NPC receives/sets/spikes — check REAL physical arrival distance on rival side!
            ProceduralHumanoid hitter = recvTeam[_targetIdx];
            if (_player != null && _targetTeam == 1 && _touchStep == 0 && hitter != null)
            {
                Vector3 dHitter = hitter.transform.localPosition - _flightEnd;
                dHitter.y = 0f;
                float npcReach = 1.32f + _leagueTier * 0.14f;
                if (dHitter.magnitude > npcReach)
                {
                    // REAL WINNER / ACE! Rival physically couldn't reach your shot!
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(ballPos), new Color(0.95f, 0.84f, 0.56f), count: 14, spread: 0.55f);
                    if (_rallyCount <= 1)
                    {
                        _playerAcesScored++;
                        SetCallout(SportLoc.L($"ACE SERVİS SAYISI! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", $"SERVICE ACE! ({Mathf.RoundToInt(_lastHitKmh)} km/h)"), new Color(1.0f, 0.86f, 0.22f), 1.6f);
                    }
                    else
                    {
                        SetCallout(SportLoc.L($"MÜTHİŞ SAYI! (Ralli {_rallyCount})", $"GREAT WINNER! (Rally {_rallyCount})"), new Color(0.28f, 0.95f, 0.78f), 1.5f);
                    }
                    _npcSwingTriggered = false;
                    AwardVolleyPoint(tealWon: true, ballPos);
                    return;
                }
            }

            PlayHit(_touchStep >= 1 ? 0.52f : 0.36f, alwaysHear: _player != null);
            bool alreadySwung = _npcSwingTriggered;
            _npcSwingTriggered = false;

            if (_player != null && _targetTeam == 0 && _targetIdx == 0)
            {
                // Player's front teammate sets a crisp high ball right into the player's attack zone!
                if (!alreadySwung) hitter.PlaySportAnim(SportAnimKind.VolleyballBump, 0.46f);
                _touchStep = 2;
                _targetIdx = 1;
                _playerHitQueued = false;
                _playerEarlyBuffered = false;
                _playerAnimPending = false;
                _flightStart = ballPos;
                _flightEnd = new Vector3(Mathf.Clamp(_playerLocalPos.x * 0.45f + 0.55f, -2.1f, 2.2f), 2.12f, -2.05f);
                _flightDuration = 1.15f;
                _flightElapsed = 0f;
                _flightArc = 2.05f;
                if (_ballTrail != null) _ballTrail.emitting = false;
                HudRevision++;
                return;
            }

            if (_touchStep == 0)
            {
                if (!alreadySwung) hitter.PlaySportAnim(SportAnimKind.VolleyballBump, 0.48f);
                _touchStep = 1;
                _targetIdx = 1 - _targetIdx;
                float frontZ = _targetTeam == 0 ? -1.85f : 1.85f;
                _flightStart = ballPos;
                _flightEnd = new Vector3(Random.Range(-2.3f, 2.3f), 2.08f, frontZ);
                _flightDuration = 0.88f;
                _flightElapsed = 0f;
                _flightArc = 1.65f;
                if (_ballTrail != null) _ballTrail.emitting = false;
            }
            else
            {
                if (!alreadySwung) hitter.PlaySportAnim(SportAnimKind.VolleyballSpike, 0.60f);
                _rallyCount++;

                // CHECK PLAYER DOUBLE-HAND NET ROOF BLOCK!
                if (_player != null && _targetTeam == 1 && _blockActiveTimer > 0f)
                {
                    float blockGapX = Mathf.Abs(_playerLocalPos.x - ballPos.x);
                    if (blockGapX <= 1.85f && _playerLocalPos.z >= -2.9f)
                    {
                        // MONSTER ROOF BLOCK straight onto rival sand!
                        _playerBlocksScored++;
                        GameFeelAudio.PlayVolleyBlockThud();
                        GameFeelAudio.TriggerHitstop(0.048f);
                        _cameraRig?.AddShake(0.22f, 0.24f);
                        SetTrailColor(new Color(0.24f, 0.96f, 0.84f, 0.95f));
                        if (_ballTrail != null) _ballTrail.emitting = true;
                        SetCallout(SportLoc.L("DUVAR BLOK SAYISI!", "MONSTER ROOF BLOCK!"), new Color(0.24f, 0.96f, 0.84f), 1.65f);
                        AwardVolleyPoint(tealWon: true, new Vector3(ballPos.x, 0.35f, 1.35f));
                        return;
                    }
                }

                bool ambientPointOver = _player == null && _rallyCount >= 4 && Random.value < 0.42f;
                if (ambientPointOver)
                {
                    AwardVolleyPoint(_targetTeam == 0, ballPos);
                    return;
                }

                _targetTeam = 1 - _targetTeam;
                _targetIdx = 1;
                _touchStep = 0;
                _playerHitQueued = false;
                _playerEarlyBuffered = false;
                float oppZ = _targetTeam == 0 ? Random.Range(-6.1f, -3.2f) : Random.Range(3.5f, 6.2f);
                _flightStart = ballPos;
                _flightEnd = new Vector3(Random.Range(-3.1f, 3.1f), 1.02f, oppZ);
                _flightDuration = _player != null && _targetTeam == 0 ? Mathf.Max(0.95f, 1.30f - _leagueTier * 0.14f) : 1.10f;
                _flightElapsed = 0f;
                _flightArc = 1.95f;
                SetTrailColor(new Color(0.96f, 0.36f, 0.42f, 0.78f));
                if (_ballTrail != null) _ballTrail.emitting = true;
            }
        }

        private void AwardVolleyPoint(bool tealWon, Vector3 burstPos)
        {
            _lastPointTeal = tealWon;
            if (tealWon) _tealScore++; else _burgundyScore++;
            UpdateScoreboard();
            _playerHitQueued = false;
            _playerEarlyBuffered = false;
            _playerInHitWindow = false;
            _isChargingServe = false;
            if (_landingRing != null) _landingRing.gameObject.SetActive(false);
            if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);

            ProceduralHumanoid[] winners = tealWon ? _teal : _burgundy;
            for (int i = 0; i < winners.Length; i++)
            {
                if (winners[i] != null && winners[i].gameObject.activeSelf)
                {
                    winners[i].PlaySportAnim(SportAnimKind.VictoryJump, 0.64f);
                }
            }

            if (_player != null)
            {
                GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                if (tealWon)
                {
                    _player.Humanoid?.PlaySportAnim(SportAnimKind.VictoryJump, 0.72f);
                    GameFeelAudio.PlayCrowdCheer(bigCheer: _rallyCount >= 3);
                    GameFeelAudio.PlayCoinChime();
                }

                if (_tealScore >= PointsToWin || _burgundyScore >= PointsToWin)
                {
                    _matchOver = true;
                    _playerWonMatch = _tealScore >= PointsToWin;
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: true);
                    if (_playerWonMatch)
                    {
                        int reward = 85 + _leagueTier * 40;
                        GameFeelAudio.PlayMilestoneFanfare();
                        SaveManager.Instance?.AddMoney(reward);
                        if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                        {
                            SaveManager.Instance.Data.volleyballWins++;
                            if (SaveManager.Instance.Data.volleyballLeagueTier < 2)
                            {
                                SaveManager.Instance.Data.volleyballLeagueTier++;
                            }
                            SaveManager.Instance.Save();
                        }
                        NeighborhoodReputation.AddReputation(35 + _leagueTier * 10, SportLoc.L($"Voleybol [{GetLeagueLabel()}] Zaferi! (+{reward} ₺)", $"Volleyball [{GetLeagueLabel()}] Victory! (+{reward} ₺)"));
                    }
                    SportMatchEvents.NotifyMatchFinished("sport_volleyball", _playerWonMatch);
                }
                HudRevision++;
            }

            if (_player != null || IsPlayerNear(24f))
            {
                GameFeelAudio.SpawnBurst(
                    transform.TransformPoint(burstPos + Vector3.up * 0.4f),
                    tealWon ? new Color(0.18f, 0.88f, 0.78f) : new Color(0.78f, 0.22f, 0.34f),
                    count: 16, spread: 0.60f);
            }
            _pauseTimer = _matchOver ? 1.0f : 1.75f;
        }

        private void UpdateBallAndShadow(Vector3 ballPos, float dt)
        {
            _ball.localPosition = ballPos;
            _ball.Rotate(Vector3.one * (dt * 180f), Space.Self);
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(ballPos.x, 0.024f, ballPos.z);
                float s = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(ballPos.y / 4.5f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private static Vector3 GetReadyPos(int team, int idx)
        {
            float zSign = team == 0 ? -1f : 1f;
            return idx == 0
                ? new Vector3(team == 0 ? -1.4f : 1.4f, 0f, zSign * 2.1f)
                : new Vector3(team == 0 ? 1.5f : -1.5f, 0f, zSign * 5.2f);
        }

        private void MoveVolleyPlayer(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, float netDirZ)
        {
            localTarget.x = Mathf.Clamp(localTarget.x, -HalfW + 0.5f, HalfW - 0.5f);
            localTarget.z = Mathf.Clamp(localTarget.z, -HalfL + 0.6f, HalfL - 0.6f);
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float spd = 0f;
            Vector3 wDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                spd = Mathf.Min(maxSpeed, dist * 4.6f);
                Vector3 dir = delta / dist;
                pos += dir * (spd * dt);
                hum.transform.localPosition = pos;
                wDir = transform.TransformDirection(dir);
            }

            Quaternion faceNet = Quaternion.LookRotation(new Vector3(0f, 0f, netDirZ), Vector3.up);
            hum.transform.localRotation = Quaternion.Slerp(hum.transform.localRotation, faceNet, 1f - Mathf.Exp(-9f * dt));
            hum.SetLocomotion(spd, true, 0f, wDir, sampleGround: false);
            if (_ball != null) hum.SetLookAtPoint(true, _ball.position);
        }

        private void IdleTeam(ProceduralHumanoid[] team)
        {
            for (int i = 0; i < team.Length; i++)
            {
                if (team[i] != null && team[i].gameObject.activeSelf)
                {
                    team[i].SetLocomotion(0f, true, 0f, team[i].transform.forward, sampleGround: false);
                }
            }
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = SportLoc.L($"HAMİDİYE VOLEYBOL\nTURKUAZ {_tealScore} - {_burgundyScore} BORDO", $"HAMIDIYE VOLLEYBALL\nTEAL {_tealScore} - {_burgundyScore} BURGUNDY");
            }
        }

        private void PlayHit(float vol, bool alwaysHear = false)
        {
            if (_audio == null || _hitClip == null) return;
            if (!alwaysHear && _player == null && !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.94f, 1.08f);
            _audio.PlayOneShot(_hitClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }
}
