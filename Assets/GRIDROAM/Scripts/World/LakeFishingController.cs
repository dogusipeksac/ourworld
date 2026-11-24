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
        private Transform _leapingFishTail;
        private FishSpecies _hookedFish;

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

        private void OnDestroy()
        {
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
                        float castAngle = Mathf.Lerp(45f, -14f, Mathf.SmoothStep(0f, 1f, t));
                        _activeRodRoot.transform.rotation = Quaternion.Euler(castAngle, 90f, 0f);
                    }
                    if (t >= 1f)
                    {
                        _phase = FishingPhase.WaitingBite;
                        _phaseTimer = 0f;
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
                        _activeRodRoot.transform.rotation = Quaternion.Euler(-12f + Mathf.Sin(Time.time * 1.4f) * 2.5f, 90f, 0f);
                    }
                    if (_phaseTimer >= _biteWaitDuration)
                    {
                        PickHookedFish();
                        _phase = FishingPhase.BiteReeling;
                        _phaseTimer = 0f;
                        _reelProgress = 0.28f;
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

                    // Dynamic rod flex & bobber thrashing!
                    if (_activeRodRoot != null)
                    {
                        float flex = reeling ? (-24f + Mathf.Sin(Time.time * 24f) * 5f) : (-16f + Mathf.Sin(Time.time * 14f) * 3f);
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
                    // Parabolic leaping 3D Blender fish with frantic TailFinPivot wiggle!
                    float t = Mathf.Clamp01(_phaseTimer / 2.2f);
                    if (_leapingFishObj != null)
                    {
                        Vector3 start = _bobberAnchor + Vector3.up * 0.2f;
                        Vector3 end = PierTipWorldPos + new Vector3(-1.4f, 1.35f, 0f);
                        Vector3 p = Vector3.Lerp(start, end, Mathf.Clamp01(t * 2.1f));
                        p.y += Mathf.Sin(Mathf.Clamp01(t * 2.1f) * Mathf.PI) * 2.4f;
                        _leapingFishObj.transform.position = p;
                        _leapingFishObj.transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 8f) * 18f, 90f, 0f);
                    }
                    if (_leapingFishTail != null)
                    {
                        _leapingFishTail.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 34f) * 34f, 0f);
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

            UpdatePlayerFishingIk(spaceOrE);
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

        private void UpdatePlayerFishingIk(bool spaceOrE)
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

            if (_phase == FishingPhase.Casting)
            {
                float t = Mathf.Clamp01(_phaseTimer / 0.65f);
                float whip = Mathf.SmoothStep(0f, 1f, t);
                rHand = Vector3.Lerp(
                    basePos + Vector3.up * 1.66f + right * 0.24f - fwd * 0.14f,
                    basePos + Vector3.up * 1.18f + right * 0.14f + fwd * 0.44f,
                    whip);
                lHand = Vector3.Lerp(
                    basePos + Vector3.up * 1.54f - right * 0.12f - fwd * 0.08f,
                    basePos + Vector3.up * 1.10f - right * 0.04f + fwd * 0.36f,
                    whip);
                spineLean = Mathf.Lerp(-12f, 18f, whip);
                headPitch = Mathf.Lerp(-8f, 10f, whip);
                hipsDrop = 0.05f * Mathf.Sin(whip * Mathf.PI);
            }
            else if (_phase == FishingPhase.WaitingBite)
            {
                float breath = Mathf.Sin(Time.time * 1.8f) * 0.025f;
                rHand = basePos + Vector3.up * (1.06f + breath) + right * 0.16f + fwd * 0.36f;
                lHand = basePos + Vector3.up * (1.02f + breath) - right * 0.06f + fwd * 0.44f;
                spineLean = 6f;
                headPitch = 10f;
                hipsDrop = 0.03f;
            }
            else if (_phase == FishingPhase.BiteReeling)
            {
                bool reeling = _reelButtonHeld || spaceOrE;
                float tug = Mathf.Sin(Time.time * 16f) * 0.04f;
                float crank = Time.time * (reeling ? 13f : 4f);
                // Athletic lean-back pulling rod while left hand physically orbits the spinning reel handle!
                rHand = basePos + Vector3.up * (1.16f + tug) + right * 0.16f + fwd * 0.32f;
                lHand = rHand - right * 0.12f + fwd * (0.20f + Mathf.Cos(crank) * 0.07f) + Vector3.up * (Mathf.Sin(crank) * 0.07f - 0.04f);
                spineLean = reeling ? -16f : -9f;
                headPitch = 4f;
                hipsDrop = 0.10f;
            }
            else // CatchShowcase — proud fisherman triumph pose!
            {
                float wave = Mathf.Sin(_phaseTimer * 5f) * 0.05f;
                rHand = basePos + Vector3.up * (1.46f + wave) + right * 0.22f + fwd * 0.32f;
                lHand = basePos + Vector3.up * (1.38f - wave) - right * 0.16f + fwd * 0.36f;
                spineLean = -8f;
                headPitch = -6f;
                hipsDrop = 0f;
            }

            _lastRightHandTarget = rHand;
            hum.SetDualHandAction(true, lHand, rHand, 0.95f, 1.0f, spineLean, headPitch, hipsDrop);
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
            GameFeelAudio.SpawnBurst(PierTipWorldPos + new Vector3(-2.5f, 1.4f, 0f), _hookedFish.ScaleCol, count: 20, spread: 0.9f);

            // Spawn & tint our real rigged Blender/CC0 OW_LakeFish with skeletal Swim clip!
            if (_leapingFishObj == null)
            {
                _leapingFishObj = OurWorldModelLibrary.InstantiateModel("OW_LakeFish", transform, PierTipWorldPos, Quaternion.identity);
                if (_leapingFishObj != null)
                {
                    SkeletalClipPlayer fishSkel = _leapingFishObj.AddComponent<SkeletalClipPlayer>();
                    if (!fishSkel.Initialize("OW_LakeFish", "Swim", 0f))
                    {
                        _leapingFishTail = OurWorldModelLibrary.FindPart(_leapingFishObj.transform, "TailFinPivot");
                    }
                    else
                    {
                        fishSkel.PlayClip("Swim", 0.05f, 1.8f);
                    }
                }
            }
            if (_leapingFishObj != null)
            {
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
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material bm = new Material(lit);
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
