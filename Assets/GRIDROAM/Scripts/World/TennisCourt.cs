using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Realistic 1v1 Clay Court Tennis Match (Gürsu Sk. Kortu):
    /// - 2-Step Hold-to-Charge Serve with Golden Sweet-Spot (165-198 km/s ACE Serve) & Net Fault overcharge risk
    /// - Full Shot Variety via 2-button mobile controls:
    ///     Primary (Tap/Hold): Angled Flat / Heavy Topspin Drive with golden comet trail
    ///     Secondary Tap: Feather-soft Drop Shot (Kısa Top) behind the net
    ///     Secondary Hold: High tactical Lob over a net-rushing opponent
    /// - Full 2D Court Movement (baseline + net rush) & mid-air Net Volley winners!
    /// - Glowing 3D Opponent Court Aim Reticle steered live by joystick
    /// - Authentic Tennis Scoring (0 - 15 - 30 - 40 / Berabere / Avantaj) + 3 League Tiers + Best Serve Record
    /// </summary>
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
        private const float AmbientActiveRangeSq = 78f * 78f;

        private enum Phase { Serve, Serving, ToOpponent, ToPlayer, Point, MatchOver }
        private enum ShotKind { Flat, Topspin, DropShot, Lob, Volley, GoldenServe }

        private struct Flight
        {
            private const float BounceSpeedFraction = 0.64f;
            private const float AfterBounceDuration = 0.54f;

            public Vector3 Start, End;
            public float FirstLeg;
            public float ExtraArc;
            public float Duration => FirstLeg + AfterBounceDuration;

            private float BounceVerticalSpeed => (End.y - BallFloor) / AfterBounceDuration +
                0.5f * Gravity * AfterBounceDuration;
            public float SecondBounceTime => FirstLeg + 2f * BounceVerticalSpeed / Gravity;

            public Vector3 PositionAt(float time)
            {
                Vector3 incoming = (End - Start) / (FirstLeg + AfterBounceDuration * BounceSpeedFraction);
                incoming.y = 0f;
                Vector3 bounce = Start + incoming * FirstLeg;
                bounce.y = BallFloor;
                if (time <= FirstLeg)
                {
                    Vector3 p = Projectile(Start, bounce, time, FirstLeg);
                    if (ExtraArc > 0.01f)
                    {
                        float u = Mathf.Clamp01(time / Mathf.Max(0.05f, FirstLeg));
                        p.y += 4f * ExtraArc * u * (1f - u);
                    }
                    return p;
                }

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

        // Pro Tennis visual helpers (runtime trail + glowing opponent court aim target)
        private TrailRenderer _ballTrail;
        private Material _ballTrailMat;
        private Transform _aimReticle;
        private Material _aimReticleMat;
        private Material _aimReticleCoreMat;

        private Phase _phase;
        private Flight _flight;
        private Vector3 _serveStart;
        private Vector3 _serveContact;
        private float _elapsed;
        private float _looseBallTime;
        private float _queuedAim;
        private float _queuedDepthAim;
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

        // Pro Tennis mechanics state
        private int _leagueTier;
        private float _playerZ = PlayerBaseline;
        private float _opponentZ = -PlayerBaseline;
        private float _stamina = 100f;
        private bool _isChargingServe;
        private float _serveCharge;
        private float _activeServeCharge;
        private bool _isChargingSecondary;
        private float _secondaryCharge;
        private ShotKind _queuedShotKind = ShotKind.Flat;
        private ShotKind _lastShotKind = ShotKind.Flat;
        private float _lastShotKmh;
        private int _acesCount;
        private int _winnersCount;
        private string _calloutText;
        private Color _calloutColor = new Color(1.0f, 0.85f, 0.22f);
        private float _calloutTimer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private bool IsAmbientMatch => _player == null && _ambientPlayer != null;

        public Vector3 ReturnPosition => _beginPosition;
        public Quaternion ReturnRotation => _beginRotation;
        public int HudRevision { get; private set; }
        public bool HasSecondaryAction => _player != null && _phase != Phase.MatchOver && _phase != Phase.Point && _phase != Phase.Serve && _phase != Phase.Serving;
        public bool AcceptsHit => _player != null && (_phase == Phase.Serve || _ready || _phase == Phase.MatchOver || _phase == Phase.ToPlayer);

        private void OnDestroy()
        {
            if (_ballTrailMat != null) Destroy(_ballTrailMat);
            if (_aimReticleMat != null) Destroy(_aimReticleMat);
            if (_aimReticleCoreMat != null) Destroy(_aimReticleCoreMat);
        }

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

        private void EnsureVisualHelpers()
        {
            if (_ball != null && _ballTrail == null)
            {
                _ballTrail = _ball.gameObject.GetComponent<TrailRenderer>();
                if (_ballTrail == null) _ballTrail = _ball.gameObject.AddComponent<TrailRenderer>();
                _ballTrail.time = 0.28f;
                _ballTrail.minVertexDistance = 0.08f;
                _ballTrail.startWidth = 0.14f;
                _ballTrail.endWidth = 0.02f;
                _ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ballTrail.receiveShadows = false;
                Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (sh != null)
                {
                    _ballTrailMat = new Material(sh);
                    _ballTrail.sharedMaterial = _ballTrailMat;
                }
                SetTrailColor(new Color(0.95f, 0.96f, 0.30f, 0.85f));
                _ballTrail.emitting = false;
            }

            if (_aimReticle == null)
            {
                GameObject aimRoot = new GameObject("Tennis_OpponentAimReticle");
                aimRoot.transform.SetParent(transform, false);

                GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outer.name = "OuterTarget";
                Destroy(outer.GetComponent<Collider>());
                outer.transform.SetParent(aimRoot.transform, false);
                outer.transform.localScale = new Vector3(1.45f, 0.022f, 1.45f);

                GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                inner.name = "InnerBullseye";
                Destroy(inner.GetComponent<Collider>());
                inner.transform.SetParent(aimRoot.transform, false);
                inner.transform.localPosition = new Vector3(0f, 0.018f, 0f);
                inner.transform.localScale = new Vector3(0.62f, 0.022f, 0.62f);

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (unlit != null)
                {
                    _aimReticleMat = new Material(unlit);
                    _aimReticleMat.color = new Color(0.98f, 0.24f, 0.12f, 1f);
                    if (_aimReticleMat.HasProperty(BaseColorId)) _aimReticleMat.SetColor(BaseColorId, new Color(0.98f, 0.24f, 0.12f, 1f));
                    MeshRenderer mrOut = outer.GetComponent<MeshRenderer>();
                    if (mrOut != null) { mrOut.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrOut.sharedMaterial = _aimReticleMat; }

                    _aimReticleCoreMat = new Material(unlit);
                    _aimReticleCoreMat.color = new Color(1.0f, 0.88f, 0.20f, 1f);
                    if (_aimReticleCoreMat.HasProperty(BaseColorId)) _aimReticleCoreMat.SetColor(BaseColorId, new Color(1.0f, 0.88f, 0.20f, 1f));
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
                0 => "Acemi Lig",
                1 => "Mahalle Ligi",
                _ => "Şampiyonlar Ligi"
            };
        }

        private void SetCallout(string text, Color color, float duration)
        {
            _calloutText = text;
            _calloutColor = color;
            _calloutTimer = duration;
            HudRevision++;
        }

        public void Begin(PlayerFootController player)
        {
            EnsureVisualHelpers();
            _player = player;
            _beginPosition = player.transform.position;
            _beginRotation = player.transform.rotation;
            _points = 0;
            _opponentPoints = 0;
            _acesCount = 0;
            _winnersCount = 0;
            _stamina = 100f;
            _lastShotWasPerfect = false;
            _isChargingServe = false;
            _isChargingSecondary = false;
            _leagueTier = SaveManager.Instance != null ? Mathf.Clamp(SaveManager.Instance.Data.tennisLeagueTier, 0, 2) : 0;

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
                _cameraRig.SetSportCameraOverride(true, distance: 7.9f, pitch: 25f, fov: 61f, lockYaw: transform.eulerAngles.y);
            }

            ResetPoint();
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
            SetCallout($"{GetLeagueLabel()} · Tenis Maçı Başladı!", new Color(0.28f, 0.94f, 0.80f), 1.6f);
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
            if (_ballTrail != null) _ballTrail.emitting = false;
            if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);
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
            _isChargingServe = false;
            _isChargingSecondary = false;

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
            if (_ballTrail != null) _ballTrail.emitting = false;
            if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);
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
            if (_readyMarker != null) _readyMarker.gameObject.SetActive(false);
            if (_aimReticle != null) _aimReticle.gameObject.SetActive(false);
            _playerRacket.gameObject.SetActive(false);
            _playerRacket.SetParent(transform, false);
        }

        private string FormatTennisScore()
        {
            if (_points >= 3 && _opponentPoints >= 3)
            {
                if (_points == _opponentPoints) return "40 - 40 (Berabere)";
                return _points > _opponentPoints ? "AV - 40 (Avantaj Sen)" : "40 - AV (Avantaj Rakip)";
            }
            string[] labels = { "0", "15", "30", "40", "OYUN" };
            string pStr = labels[Mathf.Clamp(_points, 0, 4)];
            string oStr = labels[Mathf.Clamp(_opponentPoints, 0, 4)];
            return $"{pStr} - {oStr}";
        }

        public string HudText()
        {
            string title = LocalizationService.Tr("sports.tennis.title", "Tenis Kortu");
            string rallyInfo = _returns > 0 ? $" · Ralli {_returns}" : string.Empty;
            string status;

            if (_phase == Phase.MatchOver)
            {
                status = _lastPointWon
                    ? $"OYUN SENİN! ({_acesCount} Ace · {_winnersCount} Winner) — Tekrar için Vur"
                    : "Maç Bitti — Rövanş için Vur!";
            }
            else if (_phase == Phase.Point)
            {
                status = _lastPointWon ? "SAYI SENİN! Yeni servis hazırlanıyor..." : "Rakip sayı aldı · Odaklan!";
            }
            else if (_isChargingServe)
            {
                status = "Servis Gücünü Ayarla · Altın bölgede bırak -> 185 km/s ACE!";
            }
            else if (_phase == Phase.Serve)
            {
                status = "Servis (Basılı Tut) · Yön tuşuyla köşe seç!";
            }
            else if (_isChargingSecondary)
            {
                status = "Kısa bırak: Drop Shot (Kısa Top) · Basılı tut: Taktik Lob!";
            }
            else if (_phase == Phase.ToPlayer)
            {
                bool atNet = _playerZ > -5.5f;
                if (_returnQueued)
                {
                    status = _preciseReturn ? "MÜKEMMEL ZAMANLAMA KİLİTLENDİ!" : "Vuruş hazır · Yön tuşuyla açı ver!";
                }
                else if (atNet)
                {
                    status = "FİLE ÖNÜNDESİN! Vur: File Volesi · İkinci Tuş: Kısa Top!";
                }
                else if (_ready)
                {
                    status = "ŞİMDİ VUR! (Vur: Topspin · İkinci Tuş: Drop Shot / Lob)";
                }
                else
                {
                    status = "Topa yaklaş · İleri koşup File Volesi yapabilirsin!";
                }
            }
            else
            {
                status = "Top rakipte · Merkezde pozisyon al veya fileye çık!";
            }

            return $"{title} [{GetLeagueLabel()}]  ·  Sen {FormatTennisScore()} Rakip{rallyInfo}\n{status}";
        }

        public string PrimaryButtonLabel()
        {
            if (_phase == Phase.MatchOver) return LocalizationService.Tr("sports.btn.rematch", "Tekrar");
            if (_isChargingServe) return "BIRAK!";
            if (_phase == Phase.Serve) return "Servis (Tut)";
            if (_phase == Phase.ToPlayer && _playerZ > -5.5f) return "Vole!";
            if (_phase == Phase.ToPlayer && _ready) return "TOPSPİN!";
            return "Vur";
        }

        public string SecondaryButtonLabel()
        {
            if (_isChargingSecondary) return _secondaryCharge >= 0.42f ? "LOB!" : "Kısa Top";
            return "Drop / Lob";
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = _player != null && _phase != Phase.MatchOver,
                StaminaFill01 = Mathf.Clamp01(_stamina / 100f),
                CalloutBadge = _calloutTimer > 0f ? _calloutText : null,
                CalloutColor = _calloutColor
            };

            if (_isChargingServe)
            {
                bool sweet = _serveCharge >= 0.56f && _serveCharge <= 0.86f;
                bool over = _serveCharge > 0.92f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _serveCharge;
                state.PrimaryColor = over
                    ? new Color(0.96f, 0.30f, 0.24f)
                    : (sweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.92f, 0.78f));
                state.PrimaryLabel = over ? "FİLE HATASI RİSKİ!" : (sweet ? "ALTIN ACE SERVİS!" : "SERVİS GÜCÜ");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.56f;
                state.SweetSpotMax01 = 0.86f;
            }
            else if (_isChargingSecondary)
            {
                bool lob = _secondaryCharge >= 0.42f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _secondaryCharge;
                state.PrimaryColor = lob ? new Color(0.32f, 0.82f, 1.0f) : new Color(0.26f, 0.94f, 0.72f);
                state.PrimaryLabel = lob ? "TAKTİK LOB!" : "KISA TOP (DROP SHOT)";
                state.ShowSweetSpot = false;
            }
            else if (_player != null && _phase == Phase.ToPlayer)
            {
                float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.25f, _flight.Duration));
                bool inGold = progress >= 0.70f && progress <= 0.94f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = progress;
                state.PrimaryColor = _returnQueued
                    ? new Color(1.0f, 0.86f, 0.24f)
                    : (inGold ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.92f, 0.72f));
                state.PrimaryLabel = _playerZ > -5.5f ? "FİLE VOLESİ ZAMANI" : "TOPSPİN ZAMANLAMA";
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.70f;
                state.SweetSpotMax01 = 0.94f;
            }

            return state;
        }

        // Primary Press (Tap or start Hold-to-Charge Serve!)
        public void Hit()
        {
            if (_player == null) return;

            if (_phase == Phase.MatchOver)
            {
                if (_elapsed >= 0.45f)
                {
                    _points = 0;
                    _opponentPoints = 0;
                    _acesCount = 0;
                    _winnersCount = 0;
                    _stamina = 100f;
                    _lastShotWasPerfect = false;
                    ResetPoint();
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                }
                return;
            }

            if (_phase == Phase.Serve)
            {
                _isChargingServe = true;
                _serveCharge = 0.08f;
                HudRevision++;
                return;
            }

            if (_phase != Phase.ToPlayer || _returnQueued) return;

            CaptureAimInput();
            bool atNet = _playerZ > -5.5f;
            _queuedShotKind = atNet ? ShotKind.Volley : ShotKind.Topspin;

            float remaining = Mathf.Max(0f, _flight.Duration - _elapsed);
            // Mid-air Net Volley: strike early before bounce if player rushed the net!
            if (atNet && _elapsed >= _flight.FirstLeg * 0.52f && _elapsed <= _flight.FirstLeg + 0.14f)
            {
                Vector3 ballNow = _flight.PositionAt(_elapsed);
                if (Mathf.Abs(ballNow.x - PlayerX) < 1.85f && Mathf.Abs(ballNow.z - _playerZ) < 2.25f)
                {
                    ExecuteImmediateNetVolley(ballNow);
                    return;
                }
            }

            if (_ready || remaining <= ContactWindow)
            {
                LockInPlayerReturn(remaining, isDirectTap: true);
                return;
            }

            if (remaining <= _flight.Duration + 0.05f && !_earlyBufferQueued)
            {
                _earlyBufferQueued = true;
                HudRevision++;
            }
        }

        public void TriggerPrimaryRelease(float holdSeconds)
        {
            if (_player == null || !_isChargingServe || _phase != Phase.Serve) return;
            _isChargingServe = false;
            _activeServeCharge = Mathf.Clamp01(_serveCharge);
            CaptureAimInput();

            _phase = Phase.Serving;
            _elapsed = 0f;
            _lastShotWasPerfect = false;
            _serveStart = _ball.localPosition;
            _serveContact = new Vector3(PlayerX + 0.46f, 2.12f, _playerZ + 0.40f);
            _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(_serveContact), ServeTime, serving: true);
            HudRevision++;
        }

        // Secondary Press -> begin Drop Shot (tap) vs Lob (hold)
        public void TriggerSecondaryAction()
        {
            if (_player == null || _phase != Phase.ToPlayer || _returnQueued) return;
            _isChargingSecondary = true;
            _secondaryCharge = 0.08f;
            HudRevision++;
        }

        public void TriggerSecondaryRelease(float holdSeconds)
        {
            if (_player == null || !_isChargingSecondary) return;
            bool isLob = _secondaryCharge >= 0.42f || holdSeconds >= 0.24f;
            _isChargingSecondary = false;
            _secondaryCharge = 0f;
            if (_phase != Phase.ToPlayer || _returnQueued) return;

            CaptureAimInput();
            _queuedShotKind = isLob ? ShotKind.Lob : ShotKind.DropShot;
            float remaining = Mathf.Max(0f, _flight.Duration - _elapsed);
            if (_ready || remaining <= ContactWindow)
            {
                LockInPlayerReturn(remaining, isDirectTap: true);
            }
            else
            {
                _earlyBufferQueued = true;
                HudRevision++;
            }
        }

        private void CaptureAimInput()
        {
            float playerX = PlayerX;
            Vector2 mv = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            _queuedAim = Mathf.Abs(mv.x) >= 0.14f ? Mathf.Clamp(mv.x, -1f, 1f) : (playerX > 0f ? -0.58f : 0.58f);
            _queuedDepthAim = Mathf.Clamp(mv.y, -1f, 1f);
        }

        private void ExecuteImmediateNetVolley(Vector3 contact)
        {
            _stamina = Mathf.Max(0f, _stamina - 14f);
            _player.Humanoid?.PlayTennisSwing(transform.TransformPoint(contact), 0f);
            float targetX = _queuedAim * 3.85f;
            _lastShotKind = ShotKind.Volley;
            _lastShotWasPerfect = true;
            _lastShotKmh = Random.Range(118f, 146f);
            SetTrailColor(new Color(0.26f, 0.96f, 0.84f, 0.92f));
            if (_ballTrail != null) _ballTrail.emitting = true;
            GameFeelAudio.SpawnBurst(transform.TransformPoint(contact), new Color(0.28f, 0.96f, 0.84f), count: 12, spread: 0.42f);
            GameFeelAudio.TriggerHitstop(0.036f);
            _cameraRig?.AddShake(0.12f, 0.16f);
            SetCallout($"FİLE VOLESİ! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(0.28f, 0.96f, 0.84f), 1.35f);
            StartFlight(true, contact, targetX, targetZ: 6.8f, firstLeg: 0.58f, extraArc: 0f);
            _returns++;
        }

        private void LockInPlayerReturn(float remaining, bool isDirectTap)
        {
            _returnQueued = true;
            _earlyBufferQueued = false;
            _preciseReturn = isDirectTap && remaining >= 0.05f && remaining <= 0.42f;
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

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _elapsed += dt;
            _stamina = Mathf.Min(100f, _stamina + 14f * dt);

            if (_calloutTimer > 0f)
            {
                _calloutTimer -= dt;
                if (_calloutTimer <= 0f) HudRevision++;
            }

            if (_isChargingSecondary)
            {
                _secondaryCharge = Mathf.Min(1f, _secondaryCharge + dt / 0.55f);
            }

            switch (_phase)
            {
                case Phase.Serve:
                {
                    Vector2 mv = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
                    if (_isChargingServe)
                    {
                        _serveCharge += dt / 0.82f;
                        if (Mathf.Abs(mv.x) > 0.12f) _queuedAim = Mathf.Clamp(_queuedAim + mv.x * 2.2f * dt, -1f, 1f);
                        if (_serveCharge >= 1.0f)
                        {
                            _serveCharge = 1.0f;
                            TriggerPrimaryRelease(0.85f);
                        }
                    }
                    else if (Mathf.Abs(mv.x) > 0.12f)
                    {
                        float serveMoveX = Mathf.Clamp(PlayerX + mv.x * 3.1f * dt, -2.6f, 2.6f);
                        _playerZ = PlayerBaseline;
                        SetPlayerPosition(serveMoveX, _playerZ);
                        _queuedAim = Mathf.Clamp(-serveMoveX * 0.35f + mv.x * 0.6f, -1f, 1f);
                        Vector3 hand = new Vector3(serveMoveX - 0.25f, 1.12f, _playerZ + 0.33f);
                        _player.Humanoid?.SetTennisLeftHand(transform.TransformPoint(hand));
                        SetBallPosition(hand + Vector3.up * 0.065f);
                    }
                    UpdateAimReticle(show: true, targetZ: 6.4f);
                    break;
                }
                case Phase.Serving:
                    UpdateAimReticle(show: false, targetZ: 6.4f);
                    UpdateServe();
                    break;
                case Phase.ToOpponent:
                case Phase.ToPlayer:
                    UpdateFlight(dt);
                    break;
                case Phase.Point:
                case Phase.MatchOver:
                    UpdateAimReticle(show: false, targetZ: 6.4f);
                    UpdateLooseBall();
                    if (_phase == Phase.Point && _elapsed >= 1.45f) ResetPoint();
                    break;
            }
        }

        private void UpdateAimReticle(bool show, float targetZ)
        {
            if (_aimReticle == null) return;
            if (_aimReticle.gameObject.activeSelf != show) _aimReticle.gameObject.SetActive(show);
            if (!show) return;
            float aimX = Mathf.Clamp(_queuedAim * 3.75f, -3.85f, 3.85f);
            _aimReticle.localPosition = new Vector3(aimX, 0.036f, targetZ);
            float pulse = 1.0f + Mathf.Sin(Time.time * 7f) * 0.09f;
            _aimReticle.localScale = new Vector3(pulse, 1f, pulse);
        }

        // =====================================================================
        // AUTONOMOUS NPC vs NPC AMBIENT RALLY
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
                    StartFlight(true, _serveContact, Random.Range(-3.1f, 3.1f), -PlayerBaseline - ContactForward, Random.Range(1.02f, 1.18f), 0f, volumeScale: 0.58f);
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
                        MoveNpc(_opponent, 0f, -PlayerBaseline, _elapsed > 0.24f ? 2.1f : 0f);
                        float gap = MoveNpc(_ambientPlayer, targetX, PlayerBaseline, _elapsed >= 0.16f ? speed : 0f);
                        if (!_ambientSwinging && !shouldMissThisBall && remaining <= 0.19f && gap <= remaining * speed + 0.28f)
                        {
                            _ambientSwinging = true;
                            _ambientPlayer.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
                        }
                        if (remaining > 0f) return;

                        if (shouldMissThisBall || gap > 0.35f) { FinishAmbientPoint(); return; }
                        _ambientPlayer.PlayTennisSwing(_ball.position, 0f);
                        _returns++;
                        StartFlight(true, _flight.End, Random.Range(-3.45f, 3.45f), -PlayerBaseline - ContactForward, Random.Range(0.96f, 1.16f), 0f, volumeScale: 0.58f);
                    }
                    else
                    {
                        MoveNpc(_ambientPlayer, 0f, PlayerBaseline, _elapsed > 0.24f ? 2.1f : 0f);
                        float gap = MoveNpc(_opponent, targetX, -PlayerBaseline, _elapsed >= 0.16f ? speed : 0f);
                        if (!_opponentSwinging && !shouldMissThisBall && remaining <= 0.19f && gap <= remaining * speed + 0.28f)
                        {
                            _opponentSwinging = true;
                            _opponent.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
                        }
                        if (remaining > 0f) return;

                        if (shouldMissThisBall || gap > 0.35f) { FinishAmbientPoint(); return; }
                        _opponent.PlayTennisSwing(_ball.position, 0f);
                        _returns++;
                        StartFlight(false, _flight.End, Random.Range(-3.45f, 3.45f), PlayerBaseline + ContactForward, Random.Range(0.96f, 1.16f), 0f, volumeScale: 0.58f);
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
                float raise = Mathf.SmoothStep(0f, 1f, time / ServeRelease) * 0.24f;
                SetBallPosition(_serveStart + Vector3.up * raise);
                _player.Humanoid?.SetTennisLeftHand(_ball.position - transform.up * 0.065f);
            }
            else
            {
                SetBallPosition(Projectile(_serveStart + Vector3.up * 0.24f, _serveContact, time - ServeRelease, ServeTime - ServeRelease));
                _player.Humanoid?.SetTennisLeftHand(null);
            }

            if (time < ServeTime) return;
            _player.Humanoid?.PlayTennisSwing(_ball.position, 0f, serving: true);

            bool isGoldenAce = _activeServeCharge >= 0.56f && _activeServeCharge <= 0.86f;
            bool isOvercharged = _activeServeCharge > 0.92f;

            if (isOvercharged && Random.value < 0.52f)
            {
                // Fault into the net!
                SetCallout("FİLE HATASI! (Gücü altın bölgede bırak)", new Color(0.96f, 0.36f, 0.30f), 1.4f);
                GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                StartFlight(true, _serveContact, _queuedAim * 1.4f, targetZ: 0.1f, firstLeg: 0.42f, extraArc: 0f);
                AwardPoint(playerWon: false);
                return;
            }

            _lastShotKind = isGoldenAce ? ShotKind.GoldenServe : ShotKind.Flat;
            _lastShotWasPerfect = isGoldenAce;
            float firstLeg = isGoldenAce ? 0.64f : Mathf.Lerp(1.06f, 0.80f, _activeServeCharge);
            float targetX = _queuedAim * (isGoldenAce ? 3.85f : 3.20f);
            _lastShotKmh = isGoldenAce ? Random.Range(168f, 198f) : Mathf.Lerp(112f, 156f, _activeServeCharge);

            if (SaveManager.Instance != null && _lastShotKmh > SaveManager.Instance.Data.bestTennisServeKmh)
            {
                SaveManager.Instance.Data.bestTennisServeKmh = Mathf.RoundToInt(_lastShotKmh);
            }

            if (isGoldenAce)
            {
                SetTrailColor(new Color(1.0f, 0.86f, 0.22f, 0.95f));
                if (_ballTrail != null) _ballTrail.emitting = true;
                GameFeelAudio.SpawnBurst(transform.TransformPoint(_serveContact), new Color(1.0f, 0.86f, 0.24f), count: 14, spread: 0.48f);
                GameFeelAudio.TriggerHitstop(0.038f);
                _cameraRig?.AddShake(0.14f, 0.18f);
                SetCallout($"ALTIN SERVİS! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(1.0f, 0.86f, 0.24f), 1.35f);
            }
            else
            {
                SetTrailColor(new Color(0.32f, 0.94f, 0.80f, 0.72f));
                if (_ballTrail != null) _ballTrail.emitting = true;
                SetCallout($"SERVİS ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(0.34f, 0.94f, 0.82f), 1.0f);
            }

            StartFlight(true, _serveContact, targetX, targetZ: 9.8f, firstLeg: firstLeg, extraArc: 0f);
            _returns = 1;
        }

        private void UpdateFlight(float dt)
        {
            float time = Mathf.Min(_elapsed, _flight.Duration);
            float remaining = _flight.Duration - time;
            SetBallPosition(_flight.PositionAt(time));
            if (_elapsed - dt < _flight.FirstLeg && _elapsed >= _flight.FirstLeg)
            {
                _ballSound.PlayOneShot(_bounceSound, 0.48f);
                // Clay court dust burst on bounce!
                GameFeelAudio.SpawnBurst(_ball.position, new Color(0.84f, 0.44f, 0.26f), count: 4, spread: 0.20f);
            }

            bool toPlayer = _phase == Phase.ToPlayer;
            float targetX = _flight.End.x + (toPlayer ? -ContactSide : ContactSide);

            if (toPlayer)
            {
                // Opponent smoothly recovers toward center baseline (unless previously pulled in by drop shot)
                MoveNpc(_opponent, 0f, -PlayerBaseline, _elapsed > 0.25f ? 2.45f : 0f);

                // Player has full 2D court movement (lateral assist + manual forward/back net rush!)
                Vector2 mv = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
                if (Mathf.Abs(mv.y) > 0.14f)
                {
                    _playerZ = Mathf.Clamp(_playerZ + mv.y * 5.2f * dt, PlayerBaseline - 0.35f, -2.5f);
                }
                else if (Mathf.Abs(_playerZ - _flight.End.z) > 0.35f)
                {
                    _playerZ = Mathf.MoveTowards(_playerZ, Mathf.Clamp(_flight.End.z, PlayerBaseline, -3.0f), 4.2f * dt);
                }

                float desiredX = Mathf.Abs(mv.x) > 0.18f
                    ? Mathf.Clamp(PlayerX + mv.x * 6.8f * dt, -HalfWidth - 0.5f, HalfWidth + 0.5f)
                    : targetX;
                // Gentle magnetic assist toward incoming ball X so mobile play feels crisp & fair!
                float blendedTargetX = Mathf.Lerp(desiredX, targetX, 0.58f);
                float gap = MovePlayer(blendedTargetX, _playerZ, 7.6f, dt);

                if (Mathf.Abs(mv.x) >= 0.14f) _queuedAim = Mathf.Clamp(mv.x, -1f, 1f);
                UpdateAimReticle(show: true, targetZ: _queuedShotKind == ShotKind.DropShot ? 3.1f : 10.1f);

                if (_earlyBufferQueued && !_returnQueued && remaining <= 0.36f && gap < 0.85f)
                {
                    LockInPlayerReturn(remaining, isDirectTap: false);
                }

                bool inWindow = remaining <= ContactWindow && remaining >= 0.02f;
                SetReady(!_returnQueued && inWindow && gap < 0.90f);
                UpdateTimingRingVisual(remaining, inWindow);

                if (remaining > 0f) return;

                if (!_returnQueued || gap > 0.85f)
                {
                    AwardPoint(playerWon: false);
                    return;
                }

                _player.Humanoid?.PlayTennisSwing(_ball.position, 0f);
                StartPlayerShot(_flight.End, _preciseReturn, _queuedShotKind);
                return;
            }

            // Ball flying toward Opponent
            UpdateAimReticle(show: false, targetZ: 10f);
            if (_readyMarker != null && _readyMarker.gameObject.activeSelf)
            {
                _readyMarker.gameObject.SetActive(false);
            }

            // Player can run forward to rush the net or recover to center!
            Vector2 mv2 = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            if (mv2.sqrMagnitude > 0.04f)
            {
                _playerZ = Mathf.Clamp(_playerZ + mv2.y * 4.8f * dt, PlayerBaseline - 0.35f, -2.5f);
                float nextX = Mathf.Clamp(PlayerX + mv2.x * 5.2f * dt, -HalfWidth, HalfWidth);
                MovePlayer(nextX, _playerZ, 5.4f, dt);
            }
            else
            {
                MovePlayer(0f, Mathf.MoveTowards(_playerZ, PlayerBaseline, 2.2f * dt), _elapsed > 0.28f ? 2.65f : 0f, dt);
            }

            // Opponent physically sprints in 2D (X & Z!) at league-scaled speed — Drop Shots & Lobs genuinely beat them!
            float reactionTime = Mathf.Max(0.14f, 0.34f - _leagueTier * 0.07f + (_lastShotKind == ShotKind.DropShot ? 0.14f : 0f));
            float opponentSpeed = (3.40f + _leagueTier * 0.58f) * (_lastShotWasPerfect ? 0.88f : 1.0f);
            float opponentGap = MoveNpc(_opponent, targetX, _flight.End.z, _elapsed >= reactionTime ? opponentSpeed : 0f);
            if (!_opponentSwinging && remaining <= 0.20f && opponentGap <= remaining * opponentSpeed + 0.32f)
            {
                _opponentSwinging = true;
                _opponent.PlayTennisSwing(transform.TransformPoint(_flight.End), remaining);
            }
            if (remaining > 0f) return;

            if (opponentGap > 0.42f)
            {
                if (_returns <= 1 && _lastShotKind == ShotKind.GoldenServe)
                {
                    _acesCount++;
                    SetCallout($"ACE SERVİS SAYISI! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(1.0f, 0.86f, 0.22f), 1.65f);
                }
                else
                {
                    _winnersCount++;
                    string winType = _lastShotKind switch
                    {
                        ShotKind.DropShot => "KISA TOP WINNER!",
                        ShotKind.Lob => "MUHTEŞEM LOB WINNER!",
                        ShotKind.Volley => "FİLE VOLESİ WINNER!",
                        _ => $"KÖŞEYE WINNER! ({Mathf.RoundToInt(_lastShotKmh)} km/s)"
                    };
                    SetCallout(winType, new Color(0.28f, 0.96f, 0.82f), 1.55f);
                }
                AwardPoint(playerWon: true);
                return;
            }

            _opponent.PlayTennisSwing(_ball.position, 0f);
            float returnX = Random.Range(-3.35f, 3.35f);
            float returnZ = Random.value < 0.20f && _leagueTier >= 1 ? -6.2f : (PlayerBaseline + ContactForward);
            float returnFirstLeg = Mathf.Max(0.92f, (1.28f - _leagueTier * 0.08f) - _returns * 0.015f);
            SetTrailColor(new Color(0.95f, 0.95f, 0.36f, 0.65f));
            StartFlight(false, _flight.End, returnX, returnZ, returnFirstLeg, extraArc: 0f);
        }

        private void UpdateTimingRingVisual(float remaining, bool inWindow)
        {
            if (_readyMarker == null) return;
            if (!_readyMarker.gameObject.activeSelf) _readyMarker.gameObject.SetActive(true);

            _readyMarker.localPosition = new Vector3(_flight.End.x, 0.016f, _flight.End.z);
            float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.25f, _flight.Duration));
            float ringScale = _returnQueued ? 1.08f : Mathf.Lerp(1.85f, 0.92f, progress);
            _readyMarker.localScale = new Vector3(ringScale, 1f, ringScale);

            if (_readyMarkerRenderer != null && _markerProps != null)
            {
                Color c, em;
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

        private void StartPlayerShot(Vector3 contact, bool precise, ShotKind kind)
        {
            _lastShotKind = kind;
            _lastShotWasPerfect = precise;
            _stamina = Mathf.Max(0f, _stamina - (kind == ShotKind.Topspin && precise ? 10f : 6f));

            float targetX = _queuedAim * (precise ? 3.92f : 3.25f);
            float targetZ = -PlayerBaseline - ContactForward;
            float firstLeg = precise ? 0.78f : 1.08f;
            float extraArc = 0f;

            if (kind == ShotKind.DropShot)
            {
                targetX = _queuedAim * 3.15f;
                targetZ = 3.1f; // Feather-soft right behind the net!
                firstLeg = 0.86f;
                extraArc = 0.45f;
                _lastShotKmh = Random.Range(48f, 64f);
                SetTrailColor(new Color(0.36f, 0.96f, 0.84f, 0.85f));
                GameFeelAudio.SpawnBurst(transform.TransformPoint(contact), new Color(0.36f, 0.96f, 0.84f), count: 8, spread: 0.32f);
                SetCallout("KISA TOP (DROP SHOT)!", new Color(0.36f, 0.96f, 0.84f), 1.25f);
            }
            else if (kind == ShotKind.Lob)
            {
                targetX = _queuedAim * 3.50f;
                targetZ = 10.6f; // Deep baseline lob!
                firstLeg = 1.48f;
                extraArc = 3.85f;
                _lastShotKmh = Random.Range(68f, 84f);
                SetTrailColor(new Color(0.38f, 0.82f, 1.0f, 0.85f));
                SetCallout("TAKTİK LOB!", new Color(0.38f, 0.84f, 1.0f), 1.25f);
            }
            else
            {
                _lastShotKmh = precise ? Random.Range(132f, 166f) : Random.Range(94f, 124f);
                if (precise)
                {
                    SetTrailColor(new Color(1.0f, 0.84f, 0.22f, 0.95f));
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(contact), new Color(1.0f, 0.85f, 0.24f), count: 12, spread: 0.42f);
                    GameFeelAudio.TriggerHitstop(0.032f);
                    _cameraRig?.AddShake(0.11f, 0.14f);
                    SetCallout($"MÜKEMMEL TOPSPİN! ({Mathf.RoundToInt(_lastShotKmh)} km/s)", new Color(1.0f, 0.86f, 0.24f), 1.25f);
                }
                else
                {
                    SetTrailColor(new Color(0.92f, 0.96f, 0.34f, 0.72f));
                }
            }

            if (_ballTrail != null) _ballTrail.emitting = true;
            StartFlight(true, contact, targetX, targetZ, firstLeg, extraArc);
            _returns++;
        }

        private void PlayCourtSound(AudioClip clip, float volume)
        {
            if (_ballSound == null || clip == null) return;
            if (IsAmbientMatch)
            {
                Camera cam = Camera.main;
                if (cam == null || (cam.transform.position - transform.position).sqrMagnitude > 22f * 22f) return;
            }
            _ballSound.PlayOneShot(clip, volume);
        }

        private void StartFlight(bool toOpponent, Vector3 start, float targetX, float targetZ, float firstLeg, float extraArc, float volumeScale = 1f)
        {
            _flight = new Flight
            {
                Start = start,
                End = new Vector3(targetX, ContactHeight, targetZ),
                FirstLeg = firstLeg,
                ExtraArc = extraArc
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
            _isChargingSecondary = false;
            if (_ballTrail != null) _ballTrail.emitting = false;

            // Real Tennis Game win condition: reach 4+ points with a 2-point lead (handles 40-40 Deuce / Advantage!)
            bool matchOver = (_points >= 4 || _opponentPoints >= 4) && Mathf.Abs(_points - _opponentPoints) >= 2;
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
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: true);
                    GameFeelAudio.PlayCrowdCheer(bigCheer: true);
                    GameFeelAudio.PlayMilestoneFanfare();
                    int reward = 85 + _leagueTier * 30;
                    SaveManager.Instance?.AddMoney(reward);
                    if (SaveManager.Instance != null)
                    {
                        SaveManager.Instance.Data.tennisWins++;
                        if (SaveManager.Instance.Data.tennisLeagueTier < 2 && SaveManager.Instance.Data.tennisWins >= (SaveManager.Instance.Data.tennisLeagueTier + 1) * 2)
                        {
                            SaveManager.Instance.Data.tennisLeagueTier++;
                            _leagueTier = SaveManager.Instance.Data.tennisLeagueTier;
                        }
                        SaveManager.Instance.Save();
                    }
                    NeighborhoodReputation.AddReputation(30 + _leagueTier * 10, $"Tenis [{GetLeagueLabel()}] Zaferi! (+{reward} ₺)");
                }
                else
                {
                    GameFeelAudio.PlayCrowdCheer(bigCheer: false);
                    GameFeelAudio.PlayCoinChime();
                }
            }
            else
            {
                _opponent.PlaySportAnim(SportAnimKind.VictoryJump, 0.58f);
                if (matchOver) GameFeelAudio.PlayRefereeWhistle(doubleBlast: true);
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
            _isChargingServe = false;
            _isChargingSecondary = false;
            _lastShotWasPerfect = false;
            _queuedAim = 0.45f;
            _playerZ = PlayerBaseline;
            _opponentZ = -PlayerBaseline;
            if (_ballTrail != null) { _ballTrail.Clear(); _ballTrail.emitting = false; }
            SetReady(false);
            if (_readyMarker != null) _readyMarker.gameObject.SetActive(false);
            HudRevision++;

            _opponent.transform.SetLocalPositionAndRotation(new Vector3(0f, CourtSurface, _opponentZ), Quaternion.Euler(0f, 180f, 0f));
            _opponent.SetLocomotion(0f, true, 0f);
            _opponent.ClearTennisSwing();
            SetPlayerPosition(0f, _playerZ);
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            _player.Humanoid?.ClearTennisSwing();
            Vector3 hand = new Vector3(-0.25f, 1.12f, _playerZ + 0.33f);
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
            if (_player != null) _player.Humanoid?.SetLookAtPoint(true, _ball.position);
            else if (_ambientPlayer != null && _ambientPlayer.gameObject.activeSelf) _ambientPlayer.SetLookAtPoint(true, _ball.position);
            if (_opponent != null) _opponent.SetLookAtPoint(true, _ball.position);
        }

        private float PlayerX => transform.InverseTransformPoint(_player.transform.position).x;

        private float MovePlayer(float targetX, float targetZ, float speed, float dt)
        {
            float x = PlayerX;
            float nextX = Mathf.MoveTowards(x, targetX, speed * dt);
            float nextZ = Mathf.MoveTowards(_playerZ, targetZ, speed * dt);
            float dx = nextX - x;
            float dz = nextZ - _playerZ;
            _playerZ = nextZ;
            if (Mathf.Abs(dx) > 0.0001f || Mathf.Abs(dz) > 0.0001f) SetPlayerPosition(nextX, nextZ);
            Vector3 moveVec = transform.right * dx + transform.forward * dz;
            float spd = dt > 0.0001f ? moveVec.magnitude / dt : 0f;
            _player.Humanoid?.SetLocomotion(spd, true, 0f, spd > 0.05f ? moveVec.normalized : transform.forward);
            return Mathf.Abs(targetX - nextX);
        }

        private float MoveNpc(ProceduralHumanoid npc, float targetX, float targetZ, float speed)
        {
            Vector3 pos = npc.transform.localPosition;
            Vector3 target = new Vector3(targetX, CourtSurface, Mathf.Clamp(targetZ, 1.5f, HalfLength - 0.5f));
            Vector3 delta = target - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            if (speed > 0f && dist > 0.02f)
            {
                float step = Mathf.Min(dist, speed * dt);
                Vector3 dir = delta / dist;
                pos += dir * step;
                pos.y = CourtSurface;
                npc.transform.localPosition = pos;
                npc.SetLocomotion(step / dt, true, 0f, transform.TransformDirection(dir));
                return dist - step;
            }
            npc.SetLocomotion(0f, true, 0f);
            return dist;
        }

        private void SetPlayerPosition(float x, float z)
        {
            _player.TeleportTo(transform.TransformPoint(new Vector3(x, CourtSurface, z)), transform.rotation);
        }

        private static Vector3 Projectile(Vector3 start, Vector3 end, float time, float duration)
        {
            Vector3 point = Vector3.LerpUnclamped(start, end, time / duration);
            point.y += 0.5f * Gravity * time * (duration - time);
            return point;
        }
    }
}
