using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Lightweight event bus broadcasting completed player sport matches (Tennis, Football, Basketball, Volleyball)
    /// so story missions and achievements react cleanly without tight coupling.
    /// </summary>
    public static class SportMatchEvents
    {
        public static event System.Action<string, bool> OnMatchFinished;

        public static void NotifyMatchFinished(string sportId, bool playerWon)
        {
            OnMatchFinished?.Invoke(sportId, playerWon);
        }
    }

    /// <summary>
    /// Live real-time HUD meter payload rendered under the top scoreboard pill
    /// (shot/serve charge power, spike/dig timing sweet-spot, stamina bar, and dynamic highlight badge).
    /// </summary>
    public struct SportHudMeterState
    {
        public bool ShowPrimaryBar;
        public float PrimaryFill01;
        public Color PrimaryColor;
        public string PrimaryLabel;
        public bool ShowSweetSpot;
        public float SweetSpotMin01;
        public float SweetSpotMax01;
        public bool ShowStaminaBar;
        public float StaminaFill01;
        public string CalloutBadge;
        public Color CalloutColor;
    }

    /// <summary>
    /// Shared runtime contract for any playable neighborhood sports arena
    /// (3v3 Halı Saha Football, 2v2 Streetball Basketball, 2v2 Sand Volleyball).
    /// </summary>
    public interface IPlayableSportArena
    {
        bool IsPlayerActive { get; }
        int HudRevision { get; }
        bool AcceptsPrimaryAction { get; }
        bool HasSecondaryAction { get; }
        Vector3 ReturnPosition { get; }
        Quaternion ReturnRotation { get; }
        string HudText();
        string PrimaryButtonLabel();
        string SecondaryButtonLabel();
        string ControlsHintKey();
        void BeginPlayerMatch(PlayerFootController player);
        void EndPlayerMatch();
        void TriggerPrimaryAction();
        void TriggerSecondaryAction();
        void TriggerPrimaryRelease(float holdSeconds);
        void TriggerSecondaryRelease(float holdSeconds);
        SportHudMeterState GetHudMeterState();
    }

    /// <summary>
    /// Realistic 3v3 Halı Saha football (soccer) arena:
    /// - Hold-to-charge shots with golden sweet-spot, real Magnus curve physics, and crossbar/post rebounds
    /// - Tap ground pass vs hold through-ball lob with smart 1-2 wall-pass teammate runs
    /// - Bilek çalımı (step-over feint) on sprint tap + diving turf slide tackles with stamina management
    /// - Real horizontal goalkeeper diving saves, parry rebounds, ball speed (km/h), trails & 3 league tiers!
    /// </summary>
    public sealed class LiveFootballMatch : MonoBehaviour, IPlayableSportArena
    {
        private const float HalfW = 7.6f;
        private const float HalfL = 11.4f;
        private const float GoalHalfW = 2.05f;
        private const float CrossbarY = 2.18f;
        private const float BallRadius = 0.13f;
        private const int GoalsToWin = 3;

        private ProceduralHumanoid[] _red;
        private ProceduralHumanoid[] _blue;
        private Transform _ball;
        private Transform _ballShadow;
        private TrailRenderer _ballTrail;
        private Material _ballTrailMat;
        private Transform _shotAimMarker;
        private Material _aimMarkerMat;
        private Material _aimMarkerCoreMat;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _kickClip;

        private Vector3 _ballPos;
        private Vector3 _ballVel;
        private Vector3 _ballCurveAccel; // Real lateral Magnus curve during curved shots!
        private int _carrierTeam = 0; // 0 = red, 1 = blue, -1 = loose/in flight
        private int _carrierIdx = 1;
        private float _actionTimer = 1.2f;
        private float _goalResetTimer;
        private int _redScore;
        private int _blueScore;
        private float _dribbleStep;

        private Vector3[] _redHome;
        private Vector3[] _blueHome;

        // Player-controlled match state
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;
        private Vector3 _playerLocalPos;
        private Quaternion _playerLocalRot = Quaternion.identity;
        private bool _matchOver;
        private bool _playerWonMatch;
        private bool _lastGoalByRed;
        private float _stealImmunityTimer;
        private float _dashBoostTimer;
        private float _statusBannerTimer;
        private string _statusBannerText;
        private Color _statusBannerColor = new Color(0.24f, 0.92f, 0.78f);
        private int _lastHudStateHash;

        // Realistic football mechanics state
        private int _leagueTier; // 0 = Acemi, 1 = Mahalle, 2 = Şampiyonlar
        private float _stamina = 100f;
        private bool _isChargingShot;
        private float _shotCharge;
        private bool _isChargingPass;
        private float _passCharge;
        private bool _wasSprintHeld;
        private float _feintCooldown;
        private float _slideTimer;
        private float _slideRecoveryTimer;
        private float _slideCooldown;
        private Vector3 _slideDir;
        private bool _slideConnected;
        private float _wallPassRunTimer;
        private float _blueStunTimer;
        private bool _blueGkDiving;
        private float _blueGkDiveTimer;
        private float _blueGkDiveTargetX;
        private bool _redGkDiving;
        private float _redGkDiveTimer;
        private float _redGkDiveTargetX;
        private float _lastShotKmh;
        private int _playerShotsOnTarget;
        private int _playerTacklesWon;
        private float _playerMoveSpeed;

        // Foot-contact synced kick launch (holds ball at foot during 0.15s SoccerKick windup)
        private bool _pendingKickActive;
        private float _pendingKickTimer;
        private Vector3 _pendingKickVel;
        private Vector3 _pendingKickCurve;
        private float _pendingKickSoundVol;
        private bool _pendingKickAlwaysHear;
        private bool _pendingKickSpawnBurst;
        private Color _pendingKickBurstColor;
        private int _pendingKickBurstCount;
        private float _pendingKickBurstSpread;
        private bool _pendingKickHitstop;
        private bool _pendingKickShake;
        private float _pendingKickShakeAmt;
        private float _pendingKickShakeDur;
        private float _pendingKickActionTimer;

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool HasSecondaryAction => _player != null && !_matchOver && _goalResetTimer <= 0f;
        public bool AcceptsPrimaryAction => _player != null && (_matchOver || _goalResetTimer <= 0f);

        private bool PlayerHasBall => _player != null && _carrierTeam == 0 && _carrierIdx == 1;

        public void Configure(
            ProceduralHumanoid[] redTeam,
            ProceduralHumanoid[] blueTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip kickClip)
        {
            _red = redTeam;
            _blue = blueTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _kickClip = kickClip;

            _redHome = new[]
            {
                new Vector3(0f, 0f, -HalfL + 0.9f),
                new Vector3(-2.6f, 0f, -2.2f),
                new Vector3(3.0f, 0f, -1.5f)
            };
            _blueHome = new[]
            {
                new Vector3(0f, 0f, HalfL - 0.9f),
                new Vector3(-3.0f, 0f, 2.2f),
                new Vector3(3.0f, 0f, 3.2f)
            };

            ResetKickoff(0);
            UpdateScoreboard();
        }

        private void OnDestroy()
        {
            if (_ballTrailMat != null) Destroy(_ballTrailMat);
            if (_aimMarkerMat != null) Destroy(_aimMarkerMat);
            if (_aimMarkerCoreMat != null) Destroy(_aimMarkerCoreMat);
        }

        private void EnsureVisualEffects()
        {
            if (_ball != null && _ballTrail == null)
            {
                _ballTrail = _ball.gameObject.GetComponent<TrailRenderer>();
                if (_ballTrail == null) _ballTrail = _ball.gameObject.AddComponent<TrailRenderer>();
                _ballTrail.time = 0.30f;
                _ballTrail.minVertexDistance = 0.08f;
                _ballTrail.startWidth = 0.22f;
                _ballTrail.endWidth = 0.02f;
                _ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ballTrail.receiveShadows = false;
                Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (sh != null)
                {
                    _ballTrailMat = new Material(sh);
                    _ballTrail.sharedMaterial = _ballTrailMat;
                }
                SetTrailColor(new Color(0.28f, 0.94f, 0.84f, 0.78f));
                _ballTrail.emitting = false;
            }

            if (_shotAimMarker == null)
            {
                // Vertical glowing 3D bullseye inside the goal mouth that rises toward the top 90 corner as you charge!
                GameObject aimRoot = new GameObject("Football_GoalAimReticle");
                aimRoot.transform.SetParent(transform, false);

                GameObject outerDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outerDisc.name = "OuterRing";
                Destroy(outerDisc.GetComponent<Collider>());
                outerDisc.transform.SetParent(aimRoot.transform, false);
                outerDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                outerDisc.transform.localScale = new Vector3(1.05f, 0.03f, 1.05f);

                GameObject innerCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                innerCore.name = "InnerBullseye";
                Destroy(innerCore.GetComponent<Collider>());
                innerCore.transform.SetParent(aimRoot.transform, false);
                innerCore.transform.localPosition = new Vector3(0f, 0f, -0.04f);
                innerCore.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                innerCore.transform.localScale = new Vector3(0.46f, 0.03f, 0.46f);

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (unlit != null)
                {
                    _aimMarkerMat = new Material(unlit);
                    _aimMarkerMat.color = new Color(1.0f, 0.78f, 0.10f, 1f);
                    if (_aimMarkerMat.HasProperty("_BaseColor")) _aimMarkerMat.SetColor("_BaseColor", new Color(1.0f, 0.78f, 0.10f, 1f));
                    MeshRenderer mrOut = outerDisc.GetComponent<MeshRenderer>();
                    if (mrOut != null) { mrOut.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrOut.sharedMaterial = _aimMarkerMat; }

                    _aimMarkerCoreMat = new Material(unlit);
                    _aimMarkerCoreMat.color = new Color(1.0f, 0.24f, 0.12f, 1f);
                    if (_aimMarkerCoreMat.HasProperty("_BaseColor")) _aimMarkerCoreMat.SetColor("_BaseColor", new Color(1.0f, 0.24f, 0.12f, 1f));
                    MeshRenderer mrIn = innerCore.GetComponent<MeshRenderer>();
                    if (mrIn != null) { mrIn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrIn.sharedMaterial = _aimMarkerCoreMat; }
                }

                _shotAimMarker = aimRoot.transform;
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
            return _leagueTier switch
            {
                2 => "Şampiyonlar",
                1 => "Mahalle Ligi",
                _ => "Acemi Lig"
            };
        }

        public void BeginPlayerMatch(PlayerFootController player)
        {
            if (player == null) return;
            _player = player;
            _beginPos = player.transform.position;
            _beginRot = player.transform.rotation;
            _redScore = 0;
            _blueScore = 0;
            _matchOver = false;
            _playerWonMatch = false;
            _goalResetTimer = 0f;
            _statusBannerTimer = 0f;
            _statusBannerText = null;
            _stamina = 100f;
            _isChargingShot = false;
            _shotCharge = 0f;
            _isChargingPass = false;
            _passCharge = 0f;
            _pendingKickActive = false;
            _pendingKickTimer = 0f;
            _slideTimer = 0f;
            _slideRecoveryTimer = 0f;
            _slideCooldown = 0f;
            _feintCooldown = 0f;
            _wallPassRunTimer = 0f;
            _blueStunTimer = 0f;
            _playerShotsOnTarget = 0;
            _playerTacklesWon = 0;
            _leagueTier = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? Mathf.Clamp(SaveManager.Instance.Data.footballLeagueTier, 0, 2)
                : 0;

            EnsureVisualEffects();

            if (_red != null && _red.Length > 1 && _red[1] != null)
            {
                _red[1].gameObject.SetActive(false);
            }

            player.SetSeatedState(true);
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(true, distance: 9.4f, pitch: 29f, fov: 62f, lockYaw: transform.eulerAngles.y);
            }

            ResetKickoff(0);
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
            SetCallout($"{GetLeagueLabel()} · Maç Başladı!", new Color(0.28f, 0.94f, 0.80f), 1.6f);
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
            if (_shotAimMarker != null) _shotAimMarker.gameObject.SetActive(false);
            if (_ballTrail != null) _ballTrail.emitting = false;

            if (_player != null)
            {
                _player.Humanoid?.SetLookAtPoint(false, Vector3.zero);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
                _player.SetSeatedState(false);
            }
            _player = null;
            _matchOver = false;
            _isChargingShot = false;
            _isChargingPass = false;
            _pendingKickActive = false;
            _pendingKickTimer = 0f;

            if (_red != null && _red.Length > 1 && _red[1] != null)
            {
                _red[1].gameObject.SetActive(true);
            }

            _redScore = 0;
            _blueScore = 0;
            ResetKickoff(0);
            UpdateScoreboard();
        }

        public string ControlsHintKey() => "sports.football.controls";

        public string PrimaryButtonLabel()
        {
            if (_matchOver) return LocalizationService.Tr("sports.btn.rematch", "Tekrar");
            if (_isChargingShot) return "BIRAK!";
            if (PlayerHasBall) return LocalizationService.Tr("sports.btn.shoot", "Şut (Tut)");
            if (_carrierTeam == 0 && _carrierIdx == 2) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            if (_carrierTeam == -1 && Vector3.Distance(new Vector3(_ballPos.x, 0f, _ballPos.z), _playerLocalPos) <= 2.65f)
                return "Vole!";
            return LocalizationService.Tr("sports.btn.steal", "Pres");
        }

        public string SecondaryButtonLabel()
        {
            if (_isChargingPass) return "ARA PASI!";
            if (PlayerHasBall) return LocalizationService.Tr("sports.btn.pass", "Pas / Ara");
            if (_carrierTeam == 0 && _carrierIdx == 2) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            return "Kay!";
        }

        public string HudText()
        {
            string title = LocalizationService.Tr("sports.football.title", "Halı Saha");
            string redLbl = LocalizationService.Tr("sports.football.team_you", "Sen");
            string blueLbl = LocalizationService.Tr("sports.football.team_rival", "Mavi");
            string status;

            if (_matchOver)
            {
                string winTxt = _playerWonMatch
                    ? $"ZAFER! ({_playerShotsOnTarget} Şut · {_playerTacklesWon} Kapma) — Tekrar için Vur"
                    : $"Maç Bitti ({_playerShotsOnTarget} Şut) — Rövanş için Vur";
                status = winTxt;
            }
            else if (_goalResetTimer > 0f)
            {
                status = _lastGoalByRed
                    ? $"GOOOL! ({Mathf.RoundToInt(_lastShotKmh)} km/s)"
                    : "Mavi Takım Gol Attı! Santra hazırlanıyor...";
            }
            else if (_isChargingShot)
            {
                status = "Güç doldur · Altın bölgede bırak → 90'a Falsolu Şut!";
            }
            else if (_isChargingPass)
            {
                status = "Kısa bırak: Pas · Basılı tut: Defans Arkası Ara Pası!";
            }
            else if (PlayerHasBall)
            {
                status = "Şut (Basılı Tut) · Pas/Ara Pası · Koş dokun: Bilek Çalımı!";
            }
            else if (_carrierTeam == 0)
            {
                status = "Boşluğa koş · Pas İste ile verkaç yap!";
            }
            else if (_carrierTeam == 1)
            {
                status = "Pres (Vur) veya Kayarak Müdahale (Kay!) ile topu kap!";
            }
            else
            {
                status = "Serbest top! Yaklaşınca tek dokunuş vole vurabilirsin!";
            }

            return $"{title} [{GetLeagueLabel()}]  ·  {redLbl} {_redScore} - {_blueScore} {blueLbl}\n{status}";
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = _player != null && !_matchOver,
                StaminaFill01 = Mathf.Clamp01(_stamina / 100f),
                CalloutBadge = _statusBannerTimer > 0f ? _statusBannerText : null,
                CalloutColor = _statusBannerColor
            };

            if (_isChargingShot)
            {
                bool inSweet = _shotCharge >= 0.52f && _shotCharge <= 0.86f;
                bool over = _shotCharge > 0.90f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _shotCharge;
                state.PrimaryColor = over
                    ? new Color(0.96f, 0.30f, 0.24f)
                    : (inSweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.90f, 0.72f));
                state.PrimaryLabel = over ? "AŞIRI GÜÇ!" : (inSweet ? "ALTIN FALSO!" : "ŞUT GÜCÜ");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.52f;
                state.SweetSpotMax01 = 0.86f;
            }
            else if (_isChargingPass)
            {
                bool through = _passCharge >= 0.45f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _passCharge;
                state.PrimaryColor = through ? new Color(0.32f, 0.82f, 1.0f) : new Color(0.24f, 0.90f, 0.66f);
                state.PrimaryLabel = through ? "ARA PASI (LOB)" : "YERDEN PAS";
                state.ShowSweetSpot = false;
            }

            return state;
        }

        public void TriggerPrimaryAction()
        {
            if (_player == null) return;

            if (_matchOver)
            {
                if (_goalResetTimer <= 0f)
                {
                    _redScore = 0;
                    _blueScore = 0;
                    _matchOver = false;
                    _playerShotsOnTarget = 0;
                    _playerTacklesWon = 0;
                    _stamina = 100f;
                    ResetKickoff(0);
                    UpdateScoreboard();
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                    HudRevision++;
                }
                return;
            }

            if (_goalResetTimer > 0f || _slideTimer > 0f || _slideRecoveryTimer > 0f) return;

            if (PlayerHasBall)
            {
                // Begin hold-to-charge power & curve shot!
                _isChargingShot = true;
                _shotCharge = 0.08f;
                _isChargingPass = false;
                HudRevision++;
                return;
            }

            // Ask Red teammate for an immediate crisp wall-pass!
            if (_carrierTeam == 0 && _carrierIdx == 2 && _red != null && _red.Length > 2 && _red[2] != null)
            {
                ProceduralHumanoid mate = _red[2];
                mate.PlaySportAnim(SportAnimKind.SoccerKick, 0.45f);
                PlayKickSound(0.45f, alwaysHear: true);
                Vector2 move = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
                Vector3 lead = _playerLocalPos + new Vector3(move.x * 1.4f, 0f, Mathf.Max(0.9f, move.y * 2.2f));
                lead.x = Mathf.Clamp(lead.x, -HalfW + 1f, HalfW - 1f);
                lead.z = Mathf.Clamp(lead.z, -HalfL + 1.5f, HalfL - 1.5f);
                Vector3 delta = lead - _ballPos;
                float t = Mathf.Clamp(delta.magnitude / 11.4f, 0.24f, 0.60f);
                _ballVel = new Vector3(delta.x / t, 1.45f, delta.z / t);
                _ballCurveAccel = Vector3.zero;
                _carrierTeam = -1;
                _actionTimer = 0.12f;
                SetCallout("VERKAÇ PASI GELDİ!", new Color(0.28f, 0.92f, 0.80f), 1.1f);
                return;
            }

            // One-touch volley rocket if loose ball is within reach!
            if (_carrierTeam == -1)
            {
                Vector3 dBall = _ballPos - _playerLocalPos;
                dBall.y = 0f;
                if (dBall.magnitude <= 2.65f)
                {
                    FireChargedShot(charge: 0.76f, isVolley: true);
                    return;
                }
            }

            // Standing press / tackle attempt on Blue carrier
            TryPlayerSteal();
        }

        public void TriggerPrimaryRelease(float holdSeconds)
        {
            if (!_isChargingShot) return;
            float finalCharge = Mathf.Clamp01(Mathf.Max(_shotCharge, holdSeconds / 0.82f));
            _isChargingShot = false;
            _shotCharge = 0f;
            if (PlayerHasBall && !_matchOver && _goalResetTimer <= 0f)
            {
                FireChargedShot(finalCharge, isVolley: false);
            }
            HudRevision++;
        }

        public void TriggerSecondaryAction()
        {
            if (_player == null || _matchOver || _goalResetTimer > 0f) return;
            if (_slideTimer > 0f || _slideRecoveryTimer > 0f) return;

            if (PlayerHasBall)
            {
                _isChargingPass = true;
                _passCharge = 0.08f;
                _isChargingShot = false;
                HudRevision++;
                return;
            }

            if (_carrierTeam == 0 && _carrierIdx == 2)
            {
                TriggerPrimaryAction();
                return;
            }

            // Defending: execute explosive turf Slide Tackle!
            TryPlayerSlideTackle();
        }

        public void TriggerSecondaryRelease(float holdSeconds)
        {
            if (!_isChargingPass) return;
            float charge = Mathf.Clamp01(Mathf.Max(_passCharge, holdSeconds / 0.55f));
            _isChargingPass = false;
            _passCharge = 0f;

            if (PlayerHasBall && !_pendingKickActive && _red != null && _red.Length > 2 && _red[2] != null && !_matchOver && _goalResetTimer <= 0f)
            {
                bool isThroughBall = charge >= 0.45f;
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerKick, 0.44f);

                Vector3 matePos = _red[2].transform.localPosition + new Vector3(0f, 0f, isThroughBall ? 4.2f : 1.7f);
                matePos.x = Mathf.Clamp(matePos.x, -HalfW + 1.1f, HalfW - 1.1f);
                matePos.z = Mathf.Clamp(matePos.z, -HalfL + 1.6f, HalfL - 1.5f);
                Vector3 delta = matePos - _ballPos;
                float speed = isThroughBall ? 12.2f : 10.6f;
                float t = Mathf.Clamp(delta.magnitude / speed, 0.28f, 0.72f);
                float loft = isThroughBall ? 4.4f : 1.45f;
                QueueSyncedKick(
                    vel: new Vector3(delta.x / t, loft, delta.z / t),
                    curve: Vector3.zero,
                    delay: 0.14f,
                    actionTimer: 0.20f,
                    soundVol: isThroughBall ? 0.52f : 0.40f,
                    alwaysHear: true);
                _wallPassRunTimer = 2.6f; // Teammate sprints into attack channel for 1-2 return!
                SetTrailColor(new Color(0.32f, 0.88f, 1.0f, 0.75f));
                SetCallout(isThroughBall ? "DEFANS ARKASI ARA PASI!" : "VERKAÇ PASI!", new Color(0.30f, 0.88f, 1.0f), 1.15f);
            }
            HudRevision++;
        }

        private void QueueSyncedKick(
            Vector3 vel,
            Vector3 curve,
            float delay,
            float actionTimer,
            float soundVol,
            bool alwaysHear,
            bool spawnBurst = false,
            Color burstColor = default,
            int burstCount = 0,
            float burstSpread = 0.35f,
            bool hitstop = false,
            bool shake = false,
            float shakeAmt = 0f,
            float shakeDur = 0f)
        {
            _pendingKickActive = true;
            _pendingKickTimer = delay;
            _pendingKickVel = vel;
            _pendingKickCurve = curve;
            _pendingKickActionTimer = actionTimer;
            _pendingKickSoundVol = soundVol;
            _pendingKickAlwaysHear = alwaysHear;
            _pendingKickSpawnBurst = spawnBurst;
            _pendingKickBurstColor = burstColor;
            _pendingKickBurstCount = burstCount;
            _pendingKickBurstSpread = burstSpread;
            _pendingKickHitstop = hitstop;
            _pendingKickShake = shake;
            _pendingKickShakeAmt = shakeAmt;
            _pendingKickShakeDur = shakeDur;
        }

        private void ReleasePendingKick()
        {
            _pendingKickActive = false;
            _ballVel = _pendingKickVel;
            _ballCurveAccel = _pendingKickCurve;
            _carrierTeam = -1;
            _actionTimer = _pendingKickActionTimer;
            PlayKickSound(_pendingKickSoundVol, alwaysHear: _pendingKickAlwaysHear);
            if (_pendingKickSpawnBurst)
            {
                GameFeelAudio.SpawnBurst(transform.TransformPoint(_ballPos + Vector3.up * 0.16f), _pendingKickBurstColor, count: _pendingKickBurstCount, spread: _pendingKickBurstSpread);
            }
            if (_pendingKickHitstop)
            {
                GameFeelAudio.TriggerHitstop(0.038f);
            }
            if (_pendingKickShake)
            {
                _cameraRig?.AddShake(_pendingKickShakeAmt, _pendingKickShakeDur);
            }
        }

        private void FireChargedShot(float charge, bool isVolley)
        {
            if (_pendingKickActive) return;
            float aimInput = InputHub.Instance != null ? InputHub.Instance.MoveInput.x : 0f;
            float gkX = (_blue != null && _blue.Length > 0 && _blue[0] != null) ? _blue[0].transform.localPosition.x : 0f;

            bool isPlacedLow = !isVolley && charge < 0.26f;
            bool isGoldenCurve = isVolley || (charge >= 0.52f && charge <= 0.86f);
            bool isOvercharged = !isVolley && charge > 0.90f;

            float aimX = Mathf.Abs(aimInput) >= 0.14f
                ? Mathf.Clamp(aimInput, -1f, 1f) * (GoalHalfW - 0.14f)
                : (gkX >= 0f ? -GoalHalfW * 0.76f : GoalHalfW * 0.76f);

            // Overcharged shots risk hitting the woodwork or sailing slightly high!
            float targetY;
            if (isOvercharged)
            {
                targetY = Random.Range(2.10f, 2.55f);
                aimX += (aimX >= 0f ? 0.24f : -0.24f);
            }
            else if (isGoldenCurve)
            {
                targetY = Random.Range(1.38f, 1.82f); // Crisp top-corner 90'a placement!
            }
            else if (isPlacedLow)
            {
                targetY = 0.34f; // Razor-sharp low grass cutter
            }
            else
            {
                targetY = Mathf.Lerp(0.60f, 1.35f, charge);
            }

            _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerKick, 0.52f);

            Vector3 shotTarget = new Vector3(aimX, targetY, HalfL + 0.65f);
            Vector3 delta = shotTarget - _ballPos;
            float speed = isGoldenCurve ? (isVolley ? 21.8f : 20.4f)
                : (isOvercharged ? 23.0f : (isPlacedLow ? 15.0f : Mathf.Lerp(15.5f, 18.8f, charge)));
            float flightTime = Mathf.Clamp(delta.magnitude / speed, 0.26f, 0.72f);

            // Real Magnus lateral curve! Starts bending outward then curls cleanly back into the corner
            float curveDir = Mathf.Abs(aimInput) >= 0.14f ? Mathf.Sign(aimInput) : (aimX >= 0f ? 1f : -1f);
            float curveStrength = isGoldenCurve ? 8.8f : (isPlacedLow ? 3.2f : 4.6f);
            float curveX = -curveDir * curveStrength; // Curves INWARD toward top corner!
            Vector3 curveAccel = new Vector3(curveX, 0f, 0f);

            // Compensate initial horizontal velocity so curved trajectory lands right on shotTarget!
            float initVx = (delta.x - 0.5f * curveX * flightTime * flightTime) / flightTime;
            float initVy = (delta.y + 0.5f * 11.8f * flightTime * flightTime) / flightTime;
            float initVz = delta.z / flightTime;

            Vector3 shotVel = new Vector3(initVx, Mathf.Clamp(initVy, 1.2f, 9.2f), initVz);
            _lastShotKmh = Mathf.Clamp(shotVel.magnitude * 4.4f, 48f, 108f);
            _playerShotsOnTarget++;

            float kickDelay = isVolley ? 0.08f : 0.15f;
            if (isGoldenCurve)
            {
                SetTrailColor(new Color(1.0f, 0.84f, 0.22f, 0.92f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.78f, true,
                    spawnBurst: true, burstColor: new Color(1.0f, 0.84f, 0.22f), burstCount: 12, burstSpread: 0.45f,
                    hitstop: true, shake: true, shakeAmt: 0.14f, shakeDur: 0.18f);
                SetCallout(isVolley ? $"TEK DOKUNUŞ VOLE! ({Mathf.RoundToInt(_lastShotKmh)} km/s)" : $"90'A FALSOLU FÜZE! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(1.0f, 0.86f, 0.24f), 1.35f);
            }
            else if (isPlacedLow)
            {
                SetTrailColor(new Color(0.28f, 0.94f, 0.78f, 0.75f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.58f, true,
                    spawnBurst: true, burstColor: new Color(0.28f, 0.92f, 0.78f), burstCount: 7, burstSpread: 0.30f);
                SetCallout($"YERDEN PLASE ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(0.32f, 0.92f, 0.78f), 1.0f);
            }
            else if (isOvercharged)
            {
                SetTrailColor(new Color(1.0f, 0.36f, 0.24f, 0.88f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.65f, true,
                    shake: true, shakeAmt: 0.12f, shakeDur: 0.16f);
                SetCallout($"SERT ŞUT! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(1.0f, 0.48f, 0.26f), 1.0f);
            }
            else
            {
                SetTrailColor(new Color(0.35f, 0.90f, 0.85f, 0.75f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.58f, true,
                    spawnBurst: true, burstColor: new Color(0.35f, 0.90f, 0.85f), burstCount: 8, burstSpread: 0.35f);
            }

            if (isVolley)
            {
                // Mid-air volley already has no carrier; launch after crisp 0.08s swing-through
                _carrierTeam = 0;
                _carrierIdx = 1;
            }

            HudRevision++;
        }

        private void TryPlayerFeint()
        {
            if (_feintCooldown > 0f || _stamina < 24f || !PlayerHasBall) return;
            _stamina = Mathf.Max(0f, _stamina - 24f);
            _feintCooldown = 1.45f;
            _dashBoostTimer = 0.44f;
            _stealImmunityTimer = 0.88f;
            _blueStunTimer = 0.65f; // Nearby rival defender buys the fake!

            float side = (InputHub.Instance != null && Mathf.Abs(InputHub.Instance.MoveInput.x) > 0.12f)
                ? Mathf.Sign(InputHub.Instance.MoveInput.x)
                : (Random.value < 0.5f ? -1f : 1f);
            _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerFeint, 0.42f, side);
            GameFeelAudio.PlaySlideWhoosh();
            GameFeelAudio.SpawnBurst(transform.TransformPoint(_playerLocalPos + Vector3.up * 0.25f), new Color(0.32f, 0.94f, 0.82f), count: 8, spread: 0.42f);
            SetCallout("BİLEK ÇALIMI!", new Color(0.32f, 0.95f, 0.84f), 1.0f);
        }

        private void TryPlayerSteal()
        {
            _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerSteal, 0.42f);
            _dashBoostTimer = 0.26f;

            if (_carrierTeam == 1 && _blue != null && _carrierIdx < _blue.Length && _blue[_carrierIdx] != null)
            {
                Vector3 carrierPos = _blue[_carrierIdx].transform.localPosition;
                Vector3 d = carrierPos - _playerLocalPos;
                d.y = 0f;
                if (d.magnitude <= 2.9f)
                {
                    AwardPlayerSteal("TOPU KAPTIN!");
                }
            }
        }

        private void TryPlayerSlideTackle()
        {
            if (_slideCooldown > 0f || _stamina < 22f)
            {
                TryPlayerSteal();
                return;
            }

            _stamina = Mathf.Max(0f, _stamina - 24f);
            _slideCooldown = 1.15f;
            _slideTimer = 0.46f;
            _slideConnected = false;

            Vector2 move = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            if (move.sqrMagnitude > 0.08f)
            {
                _slideDir = new Vector3(move.x, 0f, move.y).normalized;
            }
            else
            {
                Vector3 toBall = _ballPos - _playerLocalPos;
                toBall.y = 0f;
                _slideDir = toBall.sqrMagnitude > 0.04f ? toBall.normalized : (_playerLocalRot * Vector3.forward);
            }

            _playerLocalRot = Quaternion.LookRotation(_slideDir, Vector3.up);
            _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerSlideTackle, 0.54f, _slideDir.x >= 0f ? 1f : -1f);
            GameFeelAudio.PlaySlideWhoosh();
            GameFeelAudio.SpawnBurst(transform.TransformPoint(_playerLocalPos + Vector3.up * 0.12f), new Color(0.28f, 0.68f, 0.26f), count: 10, spread: 0.48f);
        }

        private void AwardPlayerSteal(string callout)
        {
            _carrierTeam = 0;
            _carrierIdx = 1;
            _stealImmunityTimer = 1.05f;
            _playerTacklesWon++;
            PlayKickSound(0.48f, alwaysHear: true);
            GameFeelAudio.SpawnBurst(transform.TransformPoint(_ballPos + Vector3.up * 0.25f), new Color(0.98f, 0.82f, 0.24f), count: 10, spread: 0.45f);
            _cameraRig?.AddShake(0.10f, 0.14f);
            SetCallout(callout, new Color(1.0f, 0.84f, 0.24f), 1.15f);
        }

        private void SetCallout(string text, Color color, float duration)
        {
            _statusBannerText = text;
            _statusBannerColor = color;
            _statusBannerTimer = duration;
            HudRevision++;
        }

        private void ResetKickoff(int team)
        {
            _ballPos = new Vector3(0f, BallRadius, 0f);
            _ballVel = Vector3.zero;
            _ballCurveAccel = Vector3.zero;
            _carrierTeam = team;
            _carrierIdx = 1;
            _actionTimer = 0.9f;
            _stealImmunityTimer = 1.0f;
            _isChargingShot = false;
            _shotCharge = 0f;
            _isChargingPass = false;
            _passCharge = 0f;
            _pendingKickActive = false;
            _pendingKickTimer = 0f;
            _slideTimer = 0f;
            _slideRecoveryTimer = 0f;
            _blueGkDiving = false;
            _redGkDiving = false;
            if (_ballTrail != null) _ballTrail.Clear();

            for (int i = 0; i < 3; i++)
            {
                if (_red != null && i < _red.Length && _red[i] != null)
                {
                    _red[i].transform.localPosition = _redHome[i];
                    _red[i].transform.localRotation = Quaternion.identity;
                }
                if (_blue != null && i < _blue.Length && _blue[i] != null)
                {
                    _blue[i].transform.localPosition = _blueHome[i];
                    _blue[i].transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }

            if (_player != null)
            {
                _playerLocalPos = new Vector3(0f, 0f, team == 0 ? -0.8f : -3.4f);
                _playerLocalRot = Quaternion.identity;
                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
            }
        }

        private void Update()
        {
            if (_red == null || _blue == null || _ball == null) return;
            if (_player != null && PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_stealImmunityTimer > 0f) _stealImmunityTimer -= dt;
            if (_dashBoostTimer > 0f) _dashBoostTimer -= dt;
            if (_feintCooldown > 0f) _feintCooldown -= dt;
            if (_slideCooldown > 0f) _slideCooldown -= dt;
            if (_slideRecoveryTimer > 0f) _slideRecoveryTimer -= dt;
            if (_wallPassRunTimer > 0f) _wallPassRunTimer -= dt;
            if (_blueStunTimer > 0f) _blueStunTimer -= dt;
            if (_blueGkDiveTimer > 0f) { _blueGkDiveTimer -= dt; if (_blueGkDiveTimer <= 0f) _blueGkDiving = false; }
            if (_redGkDiveTimer > 0f) { _redGkDiveTimer -= dt; if (_redGkDiveTimer <= 0f) _redGkDiving = false; }

            if (_statusBannerTimer > 0f)
            {
                _statusBannerTimer -= dt;
                if (_statusBannerTimer <= 0f) HudRevision++;
            }

            if (_goalResetTimer > 0f)
            {
                _goalResetTimer -= dt;
                if (_shotAimMarker != null && _shotAimMarker.gameObject.activeSelf) _shotAimMarker.gameObject.SetActive(false);
                IdleTeam(_red);
                IdleTeam(_blue);
                if (_player != null) _player.Humanoid?.SetLocomotion(0f, true, 0f);
                if (_goalResetTimer <= 0f)
                {
                    if (!_matchOver)
                    {
                        ResetKickoff(_player != null ? (_lastGoalByRed ? 1 : 0) : (_redScore + _blueScore) % 2);
                        if (_player != null) GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                    }
                    HudRevision++;
                }
                UpdateBallVisual(dt);
                return;
            }

            if (_matchOver)
            {
                if (_shotAimMarker != null && _shotAimMarker.gameObject.activeSelf) _shotAimMarker.gameObject.SetActive(false);
                IdleTeam(_red);
                IdleTeam(_blue);
                if (_player != null) _player.Humanoid?.SetLocomotion(0f, true, 0f);
                UpdateBallVisual(dt);
                return;
            }

            if (_player != null)
            {
                UpdatePlayerCharging(dt);
                UpdatePlayerOnPitch(dt);
                UpdateGoalAimReticle();
            }

            if (_carrierTeam >= 0)
            {
                _ballCurveAccel = Vector3.zero;
                if (PlayerHasBall)
                {
                    float speedRatio = Mathf.Clamp01(_playerMoveSpeed / 2.2f);
                    _dribbleStep += dt * (_playerMoveSpeed * 2.15f);
                    float dribbleDist = 0.54f + Mathf.Abs(Mathf.Sin(_dribbleStep)) * speedRatio * 0.18f;
                    _ballPos = _playerLocalPos + (_playerLocalRot * Vector3.forward) * dribbleDist;
                    _ballPos.y = BallRadius;

                    if (_pendingKickActive)
                    {
                        _pendingKickTimer -= dt;
                        if (_pendingKickTimer <= 0f)
                        {
                            ReleasePendingKick();
                        }
                    }
                    // Fair defender pressure check (disabled during feint/immunity/kick windup!)
                    else if (_stealImmunityTimer <= 0f && _blueStunTimer <= 0f)
                    {
                        float tackleRadius = _leagueTier >= 2 ? 1.02f : 0.90f;
                        for (int b = 1; b < _blue.Length; b++)
                        {
                            Vector3 d = _blue[b].transform.localPosition - _playerLocalPos;
                            d.y = 0f;
                            if (d.magnitude < tackleRadius)
                            {
                                _isChargingShot = false;
                                _isChargingPass = false;
                                _carrierTeam = 1;
                                _carrierIdx = b;
                                _actionTimer = 1.20f;
                                _blue[b].PlaySportAnim(SportAnimKind.SoccerSteal, 0.40f);
                                PlayKickSound(0.35f, alwaysHear: true);
                                HudRevision++;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ProceduralHumanoid carrier = (_carrierTeam == 0 ? _red : _blue)[_carrierIdx];
                    float attackDir = _carrierTeam == 0 ? 1f : -1f;
                    Vector3 goalTarget = new Vector3(Mathf.Sin(Time.time * 1.15f) * 1.5f, 0f, attackDir * (HalfL - 0.5f));
                    Vector3 toGoal = goalTarget - carrier.transform.localPosition;
                    toGoal.y = 0f;
                    Vector3 runDir = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : new Vector3(0f, 0f, attackDir);
                    runDir = (runDir + new Vector3(Mathf.Sin(Time.time * 2.2f + _carrierIdx) * 0.45f, 0f, 0f)).normalized;

                    float npcSpeed = _carrierTeam == 1 && _player != null
                        ? (3.0f + _leagueTier * 0.38f)
                        : 3.55f;
                    MoveNpc(carrier, carrier.transform.localPosition + runDir * 2.8f, _pendingKickActive ? npcSpeed * 0.25f : npcSpeed, dt);
                    _dribbleStep += dt * (_pendingKickActive ? 0f : 7f);
                    _ballPos = carrier.transform.localPosition + carrier.transform.localRotation * Vector3.forward * (0.52f + Mathf.Abs(Mathf.Sin(_dribbleStep)) * (_pendingKickActive ? 0f : 0.22f));
                    _ballPos.y = BallRadius;

                    if (_pendingKickActive)
                    {
                        _pendingKickTimer -= dt;
                        if (_pendingKickTimer <= 0f)
                        {
                            ReleasePendingKick();
                        }
                    }
                    else
                    {
                        _actionTimer -= dt;
                        float distToGoalZ = Mathf.Abs(attackDir * HalfL - carrier.transform.localPosition.z);

                        if (_actionTimer <= 0f || distToGoalZ < 5.6f)
                        {
                            bool shoot = distToGoalZ < 7.8f || Random.value < 0.44f;
                            carrier.PlaySportAnim(SportAnimKind.SoccerKick, 0.48f);

                            Vector3 kickVel;
                            Vector3 kickCurve;
                            if (shoot)
                            {
                                float aimX = Random.Range(-GoalHalfW + 0.32f, GoalHalfW - 0.32f);
                                Vector3 shotTarget = new Vector3(aimX, Random.Range(0.75f, 1.55f), attackDir * (HalfL + 0.6f));
                                Vector3 delta = shotTarget - _ballPos;
                                float shotSpd = 12.6f + _leagueTier * 1.1f;
                                float flightTime = Mathf.Clamp(delta.magnitude / shotSpd, 0.36f, 0.82f);
                                kickVel = new Vector3(delta.x / flightTime, Random.Range(2.4f, 4.2f), delta.z / flightTime);
                                kickCurve = new Vector3(Random.Range(-2.5f, 2.5f), 0f, 0f);
                                _lastShotKmh = kickVel.magnitude * 4.2f;
                            }
                            else
                            {
                                Vector3 targetMatePos;
                                if (_carrierTeam == 0 && _player != null)
                                {
                                    targetMatePos = _playerLocalPos + new Vector3(0f, 0f, 1.6f);
                                }
                                else
                                {
                                    int mateIdx = _carrierIdx == 1 ? 2 : 1;
                                    ProceduralHumanoid mate = (_carrierTeam == 0 ? _red : _blue)[mateIdx];
                                    targetMatePos = mate.transform.localPosition + new Vector3(0f, 0f, attackDir * 2.2f);
                                }
                                targetMatePos.x = Mathf.Clamp(targetMatePos.x, -HalfW + 1.2f, HalfW - 1.2f);
                                targetMatePos.z = Mathf.Clamp(targetMatePos.z, -HalfL + 1.8f, HalfL - 1.8f);
                                Vector3 delta = targetMatePos - _ballPos;
                                float passTime = Mathf.Clamp(delta.magnitude / 9.8f, 0.32f, 0.80f);
                                kickVel = new Vector3(delta.x / passTime, 1.65f, delta.z / passTime);
                                kickCurve = Vector3.zero;
                            }

                            QueueSyncedKick(kickVel, kickCurve, 0.15f, 0.24f, shoot ? 0.55f : 0.36f, alwaysHear: false);
                        }
                    }
                }
            }
            else
            {
                // Ball in flight / rolling with real gravity + Magnus curve!
                _ballVel += _ballCurveAccel * dt;
                _ballCurveAccel *= Mathf.Exp(-1.6f * dt);
                _ballVel.y -= 11.8f * dt;
                _ballPos += _ballVel * dt;

                if (_ballPos.y < BallRadius)
                {
                    _ballPos.y = BallRadius;
                    _ballCurveAccel *= 0.25f;
                    if (Mathf.Abs(_ballVel.y) > 1.4f)
                    {
                        _ballVel.y = -_ballVel.y * 0.50f;
                        PlayKickSound(0.22f);
                    }
                    else
                    {
                        _ballVel.y = 0f;
                    }
                    _ballVel.x *= Mathf.Exp(-1.75f * dt);
                    _ballVel.z *= Mathf.Exp(-1.75f * dt);
                }

                // Trigger proactive Goalkeeper Diving Saves on fast incoming shots!
                CheckProactiveGoalkeeperDive();

                // Goal line check (Woodwork clang, Over-the-bar out, Goalkeeper Diving Save / Parry, or GOAL!)
                if (Mathf.Abs(_ballPos.z) >= HalfL - 0.52f && Mathf.Abs(_ballPos.z) <= HalfL + 0.65f)
                {
                    bool towardBlue = _ballPos.z > 0f && _ballVel.z > 0.8f;
                    bool towardRed = _ballPos.z < 0f && _ballVel.z < -0.8f;
                    if (towardBlue || towardRed)
                    {
                        if (TryResolveGoalLineEvents(towardBlue))
                        {
                            UpdateBallVisual(dt);
                            return;
                        }
                    }
                }

                // Bounce off sideline/endline boards
                if (Mathf.Abs(_ballPos.x) > HalfW - BallRadius)
                {
                    _ballPos.x = Mathf.Sign(_ballPos.x) * (HalfW - BallRadius);
                    _ballVel.x = -_ballVel.x * 0.65f;
                }
                if (Mathf.Abs(_ballPos.z) > HalfL - BallRadius)
                {
                    _ballPos.z = Mathf.Sign(_ballPos.z) * (HalfL - BallRadius);
                    _ballVel.z = -_ballVel.z * 0.62f;
                }

                _actionTimer -= dt;
                if (_actionTimer <= 0f && _ballPos.y < 1.15f)
                {
                    TryClaimBall();
                }
            }

            UpdateAllPlayers(dt);
            UpdateBallVisual(dt);
            CheckHudStateChanged();
        }

        private void CheckProactiveGoalkeeperDive()
        {
            // Blue GK (at +HalfL - 0.85) dives when a fast shot approaches!
            if (!_blueGkDiving && _ballVel.z > 7.5f && _ballPos.z > 2.0f && _ballPos.z < HalfL - 1.2f)
            {
                float tToGoal = (HalfL - 0.85f - _ballPos.z) / Mathf.Max(1f, _ballVel.z);
                if (tToGoal > 0.05f && tToGoal < 0.58f)
                {
                    float predX = _ballPos.x + _ballVel.x * tToGoal + 0.5f * _ballCurveAccel.x * tToGoal * tToGoal;
                    if (Mathf.Abs(predX) <= GoalHalfW + 0.6f && _blue[0] != null)
                    {
                        float gkX = _blue[0].transform.localPosition.x;
                        float deltaX = predX - gkX;
                        if (Mathf.Abs(deltaX) > 0.55f)
                        {
                            _blueGkDiving = true;
                            _blueGkDiveTimer = 0.64f;
                            // League-scaled goalkeeper read accuracy
                            float maxDiveReach = 1.15f + _leagueTier * 0.34f;
                            _blueGkDiveTargetX = Mathf.Clamp(gkX + Mathf.Clamp(deltaX, -maxDiveReach, maxDiveReach), -GoalHalfW + 0.25f, GoalHalfW - 0.25f);
                            _blue[0].PlaySportAnim(SportAnimKind.GoalkeeperDive, 0.62f, deltaX >= 0f ? -1f : 1f);
                        }
                    }
                }
            }

            // Red GK (at -HalfL + 0.85) dives when Blue shoots!
            if (!_redGkDiving && _ballVel.z < -7.5f && _ballPos.z < -2.0f && _ballPos.z > -HalfL + 1.2f)
            {
                float tToGoal = (-HalfL + 0.85f - _ballPos.z) / Mathf.Min(-1f, _ballVel.z);
                if (tToGoal > 0.05f && tToGoal < 0.58f)
                {
                    float predX = _ballPos.x + _ballVel.x * tToGoal;
                    if (Mathf.Abs(predX) <= GoalHalfW + 0.6f && _red[0] != null)
                    {
                        float gkX = _red[0].transform.localPosition.x;
                        float deltaX = predX - gkX;
                        if (Mathf.Abs(deltaX) > 0.55f)
                        {
                            _redGkDiving = true;
                            _redGkDiveTimer = 0.64f;
                            _redGkDiveTargetX = Mathf.Clamp(gkX + Mathf.Clamp(deltaX, -1.55f, 1.55f), -GoalHalfW + 0.25f, GoalHalfW - 0.25f);
                            _red[0].PlaySportAnim(SportAnimKind.GoalkeeperDive, 0.62f, deltaX >= 0f ? 1f : -1f);
                        }
                    }
                }
            }
        }

        private bool TryResolveGoalLineEvents(bool towardBlue)
        {
            float absX = Mathf.Abs(_ballPos.x);
            float goalSign = towardBlue ? 1f : -1f;

            // 1. Goalkeeper diving save / parry rebound right in front of the goal line!
            if (Mathf.Abs(_ballPos.z) < HalfL + 0.06f && absX <= GoalHalfW)
            {
                ProceduralHumanoid gk = towardBlue ? _blue[0] : _red[0];
                if (gk != null && _ballPos.y <= 1.98f)
                {
                    float gkX = gk.transform.localPosition.x;
                    float gkReach = towardBlue ? (0.72f + _leagueTier * 0.14f) : 0.92f;
                    if (Mathf.Abs(_ballPos.x - gkX) <= gkReach)
                    {
                        float shotSpeed = _ballVel.magnitude;
                        PlayKickSound(0.55f, alwaysHear: _player != null);
                        if (shotSpeed >= 15.8f)
                        {
                            // Dramatic parry rebound back into the box for a follow-up tap-in!
                            float deflectX = Mathf.Sign(_ballPos.x - gkX + 0.01f) * Random.Range(2.4f, 4.2f);
                            _ballPos.z = goalSign * (HalfL - 0.85f);
                            _ballVel = new Vector3(deflectX, Random.Range(2.6f, 3.8f), -goalSign * Random.Range(4.8f, 7.2f));
                            _ballCurveAccel = Vector3.zero;
                            _actionTimer = 0.16f;
                            GameFeelAudio.SpawnBurst(transform.TransformPoint(_ballPos), new Color(1f, 0.92f, 0.52f), count: 9, spread: 0.4f);
                            if (_player != null)
                            {
                                _cameraRig?.AddShake(0.11f, 0.15f);
                                SetCallout(towardBlue ? "KALECİ ÇELDİ · RİBAUND!" : "KALECİN KURTARDI!", new Color(0.98f, 0.78f, 0.28f), 1.25f);
                            }
                        }
                        else
                        {
                            // Clean goalkeeper catch & punt clearance
                            gk.PlaySportAnim(SportAnimKind.SoccerKick, 0.45f);
                            _ballPos.z = goalSign * (HalfL - 1.0f);
                            _ballVel = new Vector3(Random.Range(-3.0f, 3.0f), 3.6f, -goalSign * 9.2f);
                            _ballCurveAccel = Vector3.zero;
                            _actionTimer = 0.35f;
                            if (_player != null)
                            {
                                SetCallout(towardBlue ? "KALECİ TUTTU!" : "HARİKA KURTARIŞ!", new Color(0.42f, 0.82f, 1.0f), 1.0f);
                            }
                        }
                        return true;
                    }
                }
            }

            if (Mathf.Abs(_ballPos.z) < HalfL + 0.08f) return false;

            // 2. Post or Crossbar Clang!
            bool hitPost = absX >= GoalHalfW - 0.16f && absX <= GoalHalfW + 0.22f && _ballPos.y <= CrossbarY + 0.22f;
            bool hitCrossbar = absX < GoalHalfW && _ballPos.y >= CrossbarY - 0.16f && _ballPos.y <= CrossbarY + 0.26f;
            if (hitPost || hitCrossbar)
            {
                _ballPos.z = goalSign * (HalfL - 0.28f);
                _ballVel = new Vector3(-_ballVel.x * 0.45f + Random.Range(-2.2f, 2.2f), Random.Range(2.4f, 4.2f), -goalSign * Mathf.Clamp(_ballVel.magnitude * 0.42f, 4.5f, 8.2f));
                _ballCurveAccel = Vector3.zero;
                _actionTimer = 0.16f;
                if (_player != null || IsPlayerNear(26f))
                {
                    GameFeelAudio.PlayGoalpostClang();
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(_ballPos), new Color(1f, 0.94f, 0.70f), count: 10, spread: 0.45f);
                }
                if (_player != null)
                {
                    _cameraRig?.AddShake(0.18f, 0.22f);
                    SetCallout("DİREKTEN DÖNDÜ!", new Color(1.0f, 0.62f, 0.22f), 1.35f);
                }
                return true;
            }

            // 3. Sailed over the crossbar (Overcharged Out!)
            if (absX <= GoalHalfW && _ballPos.y > CrossbarY + 0.26f)
            {
                _ballPos.z = goalSign * (HalfL - 1.2f);
                _ballPos.y = BallRadius;
                _ballVel = new Vector3(Random.Range(-2.5f, 2.5f), 3.4f, -goalSign * 8.5f);
                _ballCurveAccel = Vector3.zero;
                _actionTimer = 0.35f;
                if (_player != null)
                {
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                    SetCallout("ÜSTTEN AUT! (Gücü altın bölgede bırak)", new Color(0.96f, 0.46f, 0.36f), 1.4f);
                }
                return true;
            }

            // 4. Clean GOAL inside the mouth!
            if (absX <= GoalHalfW)
            {
                HandleGoalScored(towardBlue);
                return true;
            }

            return false;
        }

        private void UpdatePlayerCharging(float dt)
        {
            if (!PlayerHasBall)
            {
                _isChargingShot = false;
                _isChargingPass = false;
                _shotCharge = 0f;
                _passCharge = 0f;
                return;
            }

            if (_isChargingShot)
            {
                _shotCharge += dt / 0.82f;
                if (_shotCharge >= 1.0f)
                {
                    _shotCharge = 1.0f;
                    TriggerPrimaryRelease(0.85f);
                }
            }
            if (_isChargingPass)
            {
                _passCharge = Mathf.Min(1f, _passCharge + dt / 0.55f);
            }
        }

        private void UpdateGoalAimReticle()
        {
            if (_shotAimMarker == null) return;
            bool show = PlayerHasBall && (_isChargingShot || _playerLocalPos.z > -1.5f);
            if (_shotAimMarker.gameObject.activeSelf != show)
            {
                _shotAimMarker.gameObject.SetActive(show);
            }
            if (!show) return;

            float aimInput = InputHub.Instance != null ? InputHub.Instance.MoveInput.x : 0f;
            float gkX = (_blue != null && _blue.Length > 0 && _blue[0] != null) ? _blue[0].transform.localPosition.x : 0f;
            float aimX = Mathf.Abs(aimInput) >= 0.14f
                ? Mathf.Clamp(aimInput, -1f, 1f) * (GoalHalfW - 0.16f)
                : (gkX >= 0f ? -GoalHalfW * 0.74f : GoalHalfW * 0.74f);
            float aimY = _isChargingShot ? Mathf.Lerp(0.55f, 1.72f, _shotCharge) : 0.72f;
            _shotAimMarker.localPosition = new Vector3(aimX, aimY, HalfL - 0.16f);
            float pulse = _isChargingShot ? Mathf.Lerp(1.12f, 0.76f, _shotCharge) : (0.92f + Mathf.Sin(Time.time * 6f) * 0.08f);
            _shotAimMarker.localScale = Vector3.one * pulse;
        }

        private void HandleGoalScored(bool redScored)
        {
            _lastGoalByRed = redScored;
            if (redScored) _redScore++; else _blueScore++;
            UpdateScoreboard();
            _goalResetTimer = 2.15f;
            _ballVel *= 0.18f;
            _ballCurveAccel = Vector3.zero;
            _isChargingShot = false;
            _isChargingPass = false;
            _pendingKickActive = false;
            _pendingKickTimer = 0f;

            ProceduralHumanoid[] winners = redScored ? _red : _blue;
            for (int i = 0; i < winners.Length; i++)
            {
                if (winners[i] != null && winners[i].gameObject.activeSelf)
                {
                    winners[i].PlaySportAnim(SportAnimKind.VictoryJump, 0.72f);
                }
            }

            if (_player != null)
            {
                GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                if (redScored)
                {
                    _player.Humanoid?.PlaySportAnim(SportAnimKind.VictoryJump, 0.82f);
                    GameFeelAudio.PlayCrowdCheer(bigCheer: true);
                    _cameraRig?.AddShake(0.20f, 0.26f);
                    if (SaveManager.Instance != null)
                    {
                        SaveManager.Instance.Data.footballGoalsScored++;
                        SaveManager.Instance.AddMoney(35 + _leagueTier * 10);
                    }
                    GameFeelAudio.PlayCoinChime();
                    SetCallout($"GOOOL! · {Mathf.RoundToInt(_lastShotKmh)} km/s (+{35 + _leagueTier * 10} ₺)", new Color(1.0f, 0.85f, 0.22f), 2.1f);
                }
                else
                {
                    SetCallout("Mavi Takım Gol Attı!", new Color(0.95f, 0.36f, 0.32f), 2.0f);
                }

                if (_redScore >= GoalsToWin || _blueScore >= GoalsToWin)
                {
                    _matchOver = true;
                    _playerWonMatch = _redScore >= GoalsToWin;
                    _goalResetTimer = 1.25f;
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: true);
                    if (_playerWonMatch)
                    {
                        int reward = 90 + _leagueTier * 45;
                        GameFeelAudio.PlayMilestoneFanfare();
                        SaveManager.Instance?.AddMoney(reward);
                        if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                        {
                            SaveManager.Instance.Data.footballWins++;
                            if (SaveManager.Instance.Data.footballLeagueTier < 2)
                            {
                                SaveManager.Instance.Data.footballLeagueTier++;
                            }
                            SaveManager.Instance.Save();
                        }
                        NeighborhoodReputation.AddReputation(35 + _leagueTier * 10, $"Halı Saha [{GetLeagueLabel()}] Zaferi! (+{reward} ₺)");
                    }
                    SportMatchEvents.NotifyMatchFinished("sport_football", _playerWonMatch);
                }
                HudRevision++;
            }

            if (_player != null || IsPlayerNear(26f))
            {
                GameFeelAudio.SpawnBurst(
                    transform.TransformPoint(_ballPos + Vector3.up * 0.8f),
                    redScored ? new Color(0.95f, 0.25f, 0.20f) : new Color(0.22f, 0.56f, 0.98f),
                    count: 18, spread: 0.80f);
            }
        }

        private void UpdatePlayerOnPitch(float dt)
        {
            Vector2 move = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            bool sprintHeld = InputHub.Instance != null && InputHub.Instance.SprintHeld;

            // Tapping Sprint while carrying the ball triggers Bilek Çalımı (Step-Over Feint)!
            if (sprintHeld && !_wasSprintHeld && PlayerHasBall && !_isChargingShot)
            {
                TryPlayerFeint();
            }
            _wasSprintHeld = sprintHeld;

            // Active Slide Tackle motion & generous sliding corridor interception!
            if (_slideTimer > 0f)
            {
                _slideTimer -= dt;
                float slideSpd = Mathf.Lerp(2.4f, 8.2f, Mathf.Clamp01(_slideTimer / 0.46f));
                _playerLocalPos += _slideDir * (slideSpd * dt);
                _playerLocalPos.x = Mathf.Clamp(_playerLocalPos.x, -HalfW + 0.65f, HalfW - 0.65f);
                _playerLocalPos.z = Mathf.Clamp(_playerLocalPos.z, -HalfL + 0.95f, HalfL - 0.95f);

                if (!_slideConnected && _carrierTeam == 1 && _blue != null && _carrierIdx < _blue.Length && _blue[_carrierIdx] != null)
                {
                    Vector3 d = _blue[_carrierIdx].transform.localPosition - _playerLocalPos;
                    d.y = 0f;
                    if (d.magnitude <= 2.35f)
                    {
                        _slideConnected = true;
                        AwardPlayerSteal("TEMİZ KAYARAK MÜDAHALE!");
                    }
                }
                else if (!_slideConnected && _carrierTeam == -1)
                {
                    Vector3 d = _ballPos - _playerLocalPos;
                    d.y = 0f;
                    if (d.magnitude <= 1.9f && _ballPos.y < 1.1f)
                    {
                        _slideConnected = true;
                        AwardPlayerSteal("KAYARAK TOPU ALDIN!");
                    }
                }

                if (_slideTimer <= 0f && !_slideConnected)
                {
                    _slideRecoveryTimer = 0.32f; // Brief getup recovery on missed slide
                }

                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation * _playerLocalRot);
                _player.Humanoid?.SetLocomotion(0f, true, 0f, transform.TransformDirection(_slideDir), sampleGround: false);
                _player.Humanoid?.SetLookAtPoint(true, _ball.position);
                return;
            }

            bool canSprint = sprintHeld && _stamina > 4f && !_isChargingShot && _slideRecoveryTimer <= 0f;
            if (canSprint && move.sqrMagnitude > 0.08f)
            {
                _stamina = Mathf.Max(0f, _stamina - 22f * dt);
            }
            else
            {
                _stamina = Mathf.Min(100f, _stamina + (move.sqrMagnitude > 0.08f ? 18f : 32f) * dt);
            }

            float maxSpd = canSprint ? 6.15f : 4.55f;
            if (_dashBoostTimer > 0f) maxSpd += 2.35f;
            if (_isChargingShot || _isChargingPass) maxSpd *= 0.72f;
            if (_slideRecoveryTimer > 0f) maxSpd *= 0.42f;

            Vector3 localDir = new Vector3(move.x, 0f, move.y);
            float mag = Mathf.Clamp01(localDir.magnitude);
            float speed = mag > 0.08f ? mag * maxSpd : 0f;
            _playerMoveSpeed = speed;
            Vector3 worldDir = _player.transform.forward;

            if (speed > 0.05f)
            {
                localDir.Normalize();
                _playerLocalPos += localDir * (speed * dt);
                _playerLocalPos.x = Mathf.Clamp(_playerLocalPos.x, -HalfW + 0.65f, HalfW - 0.65f);
                _playerLocalPos.z = Mathf.Clamp(_playerLocalPos.z, -HalfL + 0.95f, HalfL - 0.95f);
                Quaternion targetRot = Quaternion.LookRotation(localDir, Vector3.up);
                _playerLocalRot = Quaternion.Slerp(_playerLocalRot, targetRot, 1f - Mathf.Exp(-12f * dt));
                worldDir = transform.TransformDirection(localDir);
            }
            else
            {
                Vector3 toBall = _ballPos - _playerLocalPos;
                toBall.y = 0f;
                if (toBall.sqrMagnitude > 0.05f)
                {
                    Quaternion lookBall = Quaternion.LookRotation(toBall.normalized, Vector3.up);
                    _playerLocalRot = Quaternion.Slerp(_playerLocalRot, lookBall, 1f - Mathf.Exp(-8f * dt));
                }
            }

            _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation * _playerLocalRot);
            _player.Humanoid?.SetLocomotion(speed, true, 0f, worldDir, sampleGround: false);
            _player.Humanoid?.SetLookAtPoint(true, _ball.position);
        }

        private void CheckHudStateChanged()
        {
            if (_player == null) return;
            int hash = (_carrierTeam + 2) * 10 + _carrierIdx + (_matchOver ? 100 : 0) + (_isChargingShot ? 200 : 0) + (_isChargingPass ? 400 : 0);
            if (hash != _lastHudStateHash)
            {
                _lastHudStateHash = hash;
                HudRevision++;
            }
        }

        private void TryClaimBall()
        {
            if (_player != null && _slideRecoveryTimer <= 0f)
            {
                Vector3 dp = _playerLocalPos - _ballPos;
                dp.y = 0f;
                if (dp.magnitude < 1.65f)
                {
                    _carrierTeam = 0;
                    _carrierIdx = 1;
                    _stealImmunityTimer = 0.85f;
                    HudRevision++;
                    return;
                }
            }

            float bestDist = 1.15f;
            int bestTeam = -1;
            int bestIdx = -1;

            for (int t = 0; t < 2; t++)
            {
                ProceduralHumanoid[] team = t == 0 ? _red : _blue;
                for (int i = 0; i < team.Length; i++)
                {
                    if (team[i] == null || !team[i].gameObject.activeSelf) continue;
                    Vector3 d = team[i].transform.localPosition - _ballPos;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTeam = t;
                        bestIdx = i;
                    }
                }
            }

            if (bestTeam >= 0)
            {
                if (bestIdx == 0)
                {
                    ProceduralHumanoid gk = (bestTeam == 0 ? _red : _blue)[0];
                    gk.PlaySportAnim(SportAnimKind.SoccerKick, 0.48f);
                    PlayKickSound(0.45f);
                    float dir = bestTeam == 0 ? 1f : -1f;
                    _ballVel = new Vector3(Random.Range(-3.2f, 3.2f), 3.6f, dir * 8.8f);
                    _ballCurveAccel = Vector3.zero;
                    _actionTimer = 0.42f;
                }
                else
                {
                    _carrierTeam = bestTeam;
                    _carrierIdx = bestIdx;
                    _actionTimer = Random.Range(0.95f, 1.75f);
                }
            }
        }

        private void UpdateAllPlayers(float dt)
        {
            UpdateGoalkeeper(_red[0], -HalfL + 0.85f, dt, _player != null ? 3.7f : 3.2f, _redGkDiving, _redGkDiveTargetX);
            UpdateGoalkeeper(_blue[0], HalfL - 0.85f, dt, _player != null ? (2.55f + _leagueTier * 0.42f) : 3.2f, _blueGkDiving, _blueGkDiveTargetX);

            for (int t = 0; t < 2; t++)
            {
                ProceduralHumanoid[] team = t == 0 ? _red : _blue;
                Vector3[] homes = t == 0 ? _redHome : _blueHome;
                float attackDir = t == 0 ? 1f : -1f;

                for (int i = 1; i < team.Length; i++)
                {
                    if (team[i] == null || !team[i].gameObject.activeSelf) continue;
                    if (_carrierTeam == t && _carrierIdx == i) continue;

                    Vector3 target;
                    if (_carrierTeam == -1)
                    {
                        int other = i == 1 ? 2 : 1;
                        float myDist = (team[i].transform.localPosition - _ballPos).sqrMagnitude;
                        Vector3 otherPos = (t == 0 && other == 1 && _player != null)
                            ? _playerLocalPos
                            : team[other].transform.localPosition;
                        float otherDist = (otherPos - _ballPos).sqrMagnitude;
                        target = myDist <= otherDist
                            ? new Vector3(_ballPos.x, 0f, _ballPos.z)
                            : new Vector3(homes[i].x, 0f, Mathf.Clamp(_ballPos.z + attackDir * 2.4f, -HalfL + 2f, HalfL - 2f));
                    }
                    else if (_carrierTeam == t)
                    {
                        float flank = i == 1 ? -3.6f : (_player != null && _playerLocalPos.x > 0f ? -3.6f : 3.6f);
                        float pushAhead = (t == 0 && _wallPassRunTimer > 0f) ? 5.8f : 4.0f;
                        target = new Vector3(flank, 0f, Mathf.Clamp(_ballPos.z + attackDir * pushAhead, -HalfL + 2.2f, HalfL - 1.8f));
                    }
                    else
                    {
                        int other = i == 1 ? 2 : 1;
                        float myDist = (team[i].transform.localPosition - _ballPos).sqrMagnitude;
                        Vector3 otherPos = (t == 0 && other == 1 && _player != null)
                            ? _playerLocalPos
                            : team[other].transform.localPosition;
                        float otherDist = (otherPos - _ballPos).sqrMagnitude;
                        target = myDist <= otherDist
                            ? new Vector3(_ballPos.x, 0f, _ballPos.z - attackDir * 0.9f)
                            : new Vector3(homes[i].x * 0.8f, 0f, Mathf.Clamp(_ballPos.z - attackDir * 3.5f, -HalfL + 2.5f, HalfL - 2.5f));
                    }

                    target.x = Mathf.Clamp(target.x, -HalfW + 0.9f, HalfW - 0.9f);
                    target.z = Mathf.Clamp(target.z, -HalfL + 1.4f, HalfL - 1.4f);
                    float maxNpcSpd = (_player != null && t == 1) ? (3.05f + _leagueTier * 0.40f) : 3.95f;
                    if (t == 1 && _blueStunTimer > 0f) maxNpcSpd *= 0.30f; // Bought the player's step-over feint!
                    MoveNpc(team[i], target, maxNpcSpd, dt);
                }
            }
        }

        private void UpdateGoalkeeper(ProceduralHumanoid gk, float lineZ, float dt, float maxSpeed, bool diving, float diveTargetX)
        {
            if (gk == null) return;
            if (diving)
            {
                Vector3 p = gk.transform.localPosition;
                p.x = Mathf.MoveTowards(p.x, diveTargetX, (maxSpeed + 3.2f) * dt);
                p.z = lineZ;
                gk.transform.localPosition = p;
                gk.SetLocomotion(0f, true, 0f, gk.transform.forward, sampleGround: false);
                if (_ball != null) gk.SetLookAtPoint(true, _ball.position);
                return;
            }
            Vector3 target = new Vector3(Mathf.Clamp(_ballPos.x * 0.62f, -GoalHalfW + 0.42f, GoalHalfW - 0.42f), 0f, lineZ);
            MoveNpc(gk, target, maxSpeed, dt, facePoint: _ballPos);
        }

        private void MoveNpc(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, Vector3? facePoint = null)
        {
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float speed = 0f;
            Vector3 worldDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                speed = Mathf.Min(maxSpeed, dist * 4.5f);
                Vector3 dir = delta / dist;
                pos += dir * (speed * dt);
                hum.transform.localPosition = pos;
                worldDir = transform.TransformDirection(dir);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                hum.transform.localRotation = Quaternion.Slerp(hum.transform.localRotation, rot, 1f - Mathf.Exp(-10f * dt));
            }
            else if (facePoint.HasValue)
            {
                Vector3 look = facePoint.Value - pos;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    hum.transform.localRotation = Quaternion.Slerp(
                        hum.transform.localRotation,
                        Quaternion.LookRotation(look.normalized, Vector3.up),
                        1f - Mathf.Exp(-8f * dt));
                }
            }

            hum.SetLocomotion(speed, true, 0f, worldDir, sampleGround: false);
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

        private void UpdateBallVisual(float dt)
        {
            _ball.localPosition = _ballPos;
            _ball.Rotate(new Vector3(_ballVel.z, _ballCurveAccel.x * 0.35f, -_ballVel.x) * (dt * 120f), Space.Self);
            if (_ballTrail != null)
            {
                _ballTrail.emitting = _player != null && _carrierTeam == -1 && _ballVel.sqrMagnitude > 64f;
            }
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(_ballPos.x, 0.024f, _ballPos.z);
                float s = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(_ballPos.y / 3.5f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = $"HAMİDİYE HALI SAHA\nKIRMIZI {_redScore} - {_blueScore} MAVİ";
            }
        }

        private void PlayKickSound(float vol, bool alwaysHear = false)
        {
            if (_audio == null || _kickClip == null) return;
            if (!alwaysHear && _player == null && !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.92f, 1.08f);
            _audio.PlayOneShot(_kickClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }

    /// <summary>
    /// 2v2 Streetball Basketball arena: runs live NPC streetball when unattended, and lets the player
    /// join Gold Team anytime to dribble with synced right-hand bounce IK, chest-pass, steal,
    /// rise for airborne 2pt & 3pt jump shots toward the 3.05 m rim, and win matches.
    /// </summary>
    public sealed class LiveBasketballMatch : MonoBehaviour, IPlayableSportArena
    {
        private const float HalfW = 5.6f;
        private const float HalfL = 9.2f;
        private const float RimZ = 8.95f;
        private const float RimY = 3.05f;
        private const float BallRadius = 0.135f;
        private const int PointsToWin = 10;

        private ProceduralHumanoid[] _gold;
        private ProceduralHumanoid[] _navy;
        private Transform _ball;
        private Transform _ballShadow;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _bounceClip;

        private int _offenseTeam; // 0 = gold (attacks +Z), 1 = navy (attacks -Z)
        private int _handlerIdx;
        private int _phase; // 0 = dribble/attack, 1 = pass flight, 2 = shot flight, 3 = celebrate/inbound
        private float _phaseTimer = 2.0f;
        private float _flightDuration = 0.8f;
        private float _flightElapsed;
        private float _releaseDelayTimer;
        private float _releaseDelayDuration;
        private Vector3 _releaseGatherStart;
        private Vector3 _flightStart;
        private Vector3 _flightEnd;
        private float _flightArcHeight = 2.0f;
        private bool _shotWillScore;
        private int _pendingShotPoints = 2;
        private float _dribbleCycle;
        private int _lastBounceTick;
        private Vector3 _ballPos;
        private int _goldScore;
        private int _navyScore;

        // Player match state
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;
        private Vector3 _playerLocalPos;
        private Quaternion _playerLocalRot = Quaternion.identity;
        private bool _matchOver;
        private bool _playerWonMatch;
        private float _statusBannerTimer;
        private string _statusBannerKey;
        private int _lastHudHash;

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool HasSecondaryAction => _player != null && !_matchOver && PlayerHasBall;
        public bool AcceptsPrimaryAction => _player != null;

        private bool PlayerHasBall => _player != null && _offenseTeam == 0 && _handlerIdx == 0 && _phase == 0;
        private bool PlayerInThreePointZone
        {
            get
            {
                Vector3 hoop = new Vector3(0f, 0f, RimZ);
                Vector3 p = new Vector3(_playerLocalPos.x, 0f, _playerLocalPos.z);
                return Vector3.Distance(p, hoop) > 4.75f;
            }
        }

        public void Configure(
            ProceduralHumanoid[] goldTeam,
            ProceduralHumanoid[] navyTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip bounceClip)
        {
            _gold = goldTeam;
            _navy = navyTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _bounceClip = bounceClip;

            ResetPositions(offenseTeam: 0);
            UpdateScoreboard();
        }

        private void ResetPositions(int offenseTeam)
        {
            if (_gold != null && _gold.Length >= 2)
            {
                _gold[0].transform.localPosition = new Vector3(-2.0f, 0f, -2.0f);
                _gold[1].transform.localPosition = new Vector3(2.8f, 0f, 2.0f);
            }
            if (_navy != null && _navy.Length >= 2)
            {
                _navy[0].transform.localPosition = new Vector3(-1.4f, 0f, 2.8f);
                _navy[1].transform.localPosition = new Vector3(2.2f, 0f, 4.6f);
            }

            _offenseTeam = offenseTeam;
            _handlerIdx = 0;
            _phase = 0;
            _phaseTimer = 2.1f;
            _releaseDelayTimer = 0f;
            _releaseDelayDuration = 0f;

            if (_player != null)
            {
                _playerLocalPos = new Vector3(0f, 0f, -1.2f);
                _playerLocalRot = Quaternion.identity;
                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
            }
        }

        public void BeginPlayerMatch(PlayerFootController player)
        {
            if (player == null) return;
            _player = player;
            _beginPos = player.transform.position;
            _beginRot = player.transform.rotation;
            _goldScore = 0;
            _navyScore = 0;
            _matchOver = false;
            _playerWonMatch = false;
            _statusBannerTimer = 0f;
            _statusBannerKey = null;

            if (_gold != null && _gold.Length > 0 && _gold[0] != null)
            {
                _gold[0].gameObject.SetActive(false);
            }

            player.SetSeatedState(true);
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(true, distance: 8.6f, pitch: 26f, fov: 62f, lockYaw: transform.eulerAngles.y);
            }

            ResetPositions(offenseTeam: 0);
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

            if (_player != null)
            {
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                _player.Humanoid?.SetLookAtPoint(false, Vector3.zero);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
                _player.SetSeatedState(false);
            }
            _player = null;
            _matchOver = false;

            if (_gold != null && _gold.Length > 0 && _gold[0] != null)
            {
                _gold[0].gameObject.SetActive(true);
            }

            _goldScore = 0;
            _navyScore = 0;
            ResetPositions(offenseTeam: 0);
            UpdateScoreboard();
        }

        public string ControlsHintKey() => "sports.basketball.controls";

        public string PrimaryButtonLabel()
        {
            if (_matchOver) return LocalizationService.Tr("sports.btn.rematch", "Tekrar");
            if (PlayerHasBall)
            {
                return PlayerInThreePointZone
                    ? LocalizationService.Tr("sports.btn.shoot_3pt", "3'lük!")
                    : LocalizationService.Tr("sports.btn.shoot_2pt", "Şut At");
            }
            if (_offenseTeam == 0 && _handlerIdx == 1) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            return LocalizationService.Tr("sports.btn.steal", "Top Kap");
        }

        public string SecondaryButtonLabel()
        {
            if (PlayerHasBall) return LocalizationService.Tr("sports.btn.pass", "Pas Ver");
            if (_offenseTeam == 0 && _handlerIdx == 1) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            return LocalizationService.Tr("sports.btn.steal", "Top Kap");
        }

        public string HudText()
        {
            string title = LocalizationService.Tr("sports.basketball.title", "Basketbol");
            string you = LocalizationService.Tr("sports.basketball.team_you", "Sen");
            string rival = LocalizationService.Tr("sports.basketball.team_rival", "Lacivert");
            string status;

            if (_matchOver)
            {
                status = LocalizationService.Tr(_playerWonMatch ? "sports.basketball.won" : "sports.basketball.lost");
            }
            else if (_statusBannerTimer > 0f && !string.IsNullOrEmpty(_statusBannerKey))
            {
                status = LocalizationService.Tr(_statusBannerKey);
            }
            else if (PlayerHasBall)
            {
                status = LocalizationService.Tr(PlayerInThreePointZone ? "sports.basketball.zone_3pt" : "sports.basketball.zone_2pt");
            }
            else if (_offenseTeam == 0)
            {
                status = LocalizationService.Tr("sports.basketball.mate_ball");
            }
            else
            {
                status = LocalizationService.Tr("sports.basketball.defend");
            }

            return $"{title}  ·  {you} {_goldScore} - {_navyScore} {rival}\n{status}";
        }

        public void TriggerPrimaryAction()
        {
            if (_player == null) return;

            if (_matchOver)
            {
                _goldScore = 0;
                _navyScore = 0;
                _matchOver = false;
                ResetPositions(offenseTeam: 0);
                UpdateScoreboard();
                GameFeelAudio.PlayUiClick();
                HudRevision++;
                return;
            }

            if (PlayerHasBall)
            {
                LaunchPlayerJumpShot();
                return;
            }

            // Ask Gold teammate for an instant chest pass!
            if (_offenseTeam == 0 && _handlerIdx == 1 && _phase == 0 && _gold != null && _gold.Length > 1)
            {
                _gold[1].PlaySportAnim(SportAnimKind.BasketballPass, 0.42f);
                _phase = 1;
                _flightStart = _ballPos;
                _flightEnd = _playerLocalPos + Vector3.up * 1.15f;
                _flightDuration = 0.40f;
                _flightElapsed = 0f;
                _flightArcHeight = 0.38f;
                PlayBounce(0.36f, alwaysHear: true);
                HudRevision++;
                return;
            }

            // Defend / steal from Navy!
            if (_offenseTeam == 1 && _phase == 0 && _navy != null && _handlerIdx < _navy.Length)
            {
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SoccerSteal, 0.42f);
                Vector3 carrierPos = _navy[_handlerIdx].transform.localPosition;
                Vector3 d = carrierPos - _playerLocalPos;
                d.y = 0f;
                if (d.magnitude <= 3.3f)
                {
                    _offenseTeam = 0;
                    _handlerIdx = 0;
                    _phase = 0;
                    PlayBounce(0.45f, alwaysHear: true);
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(_ballPos), new Color(1.0f, 0.84f, 0.24f), count: 8, spread: 0.4f);
                    SetTempStatus("sports.basketball.stole", 1.2f);
                }
            }
        }

        public void TriggerSecondaryAction()
        {
            if (_player == null || _matchOver) return;

            if (PlayerHasBall && _gold != null && _gold.Length > 1 && _gold[1] != null)
            {
                _player.Humanoid?.PlaySportAnim(SportAnimKind.BasketballPass, 0.42f);
                _phase = 1;
                _releaseGatherStart = _ballPos;
                _flightStart = _playerLocalPos + _playerLocalRot * new Vector3(0f, 1.24f, 0.46f);
                _flightEnd = _gold[1].transform.localPosition + Vector3.up * 1.15f;
                _flightDuration = 0.42f;
                _flightElapsed = 0f;
                _releaseDelayDuration = 0.12f;
                _releaseDelayTimer = 0.12f;
                _flightArcHeight = 0.40f;
                HudRevision++;
                return;
            }

            TriggerPrimaryAction();
        }

        public void TriggerPrimaryRelease(float holdSeconds) { }

        public void TriggerSecondaryRelease(float holdSeconds) { }

        public SportHudMeterState GetHudMeterState()
        {
            return default;
        }

        private void LaunchPlayerJumpShot()
        {
            Vector3 hoopPos = new Vector3(0f, RimY, RimZ);
            Vector3 flatPlayer = new Vector3(_playerLocalPos.x, 0f, _playerLocalPos.z);
            float distToRim = Vector3.Distance(flatPlayer, new Vector3(0f, 0f, RimZ));

            // Turn player smoothly toward the basket
            Vector3 toHoop = new Vector3(0f, 0f, RimZ) - flatPlayer;
            if (toHoop.sqrMagnitude > 0.01f)
            {
                _playerLocalRot = Quaternion.LookRotation(toHoop.normalized, Vector3.up);
                _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation * _playerLocalRot);
            }

            _pendingShotPoints = distToRim > 4.75f ? 3 : 2;
            float makeChance = distToRim < 3.4f ? 0.94f : (distToRim <= 4.75f ? 0.84f : 0.74f);
            _shotWillScore = Random.value < makeChance;

            _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            _player.Humanoid?.PlaySportAnim(SportAnimKind.BasketballJumpShot, 0.74f);

            _phase = 2;
            _releaseGatherStart = _playerLocalPos + _playerLocalRot * new Vector3(0.08f, 1.22f, 0.28f);
            _flightStart = _playerLocalPos + _playerLocalRot * new Vector3(0.06f, 2.18f, 0.20f);
            _flightEnd = _shotWillScore
                ? hoopPos
                : hoopPos + new Vector3(Random.Range(-0.42f, 0.42f), 0.1f, -0.32f);
            _flightDuration = Mathf.Clamp(0.68f + distToRim * 0.055f, 0.72f, 1.12f);
            _flightElapsed = 0f;
            _releaseDelayDuration = 0.24f; // Ball rises in shooter's hands and leaves right at jump-shot extension
            _releaseDelayTimer = 0.24f;
            _flightArcHeight = Mathf.Clamp(1.35f + distToRim * 0.12f, 1.45f, 2.35f);
            HudRevision++;
        }

        private void SetTempStatus(string key, float duration)
        {
            _statusBannerKey = key;
            _statusBannerTimer = duration;
            HudRevision++;
        }

        private void Update()
        {
            if (_gold == null || _navy == null || _ball == null) return;
            if (_player != null && PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_statusBannerTimer > 0f)
            {
                _statusBannerTimer -= dt;
                if (_statusBannerTimer <= 0f) HudRevision++;
            }

            ClearHandPoses(_gold);
            ClearHandPoses(_navy);
            if (_player != null)
            {
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }

            if (_matchOver)
            {
                IdleAll();
                if (_player != null) _player.Humanoid?.SetLocomotion(0f, true, 0f);
                UpdateBallTransform(dt);
                return;
            }

            if (_player != null)
            {
                UpdatePlayerOnCourt(dt);
            }

            ProceduralHumanoid[] off = _offenseTeam == 0 ? _gold : _navy;
            ProceduralHumanoid[] def = _offenseTeam == 0 ? _navy : _gold;
            float dirZ = _offenseTeam == 0 ? 1f : -1f;
            Vector3 hoopPos = new Vector3(0f, RimY, dirZ * RimZ);
            bool handlerIsPlayer = _player != null && _offenseTeam == 0 && _handlerIdx == 0;
            bool mateIsPlayer = _player != null && _offenseTeam == 0 && _handlerIdx == 1;
            ProceduralHumanoid handlerNpc = off[_handlerIdx];
            ProceduralHumanoid mateNpc = off[1 - _handlerIdx];

            if (_phase == 0)
            {
                Vector3 handlerPos = handlerIsPlayer ? _playerLocalPos : handlerNpc.transform.localPosition;
                Quaternion handlerRot = handlerIsPlayer ? _playerLocalRot : handlerNpc.transform.localRotation;

                if (!handlerIsPlayer)
                {
                    Vector3 driveSpot = new Vector3(
                        (_handlerIdx == 0 ? -2.2f : 2.2f) + Mathf.Sin(Time.time * 1.4f) * 1.1f,
                        0f,
                        dirZ * 4.4f);
                    MoveCourtPlayer(handlerNpc, driveSpot, _player != null && _offenseTeam == 1 ? 2.75f : 3.0f, dt, hoopPos);
                    handlerPos = handlerNpc.transform.localPosition;
                    handlerRot = handlerNpc.transform.localRotation;
                }

                if (!mateIsPlayer && mateNpc != null && mateNpc.gameObject.activeSelf)
                {
                    Vector3 wingSpot = new Vector3(handlerPos.x > 0f ? -3.4f : 3.4f, 0f, dirZ * 5.6f);
                    MoveCourtPlayer(mateNpc, wingSpot, 3.2f, dt, handlerPos);
                }

                // Defenders guard offense
                if (def[0] != null && def[0].gameObject.activeSelf)
                {
                    MoveCourtPlayer(def[0], Vector3.Lerp(handlerPos, new Vector3(0f, 0f, dirZ * RimZ), 0.36f), _player != null ? 2.85f : 3.1f, dt, handlerPos);
                }
                if (def[1] != null && def[1].gameObject.activeSelf)
                {
                    Vector3 matePos = mateIsPlayer ? _playerLocalPos : mateNpc.transform.localPosition;
                    MoveCourtPlayer(def[1], Vector3.Lerp(matePos, new Vector3(0f, 0f, dirZ * RimZ), 0.40f), 3.0f, dt, matePos);
                }

                // Synced rhythmic bounce dribble + right-hand IK push!
                _dribbleCycle += dt * 4.2f;
                float bounceSin = Mathf.Abs(Mathf.Sin(_dribbleCycle * Mathf.PI));
                int tick = Mathf.FloorToInt(_dribbleCycle);
                if (tick != _lastBounceTick)
                {
                    _lastBounceTick = tick;
                    PlayBounce(0.28f, alwaysHear: _player != null);
                }

                Vector3 dribbleBase = handlerPos + handlerRot * new Vector3(0.32f, 0f, 0.36f);
                _ballPos = new Vector3(dribbleBase.x, BallRadius + bounceSin * 0.88f, dribbleBase.z);
                // Keep dribbling palm cleanly on top surface of the ball (never clipping inside the sphere)
                float palmY = Mathf.Max(0.84f, _ballPos.y + BallRadius + 0.015f);
                Vector3 handWorld = transform.TransformPoint(new Vector3(dribbleBase.x, palmY, dribbleBase.z));
                ProceduralHumanoid activeHum = handlerIsPlayer ? _player.Humanoid : handlerNpc;
                activeHum?.SetDualHandAction(true, Vector3.zero, handWorld, 0f, 0.92f, 6f, 8f, 0f);

                if (!handlerIsPlayer)
                {
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        bool passToMate = Random.value < (mateIsPlayer ? 0.58f : 0.42f);
                        if (passToMate)
                        {
                            handlerNpc.PlaySportAnim(SportAnimKind.BasketballPass, 0.42f);
                            _phase = 1;
                            _releaseGatherStart = _ballPos;
                            _flightStart = handlerPos + handlerRot * new Vector3(0f, 1.24f, 0.46f);
                            _flightEnd = (mateIsPlayer ? _playerLocalPos : mateNpc.transform.localPosition) + Vector3.up * 1.15f;
                            _flightDuration = 0.46f;
                            _flightElapsed = 0f;
                            _releaseDelayDuration = 0.12f;
                            _releaseDelayTimer = 0.12f;
                            _flightArcHeight = 0.42f;
                        }
                        else
                        {
                            handlerNpc.PlaySportAnim(SportAnimKind.BasketballJumpShot, 0.72f);
                            _phase = 2;
                            _pendingShotPoints = 2;
                            _shotWillScore = Random.value < (_player != null && _offenseTeam == 1 ? 0.52f : 0.64f);
                            _releaseGatherStart = handlerPos + handlerRot * new Vector3(0.08f, 1.20f, 0.26f);
                            _flightStart = handlerPos + handlerRot * new Vector3(0.06f, 2.12f, 0.18f);
                            _flightEnd = _shotWillScore
                                ? hoopPos
                                : hoopPos + new Vector3(Random.Range(-0.45f, 0.45f), 0.1f, -dirZ * 0.35f);
                            _flightDuration = 0.92f;
                            _flightElapsed = 0f;
                            _releaseDelayDuration = 0.24f;
                            _releaseDelayTimer = 0.24f;
                            _flightArcHeight = 1.65f;
                        }
                    }
                }
            }
            else if (_phase == 1)
            {
                IdleAll();
                if (_releaseDelayTimer > 0f)
                {
                    _releaseDelayTimer -= dt;
                    float gT = _releaseDelayDuration > 0.001f ? 1f - Mathf.Clamp01(_releaseDelayTimer / _releaseDelayDuration) : 1f;
                    _ballPos = Vector3.Lerp(_releaseGatherStart, _flightStart, Mathf.SmoothStep(0f, 1f, gT));
                    if (_releaseDelayTimer <= 0f) PlayBounce(0.34f, alwaysHear: _player != null);
                }
                else
                {
                    _flightElapsed += dt;
                    float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                    _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, _flightArcHeight);
                    if (u >= 1f)
                    {
                        _handlerIdx = 1 - _handlerIdx;
                        _phase = 0;
                        _phaseTimer = Random.Range(1.2f, 2.0f);
                        HudRevision++;
                    }
                }
            }
            else if (_phase == 2)
            {
                IdleAll();
                if (_releaseDelayTimer > 0f)
                {
                    _releaseDelayTimer -= dt;
                    float gT = _releaseDelayDuration > 0.001f ? 1f - Mathf.Clamp01(_releaseDelayTimer / _releaseDelayDuration) : 1f;
                    _ballPos = Vector3.Lerp(_releaseGatherStart, _flightStart, Mathf.SmoothStep(0f, 1f, gT));
                    if (_releaseDelayTimer <= 0f) PlayBounce(0.40f, alwaysHear: _player != null);
                }
                else
                {
                    _flightElapsed += dt;
                    float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                    _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, _flightArcHeight);

                    if (u >= 1f)
                {
                    if (_shotWillScore)
                    {
                        int pts = _pendingShotPoints;
                        if (_offenseTeam == 0) _goldScore += pts; else _navyScore += pts;
                        UpdateScoreboard();

                        ProceduralHumanoid shooter = handlerIsPlayer ? _player.Humanoid : handlerNpc;
                        shooter?.PlaySportAnim(SportAnimKind.VictoryJump, 0.64f);
                        PlayBounce(0.48f, alwaysHear: _player != null);

                        if (_player != null)
                        {
                            if (_offenseTeam == 0)
                            {
                                GameFeelAudio.PlayCoinChime();
                                SaveManager.Instance?.AddMoney(pts == 3 ? 25 : 15);
                                SetTempStatus(pts == 3 ? "sports.basketball.scored_3pt" : "sports.basketball.scored_2pt", 1.35f);
                            }
                            else
                            {
                                SetTempStatus("sports.basketball.rival_scored", 1.2f);
                            }

                            if (_goldScore >= PointsToWin || _navyScore >= PointsToWin)
                            {
                                _matchOver = true;
                                _playerWonMatch = _goldScore >= PointsToWin;
                                if (_playerWonMatch)
                                {
                                    GameFeelAudio.PlayMilestoneFanfare();
                                    SaveManager.Instance?.AddMoney(90);
                                    NeighborhoodReputation.AddReputation(30, LocalizationService.Tr("sports.basketball.win_toast", "Basketbol Maçı Kazanıldı! (+90 ₺)"));
                                }
                                SportMatchEvents.NotifyMatchFinished("sport_basketball", _playerWonMatch);
                                HudRevision++;
                            }
                        }

                        if (_player != null || IsPlayerNear(24f))
                        {
                            GameFeelAudio.SpawnBurst(
                                transform.TransformPoint(hoopPos),
                                _offenseTeam == 0 ? new Color(1.0f, 0.78f, 0.18f) : new Color(0.28f, 0.56f, 0.96f),
                                count: pts == 3 ? 16 : 10, spread: 0.55f);
                        }
                    }
                    else
                    {
                        PlayBounce(0.50f, alwaysHear: _player != null);
                        if (_player != null && _offenseTeam == 0)
                        {
                            SetTempStatus("sports.basketball.missed", 1.0f);
                        }
                    }

                    // Switch possession & rebound
                    _phase = 3;
                    _phaseTimer = 0.95f;
                    _flightStart = _ballPos;
                    _offenseTeam = 1 - _offenseTeam;
                    _handlerIdx = (_player != null && _offenseTeam == 0) ? 0 : Random.Range(0, 2);
                    Vector3 reboundTarget = (_player != null && _offenseTeam == 0 && _handlerIdx == 0)
                        ? _playerLocalPos
                        : (_offenseTeam == 0 ? _gold : _navy)[_handlerIdx].transform.localPosition;
                    _flightEnd = reboundTarget + Vector3.up * 1.1f;
                    _flightElapsed = 0f;
                    _flightDuration = 0.68f;
                    HudRevision++;
                }
                }
            }
            else
            {
                _flightElapsed += dt;
                float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, 0.65f);
                IdleAll();
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    _phase = 0;
                    _phaseTimer = Random.Range(1.7f, 2.5f);
                    HudRevision++;
                }
            }

            UpdateBallTransform(dt);
            CheckHudStateChanged();
        }

        private void UpdatePlayerOnCourt(float dt)
        {
            Vector2 move = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            bool sprinting = InputHub.Instance != null && InputHub.Instance.SprintHeld;
            float maxSpd = sprinting ? 5.6f : 4.35f;

            Vector3 localDir = new Vector3(move.x, 0f, move.y);
            float mag = Mathf.Clamp01(localDir.magnitude);
            float speed = mag > 0.08f ? mag * maxSpd : 0f;
            Vector3 worldDir = _player.transform.forward;

            if (speed > 0.05f)
            {
                localDir.Normalize();
                _playerLocalPos += localDir * (speed * dt);
                _playerLocalPos.x = Mathf.Clamp(_playerLocalPos.x, -HalfW + 0.65f, HalfW - 0.65f);
                _playerLocalPos.z = Mathf.Clamp(_playerLocalPos.z, -HalfL + 0.85f, HalfL - 0.85f);
                worldDir = transform.TransformDirection(localDir);
            }

            // Face basket when holding ball, or face movement/ball otherwise
            Vector3 facePoint = PlayerHasBall ? new Vector3(0f, 0f, RimZ) : (speed > 0.2f ? _playerLocalPos + localDir * 2f : _ballPos);
            Vector3 look = facePoint - _playerLocalPos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.02f)
            {
                _playerLocalRot = Quaternion.Slerp(_playerLocalRot, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-10f * dt));
            }

            _player.TeleportTo(transform.TransformPoint(_playerLocalPos), transform.rotation * _playerLocalRot);
            _player.Humanoid?.SetLocomotion(speed, true, 0f, worldDir, sampleGround: false);
            _player.Humanoid?.SetLookAtPoint(true, _ball.position);
        }

        private void CheckHudStateChanged()
        {
            if (_player == null) return;
            int hash = _offenseTeam * 100 + _handlerIdx * 10 + _phase + (PlayerInThreePointZone ? 1000 : 0);
            if (hash != _lastHudHash)
            {
                _lastHudHash = hash;
                HudRevision++;
            }
        }

        private void UpdateBallTransform(float dt)
        {
            _ball.localPosition = _ballPos;
            _ball.Rotate(Vector3.right * (dt * 240f), Space.Self);
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(_ballPos.x, 0.024f, _ballPos.z);
                float s = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(_ballPos.y / 4.2f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private void ClearHandPoses(ProceduralHumanoid[] team)
        {
            for (int i = 0; i < team.Length; i++)
            {
                if (team[i] != null && team[i].gameObject.activeSelf)
                {
                    team[i].SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                }
            }
        }

        private void IdleAll()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_gold[i] != null && _gold[i].gameObject.activeSelf)
                    _gold[i].SetLocomotion(0f, true, 0f, _gold[i].transform.forward, sampleGround: false);
                if (_navy[i] != null && _navy[i].gameObject.activeSelf)
                    _navy[i].SetLocomotion(0f, true, 0f, _navy[i].transform.forward, sampleGround: false);
            }
        }

        private void MoveCourtPlayer(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, Vector3 faceTarget)
        {
            localTarget.x = Mathf.Clamp(localTarget.x, -HalfW + 0.8f, HalfW - 0.8f);
            localTarget.z = Mathf.Clamp(localTarget.z, -HalfL + 0.9f, HalfL - 0.9f);
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float spd = 0f;
            Vector3 wDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                spd = Mathf.Min(maxSpeed, dist * 4.2f);
                Vector3 dir = delta / dist;
                pos += dir * (spd * dt);
                hum.transform.localPosition = pos;
                wDir = transform.TransformDirection(dir);
            }

            Vector3 look = faceTarget - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                hum.transform.localRotation = Quaternion.Slerp(
                    hum.transform.localRotation,
                    Quaternion.LookRotation(look.normalized, Vector3.up),
                    1f - Mathf.Exp(-10f * dt));
            }
            hum.SetLocomotion(spd, true, 0f, wDir, sampleGround: false);
            if (_ball != null) hum.SetLookAtPoint(true, _ball.position);
        }

        private static Vector3 ParabolicLerp(Vector3 a, Vector3 b, float u, float extraArc)
        {
            Vector3 p = Vector3.Lerp(a, b, u);
            p.y += 4f * extraArc * u * (1f - u);
            return p;
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = $"HAMİDİYE BASKETBOL\nSARI {_goldScore} - {_navyScore} LACİVERT";
            }
        }

        private void PlayBounce(float vol, bool alwaysHear = false)
        {
            if (_audio == null || _bounceClip == null) return;
            if (!alwaysHear && _player == null && !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.92f, 1.08f);
            _audio.PlayOneShot(_bounceClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }

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
            return _leagueTier switch
            {
                2 => "Şampiyonlar",
                1 => "Mahalle Ligi",
                _ => "Acemi Lig"
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
            SetCallout($"{GetLeagueLabel()} · Servis Sende!", new Color(0.24f, 0.94f, 0.80f), 1.6f);
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
            if (_isChargingServe) return "BIRAK!";
            if (_awaitingPlayerServe) return "Servis (Tut)";
            if (_targetTeam == 0 && _targetIdx == 1)
            {
                return _touchStep >= 1
                    ? LocalizationService.Tr("sports.btn.spike", "Smaç!")
                    : LocalizationService.Tr("sports.btn.bump", "Manşet");
            }
            return "Hazır";
        }

        public string SecondaryButtonLabel()
        {
            if (IsPlayerAttackSet) return "Plase!";
            if (CanPlayerNetBlock) return "Blok!";
            if (_targetTeam == 0 && _targetIdx == 1 && _touchStep == 0) return "Uç!";
            return "Blok!";
        }

        public string HudText()
        {
            string title = LocalizationService.Tr("sports.volleyball.title", "Voleybol");
            string you = LocalizationService.Tr("sports.volleyball.team_you", "Sen");
            string rival = LocalizationService.Tr("sports.volleyball.team_rival", "Bordo");
            string rallyInfo = _rallyCount > 0 ? $" · Ralli {_rallyCount}" : string.Empty;
            string status;

            if (_matchOver)
            {
                status = _playerWonMatch
                    ? $"ZAFER! ({_playerBlocksScored} Blok · {_playerAcesScored} Ace) — Tekrar için Vur"
                    : "Maç Bitti — Rövanş için Vur!";
            }
            else if (_pauseTimer > 0f)
            {
                status = _lastPointTeal ? "SAYI SENİN!" : "Bordo Sayı Aldı · Hazırlan!";
            }
            else if (_isChargingServe)
            {
                status = "Servis Gücünü Ayarla · Altın bölgede bırak → Sıçramalı ACE!";
            }
            else if (_awaitingPlayerServe)
            {
                status = "Servis (Basılı Tut) · Yön tuşuyla hedefle!";
            }
            else if (IsPlayerAttackSet)
            {
                status = "Smaç! (Vur) veya Blok Üstü Plase! (İkinci Tuş) · Joystick ile hedefle";
            }
            else if (CanPlayerNetBlock)
            {
                status = "Rakip Smaç Hazırlıyor! Filede Blok! (İkinci Tuş) zıpla!";
            }
            else if (_targetTeam == 0 && _targetIdx == 1)
            {
                status = _playerInHitWindow
                    ? "ŞİMDİ MANŞET! (Uzak topta Uç! ile dalış yap)"
                    : "Halkaya yaklaş · Altın zamanlamada Manşet vur!";
            }
            else if (_targetTeam == 0)
            {
                status = "Pasörün topu kaldırıyor · Fileye yaklaşıp smaca hazırlan!";
            }
            else
            {
                status = "Fileye koşup Blok kur veya arka sahayı koru!";
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
                state.PrimaryLabel = over ? "FİLE RİSKİ!" : (sweet ? "ALTIN ACE SERVİS!" : "SERVİS GÜCÜ");
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
                state.PrimaryLabel = _touchStep >= 1 ? "SMAÇ / PLASE ZAMANI" : "MANŞET ZAMANI";
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
                state.PrimaryLabel = "FİLE BLOK ZAMANI!";
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
            SetCallout("UÇARAK KURTARIŞ!", new Color(0.28f, 0.94f, 0.82f), 1.15f);
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
                SetCallout("SERVİS FİLEYE TAKILDI!", new Color(0.96f, 0.38f, 0.32f), 1.35f);
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
                SetCallout($"SIÇRAMALI SERVİS! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", new Color(1.0f, 0.86f, 0.24f), 1.25f);
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
                        SetCallout("MÜKEMMEL MANŞET! Smaca çık!", new Color(1.0f, 0.86f, 0.24f), 0.95f);
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
                        SetCallout("RAKİP BLOKLADI · TOPU KURTAR!", new Color(1.0f, 0.46f, 0.28f), 1.25f);
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
                    SetCallout("AKILLI PLASE! (Blok Arkası)", new Color(0.36f, 0.96f, 0.84f), 1.25f);
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
                        SetCallout($"MÜKEMMEL SMAÇ! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", new Color(1.0f, 0.86f, 0.22f), 1.30f);
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
                        SetCallout($"ACE SERVİS SAYISI! ({Mathf.RoundToInt(_lastHitKmh)} km/s)", new Color(1.0f, 0.86f, 0.22f), 1.6f);
                    }
                    else
                    {
                        SetCallout($"MÜTHİŞ SAYI! (Ralli {_rallyCount})", new Color(0.28f, 0.95f, 0.78f), 1.5f);
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
                        SetCallout("DUVAR BLOK SAYISI!", new Color(0.24f, 0.96f, 0.84f), 1.65f);
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
                        NeighborhoodReputation.AddReputation(35 + _leagueTier * 10, $"Voleybol [{GetLeagueLabel()}] Zaferi! (+{reward} ₺)");
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
                _scoreboard.text = $"HAMİDİYE VOLEYBOL\nTURKUAZ {_tealScore} - {_burgundyScore} BORDO";
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
