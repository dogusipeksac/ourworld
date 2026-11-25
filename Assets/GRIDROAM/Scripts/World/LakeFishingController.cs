using System;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.World
{
    /// <summary>
    /// Phase 3A: Sultanbeyli Lake Animated 3D Fishing Mini-Game at the wooden viewing pier (-182, 0.44, 0):
    /// - Equips custom Blender OW_FishingRod with spinning ReelHandle & dynamic rod flex
    /// - Casts a floating bobber into the turquoise lake with water splash bursts
    /// - Bite alert + mobile-ergonomic one-tap/hold reel bar ("OLTAYI ÇEK!")
    /// - Leaps a real Blender OW_LakeFish with rapid TailFinPivot wiggle out of the water and adds it to SaveData!
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class LakeFishingController : MonoBehaviour
    {
        public static LakeFishingController Instance { get; private set; }
        public static readonly Vector3 PierTipWorldPos = new Vector3(-182.0f, 0.44f, 0.0f);

        private enum FishingPhase { Idle, Casting, WaitingBite, BiteReeling, CatchShowcase }

        private struct FishSpecies
        {
            public string NameTr;
            public string NameEn;
            public int PriceTl;
            public Color ScaleCol;
            public Color FinCol;
            public float SizeScale;
        }

        private static readonly FishSpecies[] Species =
        {
            new FishSpecies { NameTr = "Gümüş Sazan",        NameEn = "Silver Carp",         PriceTl =  45, ScaleCol = new Color(0.68f, 0.78f, 0.84f), FinCol = new Color(0.86f, 0.54f, 0.24f), SizeScale = 1.00f },
            new FishSpecies { NameTr = "Aydos Alabalığı",    NameEn = "Aydos Brook Trout",   PriceTl =  75, ScaleCol = new Color(0.36f, 0.68f, 0.56f), FinCol = new Color(0.94f, 0.42f, 0.36f), SizeScale = 1.12f },
            new FishSpecies { NameTr = "Gölet Levreği",      NameEn = "Sultanbeyli Bass",    PriceTl = 110, ScaleCol = new Color(0.28f, 0.56f, 0.78f), FinCol = new Color(0.92f, 0.72f, 0.22f), SizeScale = 1.24f },
            new FishSpecies { NameTr = "Dev Yayın Balığı",   NameEn = "Giant Catfish",       PriceTl = 165, ScaleCol = new Color(0.32f, 0.36f, 0.34f), FinCol = new Color(0.70f, 0.62f, 0.44f), SizeScale = 1.48f },
            new FishSpecies { NameTr = "Efsanevi Altın Sazan", NameEn = "Legendary Golden Koi", PriceTl = 320, ScaleCol = new Color(0.99f, 0.78f, 0.16f), FinCol = new Color(0.96f, 0.28f, 0.14f), SizeScale = 1.42f }
        };

        private FishingPhase _phase = FishingPhase.Idle;
        private float _phaseTimer;
        private float _biteWaitDuration;
        private float _reelProgress;
        private bool _reelButtonHeld;
        private PlayerModeController _activePlayer;

        // 3D Props & Animation Pivots
        private GameObject _activeRodRoot;
        private Transform _reelHandle;
        private GameObject _bobberObj;
        private Vector3 _bobberAnchor;
        private GameObject _leapingFishObj;
        private SkeletalClipPlayer _leapingFishSkel;
        private Transform _leapingFishTail;
        private FishSpecies _hookedFish;
        private Vector3 _fishLeapStart;
        private Vector3 _fishLeapEnd;
        private float _fishArcHeight = 2.15f;
        private bool _fishExitSplashed;
        private bool _fishEntrySplashed;
        private bool _fishApexSparkled;

        // Low-pass IK smoothing & organic fishing motion state
        private Vector3 _smR;
        private Vector3 _smL;
        private float _smSpine;
        private float _smHead;
        private float _smDrop;
        private bool _smInit;
        private float _waitTwitchCountdown = 5f;
        private float _waitTwitchTimer = -1f;
        private float _fishPullCountdown = 0.5f;
        private float _fishPullStrength;

        // Sleek Mobile Fishing Overlay HUD
        private GameObject _uiCanvas;
        private GameObject _hudCard;
        private Text _hudTitle;
        private Text _hudStatus;
        private Image _reelBarFill;
        private Button _reelBtn;
        private Text _reelBtnLabel;

        public bool IsFishing => _phase != FishingPhase.Idle;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_LakeFishingController");
            go.AddComponent<LakeFishingController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildPierFishingStand();
            BuildFishingHud();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private Material _bobberMat;

        private void OnDestroy()
        {
            if (_bobberMat != null) Destroy(_bobberMat);
            if (Instance == this) Instance = null;
        }

        public void BeginFishing(PlayerModeController player)
        {
            if (player == null || IsFishing) return;
            _activePlayer = player;
            player.SetModalLock(this, true);

            // Position Deniz cleanly facing West across the turquoise lake!
            if (player.FootController != null)
            {
                player.FootController.TeleportTo(PierTipWorldPos + new Vector3(0.4f, 0.08f, 0f), Quaternion.Euler(0f, -90f, 0f));
            }

            // Equip Blender OW_FishingRod in Deniz's hand!
            if (_activeRodRoot == null)
            {
                _activeRodRoot = new GameObject("ActiveFishingRodPivot");
                _activeRodRoot.transform.SetParent(transform, false);
                GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_FishingRod", _activeRodRoot.transform, Vector3.zero, Quaternion.identity);
                if (fbx != null)
                {
                    _reelHandle = OurWorldModelLibrary.FindPart(fbx.transform, "ReelHandle");
                }
            }
            _activeRodRoot.SetActive(true);
            _activeRodRoot.transform.SetPositionAndRotation(
                PierTipWorldPos + new Vector3(0.05f, 1.12f, 0.24f),
                Quaternion.Euler(14f, 90f, 0f));

            if (_bobberObj != null) _bobberObj.SetActive(false);
            if (_leapingFishObj != null) _leapingFishObj.SetActive(false);

            _phase = FishingPhase.Casting;
            _phaseTimer = 0f;
            _reelProgress = 0.25f;
            _reelButtonHeld = false;
            _smInit = false;
            _waitTwitchCountdown = UnityEngine.Random.Range(4f, 7f);
            _waitTwitchTimer = -1f;
            _fishPullCountdown = UnityEngine.Random.Range(0.35f, 0.90f);
            _fishPullStrength = 0f;
            _biteWaitDuration = UnityEngine.Random.Range(2.2f, 3.8f);
            _bobberAnchor = new Vector3(-191.5f, 0.08f, UnityEngine.Random.Range(-2.6f, 2.6f));

            if (_hudCard != null) _hudCard.SetActive(true);
            GameFeelAudio.PlayUiClick();
            UpdateFishingUi();
        }

        public void StopFishing()
        {
            _phase = FishingPhase.Idle;
            _reelButtonHeld = false;
            _smInit = false;
            _waitTwitchTimer = -1f;
            _fishPullStrength = 0f;
            if (_activeRodRoot != null) _activeRodRoot.SetActive(false);
            if (_bobberObj != null) _bobberObj.SetActive(false);
            if (_leapingFishObj != null) _leapingFishObj.SetActive(false);
            if (_hudCard != null) _hudCard.SetActive(false);
            if (_activePlayer != null)
            {
                if (_activePlayer.FootController != null && _activePlayer.FootController.Humanoid != null)
                {
                    _activePlayer.FootController.Humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                }
                _activePlayer.SetModalLock(this, false);
                _activePlayer = null;
            }
        }

        /// <summary>
        /// Instant deterministic catch helper for automated Play Mode verification.
        /// </summary>
        public void TriggerInstantCatchForTest()
        {
            if (!IsFishing && PlayerModeController.Instance != null)
            {
                BeginFishing(PlayerModeController.Instance);
            }
            PickHookedFish();
            CompleteFishCatch();
        }

        private void Update()
        {
            if (_phase == FishingPhase.Idle) return;

            float dt = Time.deltaTime;
            _phaseTimer += dt;

            bool spaceOrE = Keyboard.current != null && (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed);
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (esc)
            {
                StopFishing();
                return;
            }

            switch (_phase)
            {
                case FishingPhase.Casting:
                {
                    float t = Mathf.Clamp01(_phaseTimer / 0.65f);
                    if (_activeRodRoot != null)
                    {
                        float castAngle;
                        if (t < 0.38f)
                        {
                            castAngle = Mathf.Lerp(10f, 52f, Mathf.SmoothStep(0f, 1f, t / 0.38f));
                        }
                        else if (t < 0.56f)
                        {
                            float lin = (t - 0.38f) / 0.18f;
                            float easeOut = 1f - (1f - lin) * (1f - lin);
                            castAngle = Mathf.Lerp(52f, -22f, easeOut);
                        }
                        else
                        {
                            castAngle = Mathf.Lerp(-22f, -12f, Mathf.SmoothStep(0f, 1f, (t - 0.56f) / 0.44f));
                        }
                        _activeRodRoot.transform.rotation = Quaternion.Euler(castAngle, 90f, 0f);
                    }
                    if (t >= 1f)
                    {
                        _phase = FishingPhase.WaitingBite;
                        _phaseTimer = 0f;
                        _waitTwitchCountdown = UnityEngine.Random.Range(4f, 7f);
                        _waitTwitchTimer = -1f;
                        if (_bobberObj != null)
                        {
                            _bobberObj.SetActive(true);
                            _bobberObj.transform.position = _bobberAnchor;
                        }
                        GameFeelAudio.SpawnBurst(_bobberAnchor + Vector3.up * 0.1f, new Color(0.75f, 0.94f, 1f), count: 10, spread: 0.45f);
                        UpdateFishingUi();
                    }
                    break;
                }

                case FishingPhase.WaitingBite:
                {
                    if (_bobberObj != null)
                    {
                        _bobberObj.transform.position = _bobberAnchor + new Vector3(0f, Mathf.Sin(Time.time * 3.2f) * 0.045f, 0f);
                    }
                    if (_activeRodRoot != null)
                    {
                        float twitchAngle = _waitTwitchTimer >= 0f
                            ? Mathf.Sin(Mathf.Clamp01(_waitTwitchTimer / 0.25f) * Mathf.PI) * 7f
                            : 0f;
                        _activeRodRoot.transform.rotation = Quaternion.Euler(-12f + Mathf.Sin(Time.time * 1.4f) * 2.5f + twitchAngle, 90f, 0f);
                    }
                    if (_phaseTimer >= _biteWaitDuration)
                    {
                        PickHookedFish();
                        _phase = FishingPhase.BiteReeling;
                        _phaseTimer = 0f;
                        _reelProgress = 0.28f;
                        _fishPullCountdown = UnityEngine.Random.Range(0.35f, 0.90f);
                        _fishPullStrength = 0.08f;
                        GameFeelAudio.PlayUiClick();
                        GameFeelAudio.SpawnBurst(_bobberAnchor + Vector3.up * 0.2f, new Color(0.86f, 0.98f, 1f), count: 16, spread: 0.65f);
                        UpdateFishingUi();
                    }
                    break;
                }

                case FishingPhase.BiteReeling:
                {
                    bool reeling = _reelButtonHeld || spaceOrE;
                    if (reeling)
                    {
                        _reelProgress += dt * 0.44f;
                        if (_reelHandle != null) _reelHandle.Rotate(540f * dt, 0f, 0f, Space.Self);
                    }
                    else
                    {
                        _reelProgress = Mathf.Max(0.05f, _reelProgress - dt * 0.12f);
                    }

                    // Dynamic rod flex responding to fish-pull impulses & bobber thrashing!
                    if (_activeRodRoot != null)
                    {
                        float baseFlex = reeling ? -22f : -15f;
                        float flex = baseFlex - _fishPullStrength * 110f + Mathf.Sin(Time.time * 9f) * 2.2f;
                        _activeRodRoot.transform.rotation = Quaternion.Euler(flex, 90f, 0f);
                    }
                    if (_bobberObj != null)
                    {
                        Vector3 pullPos = Vector3.Lerp(_bobberAnchor, PierTipWorldPos + new Vector3(-2.5f, 0.08f, 0f), _reelProgress);
                        pullPos.y = 0.02f + Mathf.Sin(Time.time * 22f) * 0.12f;
                        _bobberObj.transform.position = pullPos;
                    }

                    UpdateFishingUi();

                    if (_reelProgress >= 1.0f)
                    {
                        CompleteFishCatch();
                    }
                    break;
                }

                case FishingPhase.CatchShowcase:
                {
                    // Parabolic ballistic fish leap from open water -> showcase arc -> clean plunge back into lake!
                    const float leapDuration = 1.92f;
                    float u = Mathf.Clamp01(_phaseTimer / leapDuration);
                    if (_leapingFishObj != null)
                    {
                        if (u < 1f)
                        {
                            Vector3 p = Vector3.Lerp(_fishLeapStart, _fishLeapEnd, u);
                            p.y += 4f * _fishArcHeight * u * (1f - u);
                            _leapingFishObj.transform.position = p;

                            // Analytic trajectory velocity tangent -> body pitch follows ascent, apex & dive!
                            Vector3 vel = (_fishLeapEnd - _fishLeapStart) / leapDuration;
                            vel.y += (4f * _fishArcHeight * (1f - 2f * u)) / leapDuration;
                            float airEnv = Mathf.Sin(u * Mathf.PI);
                            if (vel.sqrMagnitude > 0.001f)
                            {
                                Quaternion trajRot = Quaternion.LookRotation(vel.normalized, Vector3.up);
                                float wiggleYaw = Mathf.Sin(_phaseTimer * 22f) * 10f * airEnv;
                                float wiggleRoll = Mathf.Sin(_phaseTimer * 17f) * 14f * airEnv;
                                _leapingFishObj.transform.rotation = trajRot * Quaternion.Euler(0f, wiggleYaw, wiggleRoll);
                            }

                            // Water exit splash synced to exact surface breach (going UP)
                            if (!_fishExitSplashed && u < 0.5f && p.y >= 0.04f)
                            {
                                _fishExitSplashed = true;
                                GameFeelAudio.PlayWaterSplash();
                                GameFeelAudio.SpawnBurst(new Vector3(p.x, 0.08f, p.z), new Color(0.84f, 0.97f, 1f), count: 16, spread: 0.65f);
                            }

                            // Celebration scale-colored sparkle burst right at apex showcase!
                            if (!_fishApexSparkled && u >= 0.48f)
                            {
                                _fishApexSparkled = true;
                                GameFeelAudio.SpawnBurst(p, _hookedFish.ScaleCol, count: 18, spread: 0.78f);
                            }

                            // Water entry splash synced to exact plunge re-entry (going DOWN)
                            if (!_fishEntrySplashed && u > 0.5f && p.y <= 0.06f)
                            {
                                _fishEntrySplashed = true;
                                GameFeelAudio.PlayWaterSplash();
                                GameFeelAudio.SpawnBurst(new Vector3(p.x, 0.08f, p.z), new Color(0.82f, 0.96f, 1f), count: 18, spread: 0.72f);
                            }

                            if (_leapingFishSkel != null && _leapingFishSkel.HasClips)
                            {
                                _leapingFishSkel.SetActiveSpeed(Mathf.Lerp(1.25f, 2.15f, airEnv));
                            }
                            if (_leapingFishTail != null)
                            {
                                _leapingFishTail.localRotation = Quaternion.Euler(0f, Mathf.Sin(_phaseTimer * 36f) * (22f + 16f * airEnv), 0f);
                            }
                        }
                        else if (_leapingFishObj.activeSelf)
                        {
                            if (!_fishEntrySplashed)
                            {
                                _fishEntrySplashed = true;
                                GameFeelAudio.PlayWaterSplash();
                                GameFeelAudio.SpawnBurst(new Vector3(_fishLeapEnd.x, 0.08f, _fishLeapEnd.z), new Color(0.82f, 0.96f, 1f), count: 18, spread: 0.72f);
                            }
                            // Cleanly hide beneath water surface after entry splash
                            _leapingFishObj.SetActive(false);
                        }
                    }
                    if (_phaseTimer >= 2.2f)
                    {
                        // Ready for next cast immediately!
                        if (_leapingFishObj != null) _leapingFishObj.SetActive(false);
                        _phase = FishingPhase.Casting;
                        _phaseTimer = 0f;
                        _biteWaitDuration = UnityEngine.Random.Range(2.0f, 3.6f);
                        _bobberAnchor = new Vector3(-191.5f, 0.08f, UnityEngine.Random.Range(-2.8f, 2.8f));
                        UpdateFishingUi();
                    }
                    break;
                }
            }

            UpdatePlayerFishingIk(spaceOrE, dt);
        }

        private Vector3 _lastRightHandTarget;

        private void LateUpdate()
        {
            if (_phase == FishingPhase.Idle || _activeRodRoot == null || !_activeRodRoot.activeSelf) return;
            if (_activePlayer == null || _activePlayer.FootController == null || _activePlayer.FootController.Humanoid == null) return;

            ProceduralHumanoid hum = _activePlayer.FootController.Humanoid;
            // Runs AFTER ProceduralHumanoid.LateUpdate() 2-bone IK so the rod cork grip locks 100% inside Deniz's actual animated Right Palm!
            Vector3 palm = hum.RightForearm != null
                ? hum.RightForearm.TransformPoint(new Vector3(0f, -0.28f, 0f))
                : _lastRightHandTarget;
            _activeRodRoot.transform.position = palm;
        }

        private void UpdatePlayerFishingIk(bool spaceOrE, float dt)
        {
            if (_activePlayer == null || _activePlayer.FootController == null || _activePlayer.FootController.Humanoid == null)
            {
                return;
            }

            Transform pTr = _activePlayer.FootController.transform;
            ProceduralHumanoid hum = _activePlayer.FootController.Humanoid;
            Vector3 fwd = pTr.forward;
            Vector3 right = pTr.right;
            Vector3 basePos = pTr.position;

            Vector3 rHand;
            Vector3 lHand;
            float spineLean;
            float headPitch;
            float hipsDrop;
            bool isForwardFlick = false;

            if (_phase == FishingPhase.Casting)
            {
                // 3-phase cast (0.65 s total): 0–0.38 backswing, 0.38–0.56 forward whip, 0.56–1.0 settle with dip overshoot
                float t = Mathf.Clamp01(_phaseTimer / 0.65f);
                Vector3 rChest = basePos + Vector3.up * 1.12f + right * 0.16f + fwd * 0.30f;
                Vector3 lChest = basePos + Vector3.up * 1.06f - right * 0.06f + fwd * 0.34f;
                Vector3 rBack = basePos + Vector3.up * 1.68f + right * 0.24f - fwd * 0.16f;
                Vector3 lBack = basePos + Vector3.up * 1.54f - right * 0.10f - fwd * 0.10f;
                Vector3 rRelease = basePos + Vector3.up * 1.04f + right * 0.16f + fwd * 0.46f;
                Vector3 lRelease = basePos + Vector3.up * 0.98f - right * 0.06f + fwd * 0.50f;
                Vector3 rHold = basePos + Vector3.up * 1.06f + right * 0.16f + fwd * 0.36f;
                Vector3 lHold = basePos + Vector3.up * 1.02f - right * 0.06f + fwd * 0.44f;

                if (t < 0.38f)
                {
                    float u = Mathf.SmoothStep(0f, 1f, t / 0.38f);
                    rHand = Vector3.Lerp(rChest, rBack, u);
                    lHand = Vector3.Lerp(lChest, lBack, u);
                    spineLean = Mathf.Lerp(2f, -12f, u);
                    headPitch = Mathf.Lerp(4f, -8f, u);
                    hipsDrop = Mathf.Lerp(0.02f, 0.05f, u);
                }
                else if (t < 0.56f)
                {
                    isForwardFlick = true;
                    float lin = (t - 0.38f) / 0.18f;
                    float u = 1f - (1f - lin) * (1f - lin);
                    rHand = Vector3.Lerp(rBack, rRelease, u);
                    lHand = Vector3.Lerp(lBack, lRelease, u);
                    spineLean = Mathf.Lerp(-12f, 18f, u);
                    headPitch = Mathf.Lerp(-8f, 12f, u);
                    hipsDrop = Mathf.Lerp(0.05f, 0.08f, u);
                }
                else
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - 0.56f) / 0.44f);
                    float dip = Mathf.Sin(u * Mathf.PI) * 0.04f;
                    rHand = Vector3.Lerp(rRelease, rHold, u) - Vector3.up * dip;
                    lHand = Vector3.Lerp(lRelease, lHold, u) - Vector3.up * (dip * 0.85f);
                    spineLean = Mathf.Lerp(18f, 6f, u);
                    headPitch = Mathf.Lerp(12f, 10f, u);
                    hipsDrop = Mathf.Lerp(0.08f, 0.03f, u);
                }
            }
            else if (_phase == FishingPhase.WaitingBite)
            {
                float breath = Mathf.Sin(Time.time * 1.8f) * 0.025f;
                float twitchLift = 0f;
                if (_waitTwitchTimer >= 0f)
                {
                    _waitTwitchTimer += dt;
                    float tw = _waitTwitchTimer / 0.25f;
                    if (tw >= 1f)
                    {
                        _waitTwitchTimer = -1f;
                        _waitTwitchCountdown = UnityEngine.Random.Range(4f, 7f);
                    }
                    else
                    {
                        twitchLift = Mathf.Sin(tw * Mathf.PI) * 0.05f;
                    }
                }
                else
                {
                    _waitTwitchCountdown -= dt;
                    if (_waitTwitchCountdown <= 0f)
                    {
                        _waitTwitchTimer = 0f;
                    }
                }

                rHand = basePos + Vector3.up * (1.06f + breath + twitchLift) + right * 0.16f + fwd * 0.36f;
                lHand = basePos + Vector3.up * (1.02f + breath + twitchLift * 0.5f) - right * 0.06f + fwd * 0.44f;
                spineLean = 6f + Mathf.Sin(Time.time * 1.8f) * 0.8f;
                headPitch = Mathf.Lerp(6f, 12f, Mathf.PerlinNoise(Time.time * 0.28f, 1.7f));
                hipsDrop = 0.03f;
            }
            else if (_phase == FishingPhase.BiteReeling)
            {
                // Fish-pull impulse model: random 0.06–0.11 m forward/down jerks every 0.35–0.90 s decaying ~6/s
                _fishPullCountdown -= dt;
                if (_fishPullCountdown <= 0f)
                {
                    _fishPullStrength = Mathf.Max(_fishPullStrength, UnityEngine.Random.Range(0.06f, 0.11f));
                    _fishPullCountdown = UnityEngine.Random.Range(0.35f, 0.90f);
                }
                _fishPullStrength *= Mathf.Exp(-6f * dt);

                bool reeling = _reelButtonHeld || spaceOrE;
                float crank = Time.time * (reeling ? 13f : 4f);
                float pullRatio = Mathf.Clamp01(_fishPullStrength / 0.11f);

                // Athletic lean-back pulling rod while left hand physically orbits the spinning reel handle!
                rHand = basePos
                    + Vector3.up * (1.16f - _fishPullStrength * 0.69f)
                    + right * 0.16f
                    + fwd * (0.32f + _fishPullStrength * 0.72f);
                lHand = rHand - right * 0.12f
                    + fwd * (0.20f + Mathf.Cos(crank) * 0.07f)
                    + Vector3.up * (Mathf.Sin(crank) * 0.07f - 0.04f);
                spineLean = (reeling ? -16f : -9f) - pullRatio * 2.5f;
                headPitch = 4f + pullRatio * 5f;
                hipsDrop = 0.10f + 0.03f * pullRatio;
            }
            else // CatchShowcase — proud 0.4 s lift, 2 happy bounce bells, then hold!
            {
                float liftLin = Mathf.Clamp01(_phaseTimer / 0.40f);
                float lift = 1f - (1f - liftLin) * (1f - liftLin);
                float holdBreath = Mathf.Sin(_phaseTimer * 2.6f) * 0.018f * lift;
                Vector3 rFrom = basePos + Vector3.up * 1.16f + right * 0.16f + fwd * 0.32f;
                Vector3 lFrom = basePos + Vector3.up * 1.10f - right * 0.04f + fwd * 0.40f;
                Vector3 rProud = basePos + Vector3.up * (1.48f + holdBreath) + right * 0.22f + fwd * 0.32f;
                Vector3 lProud = basePos + Vector3.up * (1.40f + holdBreath) - right * 0.16f + fwd * 0.36f;
                rHand = Vector3.Lerp(rFrom, rProud, lift);
                lHand = Vector3.Lerp(lFrom, lProud, lift);
                spineLean = Mathf.Lerp(-4f, -8f, lift);
                headPitch = Mathf.Lerp(2f, -6f, lift);

                float bounce = 0f;
                if (_phaseTimer >= 0.35f && _phaseTimer < 1.05f)
                {
                    float bNorm = (_phaseTimer - 0.35f) / 0.70f;
                    bounce = Mathf.Abs(Mathf.Sin(bNorm * Mathf.PI * 2f)) * 0.04f;
                }
                hipsDrop = bounce;
            }

            if (!_smInit)
            {
                _smR = rHand;
                _smL = lHand;
                _smSpine = spineLean;
                _smHead = headPitch;
                _smDrop = hipsDrop;
                _smInit = true;
            }
            else
            {
                float smoothRate = isForwardFlick ? 30f : 14f;
                float k = 1f - Mathf.Exp(-smoothRate * dt);
                _smR = Vector3.Lerp(_smR, rHand, k);
                _smL = Vector3.Lerp(_smL, lHand, k);
                _smSpine = Mathf.Lerp(_smSpine, spineLean, k);
                _smHead = Mathf.Lerp(_smHead, headPitch, k);
                _smDrop = Mathf.Lerp(_smDrop, hipsDrop, k);
            }

            _lastRightHandTarget = _smR;
            hum.SetDualHandAction(true, _smL, _smR, 0.95f, 1.0f, _smSpine, _smHead, _smDrop);
            hum.SetLocomotion(0f, true, 0f, fwd);
        }

        private void PickHookedFish()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.34f) _hookedFish = Species[0];
            else if (r < 0.62f) _hookedFish = Species[1];
            else if (r < 0.82f) _hookedFish = Species[2];
            else if (r < 0.94f) _hookedFish = Species[3];
            else _hookedFish = Species[4];
        }

        private void CompleteFishCatch()
        {
            _phase = FishingPhase.CatchShowcase;
            _phaseTimer = 0f;
            _reelButtonHeld = false;

            string fName = IsEn ? _hookedFish.NameEn : _hookedFish.NameTr;
            FarmEconomyManager.AddCaughtFish(fName, _hookedFish.PriceTl);
            NeighborhoodReputation.AddReputation(6, IsEn ? "Lake Fishing" : "Gölet Balıkçılığı");

            GameFeelAudio.PlayCoinChime();

            // Compute open-water breach & re-entry spots cleanly west of the wooden pier deck (west edge x = -183.0)
            float startX = (_bobberObj != null && _bobberObj.activeSelf)
                ? Mathf.Clamp(_bobberObj.transform.position.x - 1.1f, -188.5f, -185.2f)
                : -186.4f;
            float startZ = (_bobberObj != null && _bobberObj.activeSelf)
                ? Mathf.Clamp(_bobberObj.transform.position.z, -2.0f, 2.0f)
                : Mathf.Clamp(_bobberAnchor.z * 0.55f, -2.0f, 2.0f);
            if (Mathf.Abs(startZ) < 0.45f) startZ = startZ >= 0f ? 0.85f : -0.85f;
            _fishLeapStart = new Vector3(startX, -0.22f, startZ);
            _fishLeapEnd = new Vector3(-183.9f, -0.30f, Mathf.Clamp(-startZ * 0.75f, -2.0f, 2.0f));
            _fishArcHeight = 2.05f + (_hookedFish.SizeScale - 1f) * 0.45f;
            _fishExitSplashed = false;
            _fishEntrySplashed = false;
            _fishApexSparkled = false;

            // Spawn & tint our real rigged Blender/CC0 OW_LakeFish with skeletal Swim clip!
            if (_leapingFishObj == null)
            {
                _leapingFishObj = OurWorldModelLibrary.InstantiateModel("OW_LakeFish", transform, _fishLeapStart, Quaternion.identity);
                if (_leapingFishObj != null)
                {
                    SkeletalClipPlayer fishSkel = _leapingFishObj.AddComponent<SkeletalClipPlayer>();
                    if (!fishSkel.Initialize("OW_LakeFish", "Swim", 0f))
                    {
                        _leapingFishSkel = null;
                        _leapingFishTail = OurWorldModelLibrary.FindPart(_leapingFishObj.transform, "TailFinPivot");
                    }
                    else
                    {
                        _leapingFishSkel = fishSkel;
                        fishSkel.PlayClip("Swim", 0.05f, 1.8f);
                    }
                }
            }
            if (_leapingFishObj != null)
            {
                Vector3 initVel = (_fishLeapEnd - _fishLeapStart) / 1.92f;
                initVel.y += (4f * _fishArcHeight) / 1.92f;
                _leapingFishObj.transform.position = _fishLeapStart;
                if (initVel.sqrMagnitude > 0.001f)
                {
                    _leapingFishObj.transform.rotation = Quaternion.LookRotation(initVel.normalized, Vector3.up);
                }
                _leapingFishObj.SetActive(true);
                _leapingFishObj.transform.localScale = Vector3.one * _hookedFish.SizeScale;
                OurWorldModelLibrary.TintMaterialByKeyword(_leapingFishObj, "FishScale", _hookedFish.ScaleCol);
                OurWorldModelLibrary.TintMaterialByKeyword(_leapingFishObj, "FishFin", _hookedFish.FinCol);
            }
            if (_bobberObj != null) _bobberObj.SetActive(false);

            string toast = IsEn
                ? $"Caught {fName}! (Value: {_hookedFish.PriceTl} ₺ — sell at market or grill on Mangal!)"
                : $"{fName} tuttun! (Değeri: {_hookedFish.PriceTl} ₺ — pazarda sat ya da No:48 mangalda pişir!)";
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(toast);
            }
            UpdateFishingUi();
        }

        private void UpdateFishingUi()
        {
            if (_hudCard == null) return;
            bool en = IsEn;
            _hudTitle.text = en ? "SULTANBEYLİ LAKE FISHING" : "SULTANBEYLİ GÖLETİ BALIKÇILIK";

            if (_phase == FishingPhase.Casting || _phase == FishingPhase.WaitingBite)
            {
                _hudStatus.text = en ? "Bobber floating... wait for the splash!" : "Şamandıra suda... Balık vurmasını bekle!";
                _reelBtnLabel.text = en ? "WAITING..." : "BEKLE...";
                _reelBtn.interactable = false;
                _reelBarFill.rectTransform.anchorMax = new Vector2(0.08f, 1f);
            }
            else if (_phase == FishingPhase.BiteReeling)
            {
                _hudStatus.text = en ? "FISH ON THE LINE! TAP / HOLD TO REEL IN!" : "BALIK VURDU! HIZLICA BAS / BASILI TUT!";
                _reelBtnLabel.text = en ? "REEL IN!" : "OLTAYI ÇEK!";
                _reelBtn.interactable = true;
                _reelBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_reelProgress), 1f);
            }
            else if (_phase == FishingPhase.CatchShowcase)
            {
                string nm = en ? _hookedFish.NameEn : _hookedFish.NameTr;
                _hudStatus.text = en ? $"CAUGHT: {nm} (+{_hookedFish.PriceTl} ₺ value!)" : $"YAKALANDI: {nm} ({_hookedFish.PriceTl} ₺ değerinde!)";
                _reelBtnLabel.text = en ? "NICE CATCH!" : "HARİKA AV!";
                _reelBtn.interactable = false;
                _reelBarFill.rectTransform.anchorMax = Vector2.one;
            }
        }

        private void BuildPierFishingStand()
        {
            GameObject stand = new GameObject("Sultanbeyli_FishingPierSpot");
            stand.transform.SetParent(transform, false);
            stand.transform.position = PierTipWorldPos;

            // Display our Blender OW_FishingRod resting upright beside a tackle bucket on the pier!
            OurWorldModelLibrary.InstantiateModel("OW_FishingRod", stand.transform,
                new Vector3(0.4f, 0.12f, 1.35f), Quaternion.Euler(-76f, -35f, 0f));

            // Floating red/white bobber prefab ready for casting
            _bobberObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _bobberObj.name = "FishingBobber";
            _bobberObj.transform.SetParent(transform, false);
            _bobberObj.transform.localScale = new Vector3(0.24f, 0.30f, 0.24f);
            Destroy(_bobberObj.GetComponent<Collider>());
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material bm = new Material(lit);
            _bobberMat = bm;
            if (bm.HasProperty("_BaseColor")) bm.SetColor("_BaseColor", new Color(0.95f, 0.18f, 0.16f));
            _bobberObj.GetComponent<MeshRenderer>().sharedMaterial = bm;
            _bobberObj.SetActive(false);

            InteractableTrigger trig = stand.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FishingPier, "sultanbeyli_lake_fishing_pier", "ui.btn.talk", 3.8f);
            trig.SetCustomPrompt(IsEn ? "Go Fishing · Lake Pier" : "Balık Tut · Gölet İskelesi");
        }

        private void BuildFishingHud()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 22);

            _uiCanvas = new GameObject("Canvas_OurWorld_FishingHud");
            _uiCanvas.layer = LayerMask.NameToLayer("UI");
            _uiCanvas.transform.SetParent(transform, false);
            Canvas c = _uiCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 188;

            CanvasScaler cs = _uiCanvas.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            _uiCanvas.AddComponent<GraphicRaycaster>();

            _hudCard = new GameObject("FishingHudCard", typeof(RectTransform), typeof(Image));
            _hudCard.transform.SetParent(_uiCanvas.transform, false);
            RectTransform rt = (RectTransform)_hudCard.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 38f);
            rt.sizeDelta = new Vector2(860f, 250f);
            _hudCard.GetComponent<Image>().color = new Color(0.048f, 0.085f, 0.138f, 0.94f);
            Outline o = _hudCard.AddComponent<Outline>();
            o.effectColor = new Color(0.16f, 0.80f, 0.84f, 0.65f);
            o.effectDistance = new Vector2(2f, -2f);

            _hudTitle = MakeText(_hudCard.transform, font, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(-48f, 36f), 22, new Color(0.24f, 0.88f, 0.86f), TextAnchor.MiddleLeft);
            _hudStatus = MakeText(_hudCard.transform, font, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(-48f, 40f), 25, Color.white, TextAnchor.MiddleLeft);

            // Progress bar
            GameObject bg = new GameObject("ReelBarBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_hudCard.transform, false);
            RectTransform bgrt = (RectTransform)bg.transform;
            bgrt.anchorMin = new Vector2(0f, 1f);
            bgrt.anchorMax = new Vector2(1f, 1f);
            bgrt.pivot = new Vector2(0.5f, 1f);
            bgrt.anchoredPosition = new Vector2(0f, -104f);
            bgrt.sizeDelta = new Vector2(-48f, 30f);
            bg.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.24f, 1f);

            GameObject fill = new GameObject("ReelBarFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0.2f, 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _reelBarFill = fill.GetComponent<Image>();
            _reelBarFill.color = new Color(0.16f, 0.82f, 0.64f, 1f);

            // Big mobile REEL IN button (supports both tap & hold!)
            GameObject rGo = new GameObject("BtnReelIn", typeof(RectTransform), typeof(Image), typeof(Button));
            rGo.transform.SetParent(_hudCard.transform, false);
            RectTransform rrt = (RectTransform)rGo.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(0f, 0f);
            rrt.pivot = new Vector2(0f, 0f);
            rrt.anchoredPosition = new Vector2(24f, 20f);
            rrt.sizeDelta = new Vector2(580f, 82f);
            rGo.GetComponent<Image>().color = new Color(0.12f, 0.68f, 0.64f, 1f);
            _reelBtn = rGo.GetComponent<Button>();
            _reelBtn.onClick.AddListener(() =>
            {
                if (_phase == FishingPhase.BiteReeling)
                {
                    _reelProgress += 0.22f;
                    GameFeelAudio.PlayUiClick();
                }
            });
            _reelBtnLabel = MakeText(rGo.transform, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, Color.white, TextAnchor.MiddleCenter);

            // Exit Fishing button
            GameObject eGo = new GameObject("BtnExitFish", typeof(RectTransform), typeof(Image), typeof(Button));
            eGo.transform.SetParent(_hudCard.transform, false);
            RectTransform ert = (RectTransform)eGo.transform;
            ert.anchorMin = new Vector2(1f, 0f);
            ert.anchorMax = new Vector2(1f, 0f);
            ert.pivot = new Vector2(1f, 0f);
            ert.anchoredPosition = new Vector2(-24f, 20f);
            ert.sizeDelta = new Vector2(210f, 82f);
            eGo.GetComponent<Image>().color = new Color(0.70f, 0.24f, 0.24f, 0.95f);
            eGo.GetComponent<Button>().onClick.AddListener(StopFishing);
            MakeText(eGo.transform, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24, Color.white, TextAnchor.MiddleCenter).text = "ÇIKIŞ";

            _hudCard.SetActive(false);
        }

        private static Text MakeText(Transform parent, Font font, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fs, Color col, TextAnchor align)
        {
            GameObject go = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, aMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fs;
            t.fontStyle = FontStyle.Bold;
            t.color = col;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
    }
}
