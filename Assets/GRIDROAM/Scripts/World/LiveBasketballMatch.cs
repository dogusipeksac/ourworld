using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
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
                _scoreboard.text = SportLoc.L($"HAMİDİYE BASKETBOL\nSARI {_goldScore} - {_navyScore} LACİVERT", $"HAMIDIYE BASKETBALL\nGOLD {_goldScore} - {_navyScore} NAVY");
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

}
