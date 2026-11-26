using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
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
            SetCallout(SportLoc.L($"{GetLeagueLabel()} · Maç Başladı!", $"{GetLeagueLabel()} · Kickoff!"), new Color(0.28f, 0.94f, 0.80f), 1.6f);
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
            if (_isChargingShot) return SportLoc.L("BIRAK!", "RELEASE!");
            if (PlayerHasBall) return LocalizationService.Tr("sports.btn.shoot", "Şut (Tut)");
            if (_carrierTeam == 0 && _carrierIdx == 2) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            if (_carrierTeam == -1 && Vector3.Distance(new Vector3(_ballPos.x, 0f, _ballPos.z), _playerLocalPos) <= 2.65f)
                return SportLoc.L("Vole!", "Volley!");
            return LocalizationService.Tr("sports.btn.steal", "Pres");
        }

        public string SecondaryButtonLabel()
        {
            if (_isChargingPass) return SportLoc.L("ARA PASI!", "THROUGH!");
            if (PlayerHasBall) return LocalizationService.Tr("sports.btn.pass", "Pas / Ara");
            if (_carrierTeam == 0 && _carrierIdx == 2) return LocalizationService.Tr("sports.btn.call_pass", "Pas İste");
            return SportLoc.L("Kay!", "Slide!");
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
                    ? SportLoc.L($"ZAFER! ({_playerShotsOnTarget} Şut · {_playerTacklesWon} Kapma) — Tekrar için Vur", $"VICTORY! ({_playerShotsOnTarget} Shots · {_playerTacklesWon} Tackles) — Tap to Rematch")
                    : SportLoc.L($"Maç Bitti ({_playerShotsOnTarget} Şut) — Rövanş için Vur", $"Full Time ({_playerShotsOnTarget} Shots) — Tap for Rematch");
                status = winTxt;
            }
            else if (_goalResetTimer > 0f)
            {
                status = _lastGoalByRed
                    ? SportLoc.L($"GOOOL! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", $"GOAL! ({Mathf.RoundToInt(_lastShotKmh)} km/h)")
                    : SportLoc.L("Mavi Takım Gol Attı! Santra hazırlanıyor...", "Blue Team Scored! Preparing kickoff...");
            }
            else if (_isChargingShot)
            {
                status = SportLoc.L("Güç doldur · Altın bölgede bırak → 90'a Falsolu Şut!", "Charge power · Release in gold zone → Top-Corner Curler!");
            }
            else if (_isChargingPass)
            {
                status = SportLoc.L("Kısa bırak: Pas · Basılı tut: Defans Arkası Ara Pası!", "Quick tap: Pass · Hold: Lobbed Through Ball!");
            }
            else if (PlayerHasBall)
            {
                status = SportLoc.L("Şut (Basılı Tut) · Pas/Ara Pası · Koş dokun: Bilek Çalımı!", "Shoot (Hold) · Pass / Through · Tap Sprint: Step-Over Feint!");
            }
            else if (_carrierTeam == 0)
            {
                status = SportLoc.L("Boşluğa koş · Pas İste ile verkaç yap!", "Run into space · Call for Pass for a 1-2!");
            }
            else if (_carrierTeam == 1)
            {
                status = SportLoc.L("Pres (Vur) veya Kayarak Müdahale (Kay!) ile topu kap!", "Press (Primary) or Slide Tackle (Slide!) to win the ball!");
            }
            else
            {
                status = SportLoc.L("Serbest top! Yaklaşınca tek dokunuş vole vurabilirsin!", "Loose ball! Get close for a one-touch volley!");
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
                state.PrimaryLabel = over ? SportLoc.L("AŞIRI GÜÇ!", "OVERCHARGED!") : (inSweet ? SportLoc.L("ALTIN FALSO!", "GOLDEN CURVE!") : SportLoc.L("ŞUT GÜCÜ", "SHOT POWER"));
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
                state.PrimaryLabel = through ? SportLoc.L("ARA PASI (LOB)", "THROUGH BALL (LOB)") : SportLoc.L("YERDEN PAS", "GROUND PASS");
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
                SetCallout(SportLoc.L("VERKAÇ PASI GELDİ!", "1-2 RETURN PASS!"), new Color(0.28f, 0.92f, 0.80f), 1.1f);
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
                SetCallout(isThroughBall ? SportLoc.L("DEFANS ARKASI ARA PASI!", "LOBBED THROUGH BALL!") : SportLoc.L("VERKAÇ PASI!", "ONE-TWO PASS!"), new Color(0.30f, 0.88f, 1.0f), 1.15f);
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
                SetCallout(isVolley ? SportLoc.L($"TEK DOKUNUŞ VOLE! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", $"ONE-TOUCH VOLLEY! ({Mathf.RoundToInt(_lastShotKmh)} km/h)") : SportLoc.L($"90'A FALSOLU FÜZE! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", $"TOP-CORNER ROCKET! ({Mathf.RoundToInt(_lastShotKmh)} km/h)"), new Color(1.0f, 0.86f, 0.24f), 1.35f);
            }
            else if (isPlacedLow)
            {
                SetTrailColor(new Color(0.28f, 0.94f, 0.78f, 0.75f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.58f, true,
                    spawnBurst: true, burstColor: new Color(0.28f, 0.92f, 0.78f), burstCount: 7, burstSpread: 0.30f);
                SetCallout(SportLoc.L($"YERDEN PLASE ({Mathf.RoundToInt(_lastShotKmh)} km/s)", $"LOW FINESSE ({Mathf.RoundToInt(_lastShotKmh)} km/h)"), new Color(0.32f, 0.92f, 0.78f), 1.0f);
            }
            else if (isOvercharged)
            {
                SetTrailColor(new Color(1.0f, 0.36f, 0.24f, 0.88f));
                QueueSyncedKick(shotVel, curveAccel, kickDelay, 0.20f, 0.65f, true,
                    shake: true, shakeAmt: 0.12f, shakeDur: 0.16f);
                SetCallout(SportLoc.L($"SERT ŞUT! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", $"POWER SHOT! ({Mathf.RoundToInt(_lastShotKmh)} km/h)"), new Color(1.0f, 0.48f, 0.26f), 1.0f);
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
            SetCallout(SportLoc.L("BİLEK ÇALIMI!", "STEP-OVER FEINT!"), new Color(0.32f, 0.95f, 0.84f), 1.0f);
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
                    AwardPlayerSteal(SportLoc.L("TOPU KAPTIN!", "BALL WON!"));
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
                        if (_player != null)
                        {
                            SportsArenaAtmosphere.SpawnTurfKickPuff(transform.TransformPoint(_ballPos), heavy: false);
                        }
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
                                SetCallout(towardBlue ? SportLoc.L("KALECİ ÇELDİ · RİBAUND!", "KEEPER PARRIED · REBOUND!") : SportLoc.L("KALECİN KURTARDI!", "KEEPER SAVE!"), new Color(0.98f, 0.78f, 0.28f), 1.25f);
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
                                SetCallout(towardBlue ? SportLoc.L("KALECİ TUTTU!", "KEEPER CAUGHT IT!") : SportLoc.L("HARİKA KURTARIŞ!", "GREAT SAVE!"), new Color(0.42f, 0.82f, 1.0f), 1.0f);
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
                    SetCallout(SportLoc.L("DİREKTEN DÖNDÜ!", "OFF THE WOODWORK!"), new Color(1.0f, 0.62f, 0.22f), 1.35f);
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
                    SetCallout(SportLoc.L("ÜSTTEN AUT! (Gücü altın bölgede bırak)", "OVER THE BAR! (Release in gold zone)"), new Color(0.96f, 0.46f, 0.36f), 1.4f);
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
                    SetCallout(SportLoc.L($"GOOOL! · {Mathf.RoundToInt(_lastShotKmh)} km/s (+{35 + _leagueTier * 10} ₺)", $"GOAL! · {Mathf.RoundToInt(_lastShotKmh)} km/h (+{35 + _leagueTier * 10} ₺)"), new Color(1.0f, 0.85f, 0.22f), 2.1f);
                }
                else
                {
                    SetCallout(SportLoc.L("Mavi Takım Gol Attı!", "Blue Team Scored!"), new Color(0.95f, 0.36f, 0.32f), 2.0f);
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
                        NeighborhoodReputation.AddReputation(35 + _leagueTier * 10, SportLoc.L($"Halı Saha [{GetLeagueLabel()}] Zaferi! (+{reward} ₺)", $"Mini Football [{GetLeagueLabel()}] Victory! (+{reward} ₺)"));
                    }
                    SportMatchEvents.NotifyMatchFinished("sport_football", _playerWonMatch);
                }
                HudRevision++;
            }

            if (_player != null || IsPlayerNear(26f))
            {
                Vector3 goalWorld = transform.TransformPoint(_ballPos + Vector3.up * 0.8f);
                SportsArenaAtmosphere.PulseNearestNet(goalWorld, maxDist: 4.5f);
                SportsArenaAtmosphere.CelebrateEvent(transform.position, "football", redScored, bigPlay: true);
                GameFeelAudio.SpawnBurst(
                    goalWorld,
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
                        AwardPlayerSteal(SportLoc.L("TEMİZ KAYARAK MÜDAHALE!", "CLEAN SLIDE TACKLE!"));
                    }
                }
                else if (!_slideConnected && _carrierTeam == -1)
                {
                    Vector3 d = _ballPos - _playerLocalPos;
                    d.y = 0f;
                    if (d.magnitude <= 1.9f && _ballPos.y < 1.1f)
                    {
                        _slideConnected = true;
                        AwardPlayerSteal(SportLoc.L("KAYARAK TOPU ALDIN!", "SLIDING INTERCEPTION!"));
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
                _scoreboard.text = SportLoc.L($"HAMİDİYE HALI SAHA\nKIRMIZI {_redScore} - {_blueScore} MAVİ", $"HAMIDIYE FOOTBALL\nRED {_redScore} - {_blueScore} BLUE");
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

}
