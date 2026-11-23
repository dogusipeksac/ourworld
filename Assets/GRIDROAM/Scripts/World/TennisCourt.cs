using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
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
        private const float ContactHeight = 0.93f;
        private const float ContactSide = 0.65f;
        private const float ContactForward = 0.28f;
        private const float ContactWindow = 0.33f;
        private const float ServeTime = 0.63f;
        private const float ServeRelease = 0.12f;
        private const int PointsToWin = 3;

        private enum Phase { Serve, Serving, ToOpponent, ToPlayer, Point, MatchOver }

        private struct Flight
        {
            private const float BounceSpeedFraction = 0.68f;
            private const float AfterBounceDuration = 0.36f;

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
        private Transform _opponentRacket;
        private Transform _playerRacket;
        private Transform _readyMarker;
        private AudioSource _ballSound;
        private AudioClip _hitSound;
        private AudioClip _bounceSound;
        private PlayerFootController _player;
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
        private bool _ready;
        private bool _returnQueued;
        private bool _preciseReturn;
        private bool _opponentSwinging;
        private bool _lastPointWon;
        private Vector3 _beginPosition;
        private Quaternion _beginRotation;

        public Vector3 ReturnPosition => _beginPosition;
        public Quaternion ReturnRotation => _beginRotation;
        public int HudRevision { get; private set; }

        public void Configure(Transform ball, Transform ballShadow, ProceduralHumanoid opponent, Transform opponentRacket,
            Transform playerRacket, Transform readyMarker, AudioSource ballSound, AudioClip hitSound, AudioClip bounceSound)
        {
            _ball = ball;
            _ballShadow = ballShadow;
            _opponent = opponent;
            _opponentRacket = opponentRacket;
            _playerRacket = playerRacket;
            _readyMarker = readyMarker;
            _ballSound = ballSound;
            _hitSound = hitSound;
            _bounceSound = bounceSound;
            ResetVisuals();
            enabled = false;
        }

        public void Begin(PlayerFootController player)
        {
            _player = player;
            _beginPosition = player.transform.position;
            _beginRotation = player.transform.rotation;
            _points = 0;
            _opponentPoints = 0;

            player.SetSeatedState(true);
            _playerRacket.gameObject.SetActive(player.Humanoid != null);
            player.Humanoid?.SetTennisRacket(_playerRacket);
            _opponent.gameObject.SetActive(true);
            _opponent.SetTennisRacket(_opponentRacket);
            ResetPoint();
            enabled = true;
        }

        public void End()
        {
            if (_player != null)
            {
                _player.Humanoid?.SetLookAtPoint(false, Vector3.zero);
                _player.Humanoid?.SetTennisRacket(null);
                _player.SetSeatedState(false);
            }
            _player = null;
            _opponent.SetTennisRacket(null);
            ResetVisuals();
            enabled = false;
        }

        private void ResetVisuals()
        {
            SetBallVisible(false);
            _opponent.gameObject.SetActive(false);
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
                Phase.ToPlayer when _ready => "sports.tennis.now",
                Phase.ToPlayer when _returnQueued => "sports.tennis.swing",
                Phase.ToPlayer => "sports.tennis.wait",
                Phase.Point when _lastPointWon => "sports.tennis.point_won",
                Phase.Point => "sports.tennis.point_lost",
                Phase.MatchOver when _lastPointWon => "sports.tennis.won",
                Phase.MatchOver => "sports.tennis.lost",
                _ => "sports.tennis.rally"
            };
            return $"{player} {_points}  ·  {_opponentPoints} {opponent}\n{LocalizationService.Tr(instructions)}";
        }

        public bool AcceptsHit => _phase == Phase.Serve || _ready;

        public void Hit()
        {
            if ((_phase != Phase.Serve && _phase != Phase.ToPlayer) || _returnQueued) return;

            float playerX = PlayerX;
            float input = InputHub.Instance != null ? InputHub.Instance.MoveInput.x : 0f;
            _queuedAim = Mathf.Abs(input) >= 0.2f ? Mathf.Clamp(input, -1f, 1f) : (playerX > 0f ? -0.35f : 0.35f);
            if (_phase == Phase.Serve)
            {
                _phase = Phase.Serving;
                _elapsed = 0f;
                _serveStart = _ball.localPosition;
                _serveContact = new Vector3(playerX + 0.48f, 2.10f, PlayerBaseline + 0.40f);
                _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(_serveContact), ServeTime, serving: true);
                HudRevision++;
                return;
            }

            if (!_ready)
            {
                Vector3 drySwing = transform.TransformPoint(new Vector3(playerX + ContactSide, ContactHeight, PlayerBaseline + ContactForward));
                _player.Humanoid?.PlayTennisSwing(drySwing, 0.13f);
                if (_elapsed > _flight.FirstLeg) AwardPoint(playerWon: false);
                return;
            }

            _returnQueued = true;
            _preciseReturn = Mathf.Abs(_elapsed - _flight.Duration + 0.12f) < 0.08f;
            SetReady(false);
            _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(_flight.End), _flight.Duration - _elapsed);
        }

        private void Update()
        {
            if (_player == null || Time.deltaTime <= 0f ||
                (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked))
            {
                return;
            }

            _elapsed += Time.deltaTime;
            switch (_phase)
            {
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
                    if (_phase == Phase.Point && _elapsed >= 1.35f) ResetPoint();
                    break;
            }
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
                MoveOpponent(0f, _elapsed > 0.2f ? 2.25f : 0f);
                float gap = MovePlayer(targetX, 5.75f);
                bool inWindow = remaining <= ContactWindow && remaining >= 0.055f;
                SetReady(!_returnQueued && inWindow && gap < 0.30f);
                _readyMarker.localPosition = new Vector3(PlayerX, 0.013f, PlayerBaseline);
                if (remaining > 0f) return;

                if (!_returnQueued || gap > 0.22f)
                {
                    AwardPoint(playerWon: false);
                    return;
                }

                _player.Humanoid?.PlayTennisSwing(_ball.position, 0f);
                StartPlayerShot(_flight.End, _preciseReturn);
                return;
            }

            MovePlayer(0f, _elapsed > 0.29f ? 2.25f : 0f);
            float reactionTime = Mathf.Min(0.58f, 0.25f + _returns * 0.055f);
            float opponentGap = MoveOpponent(targetX, _elapsed >= reactionTime ? 3.9f : 0f);
            if (!_opponentSwinging && remaining <= 0.19f && opponentGap <= remaining * 3.9f + 0.20f)
            {
                _opponentSwinging = true;
                _opponent.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
            }
            if (remaining > 0f) return;

            if (opponentGap > 0.22f)
            {
                AwardPoint(playerWon: true);
                return;
            }

            _opponent.PlayTennisSwing(_ball.position, 0f);
            StartFlight(false, _flight.End, Random.Range(-3.65f, 3.65f), Mathf.Max(0.93f, 1.18f - _returns * 0.025f));
        }

        private void StartPlayerShot(Vector3 contact, bool precise)
        {
            float targetX = _queuedAim * (precise ? 3.95f : 3.3f);
            StartFlight(true, contact, targetX, precise ? 0.87f : 1.16f);
            _returns++;
        }

        private void StartFlight(bool toOpponent, Vector3 start, float targetX, float firstLeg)
        {
            float direction = toOpponent ? 1f : -1f;
            _flight = new Flight
            {
                Start = start,
                End = new Vector3(targetX, ContactHeight, direction * (-PlayerBaseline - ContactForward)),
                FirstLeg = firstLeg
            };
            _phase = toOpponent ? Phase.ToOpponent : Phase.ToPlayer;
            _ballSound.PlayOneShot(_hitSound, 0.56f);
            _returnQueued = false;
            _opponentSwinging = false;
            _elapsed = 0f;
            SetReady(false);
            HudRevision++;
        }

        private void AwardPoint(bool playerWon)
        {
            if (playerWon) _points++;
            else _opponentPoints++;

            _lastPointWon = playerWon;
            _phase = _points >= PointsToWin || _opponentPoints >= PointsToWin ? Phase.MatchOver : Phase.Point;
            _looseBallTime = Mathf.Min(_elapsed, _flight.Duration);
            _elapsed = 0f;
            SetReady(false);
            HudRevision++;
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            _opponent.SetLocomotion(0f, true, 0f);
        }

        private void UpdateLooseBall()
        {
            float lastTime = _looseBallTime;
            if (lastTime >= _flight.SecondBounceTime + 1.2f) return;
            _looseBallTime += Time.deltaTime;
            SetBallPosition(_flight.PositionAt(_looseBallTime));
            if (lastTime < _flight.SecondBounceTime && _looseBallTime >= _flight.SecondBounceTime)
            {
                _ballSound.PlayOneShot(_bounceSound, 0.28f);
            }
        }

        private void ResetPoint()
        {
            _phase = Phase.Serve;
            _returns = 0;
            _elapsed = 0f;
            _returnQueued = false;
            SetReady(false);
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
            _readyMarker.gameObject.SetActive(ready);
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
            float spread = Mathf.Lerp(0.72f, 1.18f, Mathf.InverseLerp(BallFloor, 2f, position.y));
            _ballShadow.localScale = new Vector3(spread, 1f, spread);
            _player.Humanoid?.SetLookAtPoint(true, _ball.position);
            _opponent.SetLookAtPoint(true, _ball.position);
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

        private float MoveOpponent(float target, float speed)
        {
            Vector3 position = _opponent.transform.localPosition;
            float next = Mathf.MoveTowards(position.x, target, speed * Time.deltaTime);
            SetLateralGait(_opponent, next - position.x);
            position.x = next;
            _opponent.transform.localPosition = position;
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
