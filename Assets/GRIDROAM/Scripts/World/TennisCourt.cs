using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public sealed class TennisCourt : MonoBehaviour
    {
        internal const float HalfWidth = 4.12f;
        internal const float HalfLength = 11.88f;
        internal const float LineHeight = 0.003f;
        internal const float PlayerBaseline = -11f;
        internal const float BallRadius = 0.034f;
        private const float CourtSurface = 0.002f;
        private const float BallFloor = CourtSurface + BallRadius;
        private const float Gravity = 9.81f;
        private const float ContactHeight = 0.95f;
        private const float ContactSide = 0.65f;
        private const float ContactForward = 0.28f;
        private const float ContactWindow = 0.72f;
        private const float ServeTime = 0.56f;
        private const float ServeRelease = 0.12f;
        private const int PointsToWin = 3;
        private const float AmbientActiveRangeSq = 78f * 78f;

        private enum Phase { Serve, Serving, ToOpponent, ToPlayer, Point, MatchOver }

        private struct Flight
        {
            private const float BounceSpeedFraction = 0.64f;
            private const float AfterBounceDuration = 0.54f;

            public Vector3 Start, End;
            public float FirstLeg;
            public float Duration => FirstLeg + AfterBounceDuration;

            private float BounceVerticalSpeed => (End.y - BallFloor) / AfterBounceDuration +
                0.5f * Gravity * AfterBounceDuration;
            public float SecondBounceTime => FirstLeg + 2f * BounceVerticalSpeed / Gravity;

            public Vector3 PositionAt(float time)
            {
                // Preserve the horizontal direction across the bounce, with speed lost at impact.
                Vector3 incoming = (End - Start) / (FirstLeg + AfterBounceDuration * BounceSpeedFraction);
                incoming.y = 0f;
                Vector3 bounce = Start + incoming * FirstLeg;
                bounce.y = BallFloor;
                if (time <= FirstLeg) return Projectile(Start, bounce, time, FirstLeg);

                Vector3 outgoing = incoming * BounceSpeedFraction;
                float afterBounce = time - FirstLeg;
                float airTime = SecondBounceTime - FirstLeg;
                float inAir = Mathf.Min(afterBounce, airTime);
                Vector3 point = bounce + outgoing * inAir;
                point.y += inAir * (BounceVerticalSpeed - 0.5f * Gravity * inAir);

                if (afterBounce > airTime)
                {
                    float looseTime = afterBounce - airTime;
                    point += outgoing * (0.43f * (1f - Mathf.Exp(-2.2f * looseTime)) / 2.2f);
                    point.y = BallFloor + Mathf.Max(0f, looseTime * (BounceVerticalSpeed * 0.25f - 0.5f * Gravity * looseTime));
                }
                return point;
            }
        }

        private Transform _ball;
        private Transform _ballShadow;
        private ProceduralHumanoid _opponent;
        private ProceduralHumanoid _ambientPlayer;
        private Transform _opponentRacket;
        private Transform _playerRacket;
        private Transform _readyMarker;
        private Renderer _readyMarkerRenderer;
        private MaterialPropertyBlock _markerProps;
        private AudioSource _ballSound;
        private AudioClip _hitSound;
        private AudioClip _bounceSound;
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Phase _phase;
        private Flight _flight;
        private Vector3 _serveStart;
        private Vector3 _serveContact;
        private float _elapsed;
        private float _looseBallTime;
        private float _queuedAim;
        private int _points;
        private int _opponentPoints;
        private int _returns;
        private int _ambientMaxReturns = 5;
        private bool _ready;
        private bool _returnQueued;
        private bool _earlyBufferQueued;
        private bool _preciseReturn;
        private bool _lastShotWasPerfect;
        private bool _opponentSwinging;
        private bool _ambientSwinging;
        private bool _lastPointWon;
        private Vector3 _beginPosition;
        private Quaternion _beginRotation;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private bool IsAmbientMatch => _player == null && _ambientPlayer != null;

        public Vector3 ReturnPosition => _beginPosition;
        public Quaternion ReturnRotation => _beginRotation;
        public int HudRevision { get; private set; }

        public void Configure(
            Transform ball,
            Transform ballShadow,
            ProceduralHumanoid opponent,
            Transform opponentRacket,
            Transform playerRacket,
            Transform readyMarker,
            AudioSource ballSound,
            AudioClip hitSound,
            AudioClip bounceSound,
            ProceduralHumanoid ambientPlayer = null)
        {
            _ball = ball;
            _ballShadow = ballShadow;
            _opponent = opponent;
            _opponentRacket = opponentRacket;
            _playerRacket = playerRacket;
            _readyMarker = readyMarker;
            _readyMarkerRenderer = readyMarker != null ? readyMarker.GetComponentInChildren<Renderer>(true) : null;
            _markerProps = new MaterialPropertyBlock();
            _ballSound = ballSound;
            _hitSound = hitSound;
            _bounceSound = bounceSound;
            _ambientPlayer = ambientPlayer;

            if (_ambientPlayer != null)
            {
                StartAmbientMatch();
            }
            else
            {
                ResetVisuals();
                enabled = false;
            }
        }

        public void Begin(PlayerFootController player)
        {
            _player = player;
            _beginPosition = player.transform.position;
            _beginRotation = player.transform.rotation;
            _points = 0;
            _opponentPoints = 0;
            _lastShotWasPerfect = false;

            if (_ambientPlayer != null)
            {
                _ambientPlayer.SetLookAtPoint(false, Vector3.zero);
                _ambientPlayer.SetTennisLeftHand(null);
                _ambientPlayer.SetTennisRacket(null);
                _ambientPlayer.gameObject.SetActive(false);
            }

            if (_ball != null) _ball.localScale = Vector3.one * 1.85f;
            player.SetSeatedState(true);
            _playerRacket.SetParent(transform, false);
            _playerRacket.gameObject.SetActive(player.Humanoid != null);
            player.Humanoid?.SetTennisRacket(_playerRacket);
            _opponent.gameObject.SetActive(true);
            _opponent.SetTennisRacket(_opponentRacket);

            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(true, distance: 7.4f, pitch: 23f, fov: 60f, lockYaw: transform.eulerAngles.y);
            }

            ResetPoint();
            enabled = true;
        }

        public void End()
        {
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(false);
                _cameraRig = null;
            }

            if (_ball != null) _ball.localScale = Vector3.one;
            if (_readyMarker != null)
            {
                _readyMarker.localScale = Vector3.one;
                _readyMarker.gameObject.SetActive(false);
            }

            if (_player != null)
            {
                _player.Humanoid?.SetLookAtPoint(false, Vector3.zero);
                _player.Humanoid?.SetTennisLeftHand(null);
                _player.Humanoid?.SetTennisRacket(null);
                _player.SetSeatedState(false);
            }
            _player = null;

            if (_ambientPlayer != null)
            {
                StartAmbientMatch();
            }
            else
            {
                _opponent.SetTennisRacket(null);
                ResetVisuals();
                enabled = false;
            }
        }

        private void StartAmbientMatch()
        {
            if (_ball != null) _ball.localScale = Vector3.one;
            _readyMarker.gameObject.SetActive(false);
            _ready = false;
            _earlyBufferQueued = false;
            _ambientPlayer.gameObject.SetActive(true);
            _opponent.gameObject.SetActive(true);
            _playerRacket.SetParent(transform, false);
            _playerRacket.gameObject.SetActive(true);
            _ambientPlayer.SetTennisRacket(_playerRacket);
            _opponent.SetTennisRacket(_opponentRacket);
            ResetAmbientPoint();
            enabled = true;
        }

        private void ResetVisuals()
        {
            SetBallVisible(false);
            if (_opponent != null) _opponent.gameObject.SetActive(false);
            if (_ambientPlayer != null) _ambientPlayer.gameObject.SetActive(false);
            _readyMarker.gameObject.SetActive(false);
            _playerRacket.gameObject.SetActive(false);
            _playerRacket.SetParent(transform, false);
        }

        public string HudText()
        {
            string player = LocalizationService.Tr("sports.tennis.you");
            string opponent = LocalizationService.Tr("sports.tennis.opponent");
            string instructions = _phase switch
            {
                Phase.Serve => "sports.tennis.serve",
                Phase.Serving => "sports.tennis.swing",
                Phase.ToPlayer when _returnQueued && _preciseReturn => "sports.tennis.perfect",
                Phase.ToPlayer when _returnQueued || _earlyBufferQueued => "sports.tennis.buffered",
                Phase.ToPlayer when _ready => "sports.tennis.now",
                Phase.ToPlayer => "sports.tennis.wait",
                Phase.ToOpponent when _lastShotWasPerfect => "sports.tennis.perfect",
                Phase.Point when _lastPointWon => "sports.tennis.point_won",
                Phase.Point => "sports.tennis.point_lost",
                Phase.MatchOver when _lastPointWon => "sports.tennis.won_rematch",
                Phase.MatchOver => "sports.tennis.lost_rematch",
                _ => "sports.tennis.rally"
            };
            return $"{player} {_points}  ·  {_opponentPoints} {opponent}\n{LocalizationService.Tr(instructions)}";
        }

        public string PrimaryButtonLabel()
        {
            return _phase switch
            {
                Phase.Serve => LocalizationService.Tr("sports.btn.serve", "Servis"),
                Phase.MatchOver => LocalizationService.Tr("sports.btn.rematch", "Tekrar"),
                Phase.ToPlayer when _ready => LocalizationService.Tr("sports.btn.hit_now", "VUR!"),
                _ => LocalizationService.Tr("ui.btn.attack", "Vur")
            };
        }

        public bool AcceptsHit => _player != null && (_phase == Phase.Serve || _ready || _phase == Phase.MatchOver);

        public void Hit()
        {
            if (_player == null) return;

            if (_phase == Phase.MatchOver)
            {
                if (_elapsed >= 0.45f)
                {
                    _points = 0;
                    _opponentPoints = 0;
                    _lastShotWasPerfect = false;
                    ResetPoint();
                    GameFeelAudio.PlayUiClick();
                }
                return;
            }

            if ((_phase != Phase.Serve && _phase != Phase.ToPlayer) || _returnQueued) return;

            float playerX = PlayerX;
            float input = InputHub.Instance != null ? InputHub.Instance.MoveInput.x : 0f;
            _queuedAim = Mathf.Abs(input) >= 0.16f ? Mathf.Clamp(input, -1f, 1f) : (playerX > 0f ? -0.45f : 0.45f);
            if (_phase == Phase.Serve)
            {
                _phase = Phase.Serving;
                _elapsed = 0f;
                _lastShotWasPerfect = false;
                _serveStart = _ball.localPosition;
                _serveContact = new Vector3(playerX + 0.46f, 2.08f, PlayerBaseline + 0.40f);
                _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(_serveContact), ServeTime, serving: true);
                HudRevision++;
                return;
            }

            float remaining = Mathf.Max(0f, _flight.Duration - _elapsed);
            if (_ready || remaining <= ContactWindow)
            {
                LockInPlayerReturn(remaining, isDirectTap: true);
                return;
            }

            // Forgiving early-tap input buffer: tapping early queues a clean return instead of losing the point!
            if (remaining <= _flight.Duration + 0.05f && !_earlyBufferQueued)
            {
                _earlyBufferQueued = true;
                HudRevision++;
            }
        }

        private void LockInPlayerReturn(float remaining, bool isDirectTap)
        {
            _returnQueued = true;
            _earlyBufferQueued = false;
            _preciseReturn = isDirectTap && remaining >= 0.06f && remaining <= 0.44f;
            SetReady(false);
            _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(_flight.End), Mathf.Max(0.04f, remaining));
            HudRevision++;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            if (IsAmbientMatch)
            {
                Camera cam = Camera.main;
                if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > AmbientActiveRangeSq)
                {
                    return;
                }
                UpdateAmbientMatch();
                return;
            }

            if (_player == null ||
                (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked))
            {
                return;
            }

            _elapsed += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Serve:
                    if (InputHub.Instance != null && Mathf.Abs(InputHub.Instance.MoveInput.x) > 0.12f)
                    {
                        float serveMoveX = Mathf.Clamp(PlayerX + InputHub.Instance.MoveInput.x * 3.0f * Time.deltaTime, -2.4f, 2.4f);
                        SetPlayerPosition(serveMoveX);
                        Vector3 hand = new Vector3(serveMoveX - 0.25f, 1.12f, PlayerBaseline + 0.33f);
                        _player.Humanoid?.SetTennisLeftHand(transform.TransformPoint(hand));
                        SetBallPosition(hand + Vector3.up * 0.065f);
                    }
                    break;
                case Phase.Serving:
                    UpdateServe();
                    break;
                case Phase.ToOpponent:
                case Phase.ToPlayer:
                    UpdateFlight();
                    break;
                case Phase.Point:
                case Phase.MatchOver:
                    UpdateLooseBall();
                    if (_phase == Phase.Point && _elapsed >= 1.25f) ResetPoint();
                    break;
            }
        }

        // =====================================================================
        // AUTONOMOUS NPC vs NPC AMBIENT RALLY (when player is not playing!)
        // =====================================================================
        private void UpdateAmbientMatch()
        {
            _elapsed += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Serve:
                    if (_elapsed >= 0.85f)
                    {
                        float sx = _ambientPlayer.transform.localPosition.x;
                        _phase = Phase.Serving;
                        _elapsed = 0f;
                        _serveStart = _ball.localPosition;
                        _serveContact = new Vector3(sx + 0.46f, 2.08f, PlayerBaseline + 0.40f);
                        _ambientPlayer.PlayTennisSwing(transform.TransformPoint(_serveContact), ServeTime, serving: true);
                    }
                    break;

                case Phase.Serving:
                {
                    float time = Mathf.Min(_elapsed, ServeTime);
                    if (time <= ServeRelease)
                    {
                        float raise = Mathf.SmoothStep(0f, 1f, time / ServeRelease) * 0.21f;
                        SetBallPosition(_serveStart + Vector3.up * raise);
                        _ambientPlayer.SetTennisLeftHand(_ball.position - transform.up * 0.065f);
                    }
                    else
                    {
                        SetBallPosition(Projectile(_serveStart + Vector3.up * 0.21f, _serveContact, time - ServeRelease, ServeTime - ServeRelease));
                        _ambientPlayer.SetTennisLeftHand(null);
                    }
                    if (time < ServeTime) return;
                    _ambientPlayer.PlayTennisSwing(_ball.position, 0f, serving: true);
                    StartFlight(true, _serveContact, Random.Range(-3.1f, 3.1f), Random.Range(1.02f, 1.18f), volumeScale: 0.58f);
                    _returns = 1;
                    break;
                }

                case Phase.ToOpponent:
                case Phase.ToPlayer:
                {
                    float time = Mathf.Min(_elapsed, _flight.Duration);
                    float remaining = _flight.Duration - time;
                    SetBallPosition(_flight.PositionAt(time));
                    if (_elapsed - Time.deltaTime < _flight.FirstLeg && _elapsed >= _flight.FirstLeg)
                    {
                        PlayCourtSound(_bounceSound, 0.26f);
                    }

                    bool toSouthNpc = _phase == Phase.ToPlayer;
                    bool shouldMissThisBall = _returns >= _ambientMaxReturns;
                    float targetX = _flight.End.x + (toSouthNpc ? -ContactSide : ContactSide);
                    float speed = shouldMissThisBall ? 2.15f : 4.25f;

                    if (toSouthNpc)
                    {
                        MoveNpc(_opponent, 0f, _elapsed > 0.24f ? 2.1f : 0f);
                        float gap = MoveNpc(_ambientPlayer, targetX, _elapsed >= 0.16f ? speed : 0f);
                        if (!_ambientSwinging && !shouldMissThisBall && remaining <= 0.19f && gap <= remaining * speed + 0.28f)
                        {
                            _ambientSwinging = true;
                            _ambientPlayer.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
                        }
                        if (remaining > 0f) return;

                        if (shouldMissThisBall || gap > 0.35f)
                        {
                            FinishAmbientPoint();
                            return;
                        }
                        _ambientPlayer.PlayTennisSwing(_ball.position, 0f);
                        _returns++;
                        StartFlight(true, _flight.End, Random.Range(-3.45f, 3.45f), Random.Range(0.96f, 1.16f), volumeScale: 0.58f);
                    }
                    else
                    {
                        MoveNpc(_ambientPlayer, 0f, _elapsed > 0.24f ? 2.1f : 0f);
                        float gap = MoveNpc(_opponent, targetX, _elapsed >= 0.16f ? speed : 0f);
                        if (!_opponentSwinging && !shouldMissThisBall && remaining <= 0.19f && gap <= remaining * speed + 0.28f)
                        {
                            _opponentSwinging = true;
                            _opponent.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
                        }
                        if (remaining > 0f) return;

                        if (shouldMissThisBall || gap > 0.35f)
                        {
                            FinishAmbientPoint();
                            return;
                        }
                        _opponent.PlayTennisSwing(_ball.position, 0f);
                        _returns++;
                        StartFlight(false, _flight.End, Random.Range(-3.45f, 3.45f), Random.Range(0.96f, 1.16f), volumeScale: 0.58f);
                    }
                    break;
                }

                case Phase.Point:
                    UpdateLooseBall(volumeScale: 0.55f);
                    if (_elapsed >= 1.65f) ResetAmbientPoint();
                    break;
            }
        }

        private void FinishAmbientPoint()
        {
            _phase = Phase.Point;
            _looseBallTime = Mathf.Min(_elapsed, _flight.Duration);
            _elapsed = 0f;
            _ambientPlayer.SetLocomotion(0f, true, 0f);
            _opponent.SetLocomotion(0f, true, 0f);
        }

        private void ResetAmbientPoint()
        {
            _phase = Phase.Serve;
            _returns = 0;
            _ambientMaxReturns = Random.Range(4, 8);
            _elapsed = 0f;
            _opponentSwinging = false;
            _ambientSwinging = false;

            float serveX = Random.Range(-1.1f, 1.1f);
            _opponent.transform.SetLocalPositionAndRotation(new Vector3(-serveX * 0.5f, CourtSurface, -PlayerBaseline), Quaternion.Euler(0f, 180f, 0f));
            _opponent.SetLocomotion(0f, true, 0f);
            _opponent.ClearTennisSwing();

            _ambientPlayer.transform.SetLocalPositionAndRotation(new Vector3(serveX, CourtSurface, PlayerBaseline), Quaternion.identity);
            _ambientPlayer.SetLocomotion(0f, true, 0f);
            _ambientPlayer.ClearTennisSwing();

            Vector3 hand = new Vector3(serveX - 0.25f, 1.12f, PlayerBaseline + 0.33f);
            _ambientPlayer.SetTennisLeftHand(transform.TransformPoint(hand));
            SetBallPosition(hand + Vector3.up * 0.065f);
            SetBallVisible(true);
        }

        private void UpdateServe()
        {
            float time = Mathf.Min(_elapsed, ServeTime);
            if (time <= ServeRelease)
            {
                float raise = Mathf.SmoothStep(0f, 1f, time / ServeRelease) * 0.21f;
                SetBallPosition(_serveStart + Vector3.up * raise);
                _player.Humanoid?.SetTennisLeftHand(_ball.position - transform.up * 0.065f);
            }
            else
            {
                SetBallPosition(Projectile(_serveStart + Vector3.up * 0.21f, _serveContact, time - ServeRelease, ServeTime - ServeRelease));
                _player.Humanoid?.SetTennisLeftHand(null);
            }

            if (time < ServeTime) return;
            _player.Humanoid?.PlayTennisSwing(_ball.position, 0f, serving: true);
            StartPlayerShot(_serveContact, precise: false);
        }

        private void UpdateFlight()
        {
            float time = Mathf.Min(_elapsed, _flight.Duration);
            float remaining = _flight.Duration - time;
            SetBallPosition(_flight.PositionAt(time));
            if (_elapsed - Time.deltaTime < _flight.FirstLeg && _elapsed >= _flight.FirstLeg)
            {
                _ballSound.PlayOneShot(_bounceSound, 0.48f);
            }

            bool toPlayer = _phase == Phase.ToPlayer;
            float targetX = _flight.End.x + (toPlayer ? -ContactSide : ContactSide);
            if (toPlayer)
            {
                MoveNpc(_opponent, 0f, _elapsed > 0.2f ? 2.25f : 0f);
                float gap = MovePlayer(targetX, 7.2f);

                // Allow aim refinement with joystick anytime before impact!
                if (InputHub.Instance != null && Mathf.Abs(InputHub.Instance.MoveInput.x) >= 0.16f)
                {
                    _queuedAim = Mathf.Clamp(InputHub.Instance.MoveInput.x, -1f, 1f);
                }

                // Trigger buffered early tap right in the clean contact zone
                if (_earlyBufferQueued && !_returnQueued && remaining <= 0.36f && gap < 0.70f)
                {
                    LockInPlayerReturn(remaining, isDirectTap: false);
                }

                bool inWindow = remaining <= ContactWindow && remaining >= 0.02f;
                SetReady(!_returnQueued && inWindow && gap < 0.75f);
                UpdateTimingRingVisual(remaining, inWindow);

                if (remaining > 0f) return;

                if (!_returnQueued || gap > 0.65f)
                {
                    AwardPoint(playerWon: false);
                    return;
                }

                _player.Humanoid?.PlayTennisSwing(_ball.position, 0f);
                if (_preciseReturn)
                {
                    GameFeelAudio.SpawnBurst(_ball.position, new Color(1.0f, 0.85f, 0.24f), count: 8, spread: 0.35f);
                }
                StartPlayerShot(_flight.End, _preciseReturn);
                return;
            }

            if (_readyMarker != null && _readyMarker.gameObject.activeSelf)
            {
                _readyMarker.gameObject.SetActive(false);
            }

            MovePlayer(0f, _elapsed > 0.28f ? 2.65f : 0f);
            float reactionTime = Mathf.Min(0.62f, 0.26f + _returns * 0.065f);
            float opponentSpeed = _lastShotWasPerfect ? 3.35f : 3.75f;
            float opponentGap = MoveNpc(_opponent, targetX, _elapsed >= reactionTime ? opponentSpeed : 0f);
            if (!_opponentSwinging && remaining <= 0.20f && opponentGap <= remaining * opponentSpeed + 0.22f)
            {
                _opponentSwinging = true;
                _opponent.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
            }
            if (remaining > 0f) return;

            if (opponentGap > 0.24f)
            {
                AwardPoint(playerWon: true);
                return;
            }

            _opponent.PlayTennisSwing(_ball.position, 0f);
            float returnX = Random.Range(-3.15f, 3.15f);
            float returnFirstLeg = Mathf.Max(1.06f, 1.32f - _returns * 0.02f);
            StartFlight(false, _flight.End, returnX, returnFirstLeg);
        }

        private void UpdateTimingRingVisual(float remaining, bool inWindow)
        {
            if (_readyMarker == null) return;
            if (!_readyMarker.gameObject.activeSelf) _readyMarker.gameObject.SetActive(true);

            _readyMarker.localPosition = new Vector3(_flight.End.x, 0.016f, PlayerBaseline + ContactForward);
            float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.25f, _flight.Duration));
            float ringScale = _returnQueued
                ? 1.08f
                : Mathf.Lerp(1.85f, 0.92f, progress);
            _readyMarker.localScale = new Vector3(ringScale, 1f, ringScale);

            if (_readyMarkerRenderer != null && _markerProps != null)
            {
                Color c;
                Color em;
                if (_returnQueued)
                {
                    c = new Color(1.0f, 0.84f, 0.22f, 1f);
                    em = new Color(0.65f, 0.50f, 0.08f, 1f);
                }
                else if (inWindow)
                {
                    c = new Color(0.22f, 0.96f, 0.58f, 1f);
                    em = new Color(0.12f, 0.65f, 0.36f, 1f);
                }
                else
                {
                    c = new Color(0.96f, 0.78f, 0.26f, 1f);
                    em = new Color(0.32f, 0.24f, 0.05f, 1f);
                }
                _markerProps.SetColor(BaseColorId, c);
                _markerProps.SetColor(EmissionColorId, em);
                _readyMarkerRenderer.SetPropertyBlock(_markerProps);
            }
        }

        private void StartPlayerShot(Vector3 contact, bool precise)
        {
            _lastShotWasPerfect = precise;
            float targetX = _queuedAim * (precise ? 3.92f : 3.15f);
            StartFlight(true, contact, targetX, precise ? 0.84f : 1.18f);
            _returns++;
        }

        private void PlayCourtSound(AudioClip clip, float volume)
        {
            if (_ballSound == null || clip == null) return;
            if (IsAmbientMatch)
            {
                Camera cam = Camera.main;
                if (cam == null || (cam.transform.position - transform.position).sqrMagnitude > 22f * 22f)
                {
                    return;
                }
            }
            _ballSound.PlayOneShot(clip, volume);
        }

        private void StartFlight(bool toOpponent, Vector3 start, float targetX, float firstLeg, float volumeScale = 1f)
        {
            float direction = toOpponent ? 1f : -1f;
            _flight = new Flight
            {
                Start = start,
                End = new Vector3(targetX, ContactHeight, direction * (-PlayerBaseline - ContactForward)),
                FirstLeg = firstLeg
            };
            _phase = toOpponent ? Phase.ToOpponent : Phase.ToPlayer;
            PlayCourtSound(_hitSound, 0.56f * volumeScale);
            _returnQueued = false;
            _earlyBufferQueued = false;
            _opponentSwinging = false;
            _ambientSwinging = false;
            _elapsed = 0f;
            SetReady(false);
            HudRevision++;
        }

        private void AwardPoint(bool playerWon)
        {
            if (playerWon) _points++;
            else _opponentPoints++;

            _lastPointWon = playerWon;
            _lastShotWasPerfect = false;
            _earlyBufferQueued = false;
            bool matchOver = _points >= PointsToWin || _opponentPoints >= PointsToWin;
            _phase = matchOver ? Phase.MatchOver : Phase.Point;
            _looseBallTime = Mathf.Min(_elapsed, _flight.Duration);
            _elapsed = 0f;
            SetReady(false);
            if (_readyMarker != null) _readyMarker.gameObject.SetActive(false);
            HudRevision++;
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            _opponent.SetLocomotion(0f, true, 0f);

            if (playerWon)
            {
                _player.Humanoid?.PlaySportAnim(SportAnimKind.VictoryJump, matchOver ? 0.85f : 0.60f);
                GameFeelAudio.SpawnBurst(_player.transform.position + Vector3.up * 1.4f, new Color(0.24f, 0.92f, 0.64f), count: matchOver ? 16 : 8, spread: 0.6f);
                if (matchOver)
                {
                    GameFeelAudio.PlayMilestoneFanfare();
                    SaveManager.Instance?.AddMoney(75);
                    NeighborhoodReputation.AddReputation(25, LocalizationService.Tr("sports.tennis.win_toast", "Tenis Maçı Kazanıldı! (+75 ₺)"));
                }
                else
                {
                    GameFeelAudio.PlayCoinChime();
                }
            }
            else
            {
                _opponent.PlaySportAnim(SportAnimKind.VictoryJump, 0.58f);
            }

            if (matchOver)
            {
                SportMatchEvents.NotifyMatchFinished("sport_tennis", playerWon);
            }
        }

        private void UpdateLooseBall(float volumeScale = 1f)
        {
            float lastTime = _looseBallTime;
            if (lastTime >= _flight.SecondBounceTime + 1.2f) return;
            _looseBallTime += Time.deltaTime;
            SetBallPosition(_flight.PositionAt(_looseBallTime));
            if (lastTime < _flight.SecondBounceTime && _looseBallTime >= _flight.SecondBounceTime)
            {
                PlayCourtSound(_bounceSound, 0.28f * volumeScale);
            }
        }

        private void ResetPoint()
        {
            _phase = Phase.Serve;
            _returns = 0;
            _elapsed = 0f;
            _returnQueued = false;
            _earlyBufferQueued = false;
            _lastShotWasPerfect = false;
            SetReady(false);
            if (_readyMarker != null) _readyMarker.gameObject.SetActive(false);
            HudRevision++;
            _opponent.transform.SetLocalPositionAndRotation(new Vector3(0f, CourtSurface, -PlayerBaseline), Quaternion.Euler(0f, 180f, 0f));
            _opponent.SetLocomotion(0f, true, 0f);
            _opponent.ClearTennisSwing();
            SetPlayerPosition(0f);
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            _player.Humanoid?.ClearTennisSwing();
            Vector3 hand = new Vector3(-0.25f, 1.12f, PlayerBaseline + 0.33f);
            _player.Humanoid?.SetTennisLeftHand(transform.TransformPoint(hand));
            SetBallPosition(hand + Vector3.up * 0.065f);
            SetBallVisible(true);
        }

        private void SetReady(bool ready)
        {
            if (_ready == ready) return;
            _ready = ready;
            HudRevision++;
        }

        private void SetBallVisible(bool visible)
        {
            _ball.gameObject.SetActive(visible);
            _ballShadow.gameObject.SetActive(visible);
        }

        private void SetBallPosition(Vector3 position)
        {
            _ball.localPosition = position;
            _ballShadow.localPosition = new Vector3(position.x, 0.006f, position.z);
            float spread = Mathf.Lerp(0.72f, 1.25f, Mathf.InverseLerp(BallFloor, 2f, position.y));
            if (_player != null) spread *= 1.45f;
            _ballShadow.localScale = new Vector3(spread, 1f, spread);
            if (_player != null)
            {
                _player.Humanoid?.SetLookAtPoint(true, _ball.position);
            }
            else if (_ambientPlayer != null && _ambientPlayer.gameObject.activeSelf)
            {
                _ambientPlayer.SetLookAtPoint(true, _ball.position);
            }
            if (_opponent != null)
            {
                _opponent.SetLookAtPoint(true, _ball.position);
            }
        }

        private float PlayerX => transform.InverseTransformPoint(_player.transform.position).x;

        private float MovePlayer(float target, float speed)
        {
            float x = PlayerX;
            float next = Mathf.MoveTowards(x, target, speed * Time.deltaTime);
            if (next != x) SetPlayerPosition(next);
            SetLateralGait(_player.Humanoid, next - x);
            return Mathf.Abs(target - next);
        }

        private float MoveNpc(ProceduralHumanoid npc, float target, float speed)
        {
            Vector3 position = npc.transform.localPosition;
            float next = Mathf.MoveTowards(position.x, target, speed * Time.deltaTime);
            SetLateralGait(npc, next - position.x);
            position.x = next;
            npc.transform.localPosition = position;
            return Mathf.Abs(target - next);
        }

        private void SetLateralGait(ProceduralHumanoid humanoid, float traveled)
        {
            Vector3 travelDirection = traveled == 0f ? Vector3.zero : transform.right * Mathf.Sign(traveled);
            humanoid?.SetLocomotion(Mathf.Abs(traveled) / Time.deltaTime, true, 0f, travelDirection);
        }

        private void SetPlayerPosition(float x)
        {
            _player.TeleportTo(transform.TransformPoint(new Vector3(x, CourtSurface, PlayerBaseline)), transform.rotation);
        }

        private static Vector3 Projectile(Vector3 start, Vector3 end, float time, float duration)
        {
            Vector3 point = Vector3.LerpUnclamped(start, end, time / duration);
            point.y += 0.5f * Gravity * time * (duration - time);
            return point;
        }
    }
}
