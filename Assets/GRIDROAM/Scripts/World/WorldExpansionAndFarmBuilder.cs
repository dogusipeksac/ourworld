using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Expands & beautifies the Our World open-world map at runtime (zero scene rebuild required!):
    /// 1. Expands the lush valley ground to 660x660 m + rings the 360-degree horizon with 28 rolling green Aydos Hills
    ///    and hilltop pine silhouettes (eliminates flat empty horizon edges!).
    /// 2. Builds EAST EXPANSION: Aydos Yeşil Vadi · Çiftlik & Bostan (country road from Selman St., wooden archway,
    ///    12 raised soil farm plots, stone water well, rotating windmill, red timber barn + Cow Pasture,
    ///    Chicken Coop & Run, Sheep Pen + Anatolian shepherd dog Karabaş, Village Market Stand, Farm Management Board,
    ///    tea pergola, tractor decor, and warm evening farm lanterns).
    /// 3. Builds HOME GARDEN BOSTAN (4 starter plots right beside Deniz's No:48 house!).
    /// 4. Builds WEST EXPANSION: Sultanbeyli Göleti & Aydos Çam Korusu (shimmering turquoise lake, lily pads & lotuses,
    ///    3 animated gliding ducks, wooden viewing pier with benches & lanterns, pine/blossom grove & picnic gazebos).
    /// 5. Beautifies existing Hamidiye plazas & avenues with spring cherry-blossom trees, flowerbeds, cypresses & signposts.
    /// </summary>
    public sealed class WorldExpansionAndFarmBuilder : MonoBehaviour
    {
        public static WorldExpansionAndFarmBuilder Instance { get; private set; }

        private Material _meadowMat;
        private Material _hillNearMat;
        private Material _hillFarMat;
        private Material _roadCountryMat;
        private Material _stonePathMat;
        private Material _woodTimberMat;
        private Material _woodLightMat;
        private Material _barnRedMat;
        private Material _roofTileMat;
        private Material _whiteTrimMat;
        private Material _drySoilMat;
        private Material _wetSoilMat;
        private Material _hayGoldMat;
        private Material _pineDarkMat;
        private Material _leafBrightMat;
        private Material _blossomPinkMat;
        private Material _flowerRedMat;
        private Material _flowerYellowMat;
        private Material _flowerPurpleMat;
        private Material _lakeWaterMat;
        private Material _sandShoreMat;
        private Material _tractorRedMat;
        private Material _darkMetalMat;

        private Transform _windmillBlades;
        private Transform _lakeSurface;
        private readonly List<(Transform duck, SkeletalClipPlayer skel, Vector3 center, float radius, float speed, float phase)> _ducks = new List<(Transform, SkeletalClipPlayer, Vector3, float, float, float)>();
        private readonly List<float> _duckAngles = new List<float>(4);
        private readonly List<float> _duckSpeedScales = new List<float>(4);
        private readonly List<bool> _duckInitialized = new List<bool>(4);
        private readonly List<Transform> _sprinklerHeads = new List<Transform>();
        private static readonly float[] DogTrotPhase = { 0f, 0.5f, 0.5f, 0f };
        private static readonly RaycastHit[] DogHits = new RaycastHit[4];
        private Transform _shepherdDog;
        private SkeletalClipPlayer _shepherdSkel;
        private Transform _shepherdHead;
        private Transform _shepherdTail;
        private readonly List<Transform> _shepherdLegs = new List<Transform>(4);
        private Vector3 _dogHome;
        private float _dogPhase;
        private float _dogWalkPhase;
        private float _dogCurrentSpeed;
        private float _dogWalkBlend;
        private bool _dogMoving;
        private bool _dogGalloping;
        private float _dogAngryTimer;
        private float _dogBiteCooldown;
        private float _dogHitCooldown;
        private float _duckHitCooldown;

        public static void CalmShepherdDog()
        {
            if (Instance != null)
            {
                Instance._dogAngryTimer = 0f;
                Instance._dogBiteCooldown = 0f;
            }
        }

        public void ProvokeShepherdDog(Vector3 attackerPos, bool defendingFarm = false)
        {
            if (_shepherdDog == null) return;
            _dogAngryTimer = 15.0f;
            _dogBiteCooldown = Mathf.Min(_dogBiteCooldown, 0.45f);
            NeighborhoodConsequenceSystem.EnsureInstance().PlayDogBark(0.80f);
            bool en = LocalizationService.IsEnglish;
            string line = defendingFarm
                ? (en ? "WOOF! GRRR!! (Defending the farm!)" : "HAV! HAV!! (Çiftliği koruyor!)")
                : (en ? "GRRR!! WOOF!! 🐾" : "HAV! HIRRR!! 🐾");
            NpcSpeechBubbleManager.Say(_shepherdDog, "Karabaş", line, 3.0f, NpcBubbleMood.Alert, 1.35f);
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            bool en = LocalizationService.IsEnglish;

            // 1. Check Anatolian Shepherd Dog Karabaş!
            if (_shepherdDog != null && _dogHitCooldown <= 0f)
            {
                Vector3 d = (_shepherdDog.position + Vector3.up * 0.65f) - strikeCenter;
                if (Mathf.Abs(d.y) < 1.6f)
                {
                    d.y = 0f;
                    float maxR = radius + 0.68f;
                    if (d.sqrMagnitude <= maxR * maxR)
                    {
                        _dogHitCooldown = 0.40f;
                        ProvokeShepherdDog(attackerPos, defendingFarm: false);
                        PlayerModeController.Instance?.RaiseStatusMessage(en
                            ? "🐕 Karabaş is furious and charging at you! (-14 Reputation)"
                            : "🐕 Çoban köpeği Karabaş öfkeyle üzerine saldırıyor! (-14 Saygınlık)");
                        NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                            NeighborhoodOffenseKind.HarmAnimal, _shepherdDog.position, "Karabaş", repPenalty: 14, immediateFineTl: 0, heatDelta: 1.3f);
                        return true;
                    }
                }
            }

            // 2. Check Sultanbeyli Lake Pond Ducks!
            if (_duckHitCooldown <= 0f)
            {
                float duckR = radius + 0.65f;
                for (int i = 0; i < _ducks.Count; i++)
                {
                    Transform duck = _ducks[i].duck;
                    if (duck == null) continue;
                    Vector3 d = (duck.position + Vector3.up * 0.25f) - strikeCenter;
                    if (Mathf.Abs(d.y) > 1.5f) continue;
                    d.y = 0f;
                    if (d.sqrMagnitude <= duckR * duckR)
                    {
                        _duckHitCooldown = 0.55f;
                        _duckAngles[i] += 0.95f;
                        VisualEffectsManager.SpawnWaterSplash(duck.position, bigDive: false);
                        NeighborhoodConsequenceSystem.EnsureInstance().PlayFarmAlarm(0.55f);
                        NpcSpeechBubbleManager.Say(duck, en ? "Pond Duck" : "Gölet Ördeği", en ? "QUACK!! 🦆" : "VAK VAK!! 🦆", 2.4f, NpcBubbleMood.Alert, 0.85f);
                        PlayerModeController.Instance?.RaiseStatusMessage(en
                            ? "⚠️ You startled the pond duck! (-5 Reputation)"
                            : "⚠️ Gölet ördeğini ürküttün! (-5 Saygınlık)");
                        NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                            NeighborhoodOffenseKind.HarmAnimal, duck.position, en ? "Pond Duck" : "Gölet Ördeği", repPenalty: 5, immediateFineTl: 0, heatDelta: 0.6f);
                        return true;
                    }
                }
            }

            return false;
        }

        private Transform _farmRoot;
        private int _spawnedChickens;
        private int _spawnedSheep;
        private int _spawnedCows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_ExpandedValleyAndFarm");
            go.AddComponent<WorldExpansionAndFarmBuilder>();
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
            ClearLegacyTightPerimeterWalls();
            BuildMaterials();
            BuildValleyGroundAndAydosHills();
            BuildHomeGardenBostan();
            BuildEastAydosFarmValley();
            BuildWestSultanbeyliLakeAndGrove();
            BeautifyHamidiyePlazasAndAvenues();
            RedistributeClusteredSelmanCars();

            FarmEconomyManager.OnExtraAnimalPurchased += HandleExtraAnimalPurchased;
            FarmEconomyManager.OnAutoSprinklerUnlocked += HandleSprinklerUnlocked;

            // Refresh PlayerModeController interactable cache so every new plot, animal & stand is immediately interactive!
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private readonly System.Collections.Generic.List<Material> _runtimeMats =
            new System.Collections.Generic.List<Material>(32);
        private readonly System.Collections.Generic.List<Mesh> _runtimeMeshes =
            new System.Collections.Generic.List<Mesh>(8);
        private readonly System.Collections.Generic.List<Texture2D> _runtimeTextures =
            new System.Collections.Generic.List<Texture2D>(8);

        private void OnDestroy()
        {
            FarmEconomyManager.OnExtraAnimalPurchased -= HandleExtraAnimalPurchased;
            FarmEconomyManager.OnAutoSprinklerUnlocked -= HandleSprinklerUnlocked;
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] != null) Destroy(_runtimeMats[i]);
            }
            for (int i = 0; i < _runtimeMeshes.Count; i++)
            {
                if (_runtimeMeshes[i] != null) Destroy(_runtimeMeshes[i]);
            }
            for (int i = 0; i < _runtimeTextures.Count; i++)
            {
                if (_runtimeTextures[i] != null) Destroy(_runtimeTextures[i]);
            }
            _runtimeMats.Clear();
            _runtimeMeshes.Clear();
            _runtimeTextures.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_windmillBlades != null)
            {
                _windmillBlades.Rotate(0f, 0f, -26f * dt, Space.Self);
            }

            if (_lakeSurface != null)
            {
                Vector3 p = _lakeSurface.localPosition;
                p.y = 0.04f + Mathf.Sin(Time.time * 1.4f) * 0.018f;
                _lakeSurface.localPosition = p;
                if (_lakeWaterMat != null && _lakeWaterMat.HasProperty("_BaseMap"))
                {
                    _lakeWaterMat.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.022f, Time.time * 0.015f));
                }
            }

            // Gliding & dabbling pond ducks (smooth velocity-aligned facing, dabble slowdown, pier clearance & buoyancy!)
            while (_duckAngles.Count < _ducks.Count)
            {
                int idx = _duckAngles.Count;
                _duckAngles.Add(_ducks[idx].phase);
                _duckSpeedScales.Add(1f);
                _duckInitialized.Add(false);
            }
            for (int i = 0; i < _ducks.Count; i++)
            {
                var d = _ducks[i];
                if (d.duck == null) continue;
                float dabbleCycle = Mathf.Repeat(Time.time * 0.22f + d.phase, 1f);
                bool dabbling = dabbleCycle < 0.24f;
                float speedScale = Mathf.Lerp(_duckSpeedScales[i], dabbling ? 0.24f : 1.0f, 1f - Mathf.Exp(-4.5f * dt));
                _duckSpeedScales[i] = speedScale;
                float a = _duckAngles[i] + dt * d.speed * speedScale;
                _duckAngles[i] = a;

                float bob = Mathf.Sin(a * 3f) * 0.022f;
                Vector3 pos = d.center + new Vector3(Mathf.Cos(a) * d.radius, 0.09f + bob, Mathf.Sin(a) * d.radius * 0.72f);

                // Smoothly deflect west of wooden viewing pier (x = -183..-168, z = -2.1..+2.1) so ducks never clip pier pilings!
                if (pos.x > -184.6f)
                {
                    float pierZone = 1f - Mathf.SmoothStep(2.2f, 4.4f, Mathf.Abs(pos.z));
                    pos.x = Mathf.Lerp(pos.x, -184.6f, pierZone);
                }

                if (!_duckInitialized[i])
                {
                    _duckInitialized[i] = true;
                    d.duck.position = pos;
                    Vector3 initDir = new Vector3(-Mathf.Sin(a) * d.radius, 0f, Mathf.Cos(a) * d.radius * 0.72f);
                    if (initDir.sqrMagnitude > 0.0001f)
                    {
                        d.duck.rotation = Quaternion.LookRotation(initDir.normalized, Vector3.up);
                    }
                    continue;
                }

                Vector3 prevPos = d.duck.position;
                d.duck.position = pos;
                Vector3 vel = pos - prevPos;
                vel.y = 0f;
                float waterSpd = vel.magnitude / dt;
                if (waterSpd > 0.02f)
                {
                    Quaternion yawRot = Quaternion.LookRotation(vel.normalized, Vector3.up);
                    if (d.skel != null && d.skel.HasClips)
                    {
                        float swimClipSpd = Mathf.Clamp(waterSpd / 2.2f, 0.7f, 1.35f);
                        d.skel.PlayClip(dabbling ? "Dabble" : "Swim", 0.30f, dabbling ? 1.0f : swimClipSpd);
                        float gentlePitch = Mathf.Sin(a * 2.4f) * 2.4f;
                        float gentleRoll = Mathf.Cos(a * 2.1f) * 3.2f;
                        d.duck.rotation = Quaternion.Slerp(d.duck.rotation, yawRot * Quaternion.Euler(gentlePitch, 0f, gentleRoll), 1f - Mathf.Exp(-6f * dt));
                    }
                    else
                    {
                        float dipPitch = dabbling ? Mathf.Sin((dabbleCycle / 0.24f) * Mathf.PI) * 26f : Mathf.Sin(a * 2.4f) * 3.5f;
                        float roll = Mathf.Cos(a * 2.1f) * 5.5f;
                        d.duck.rotation = Quaternion.Slerp(d.duck.rotation, yawRot * Quaternion.Euler(dipPitch, 0f, roll), 1f - Mathf.Exp(-7.5f * dt));
                    }
                }
            }

            // Anatolian shepherd dog Karabaş: defends the farm furiously when provoked, otherwise greets Deniz!
            if (_dogHitCooldown > 0f) _dogHitCooldown -= dt;
            if (_duckHitCooldown > 0f) _duckHitCooldown -= dt;
            if (_dogBiteCooldown > 0f) _dogBiteCooldown -= dt;

            if (_shepherdDog != null)
            {
                bool dogChasing = false;
                if (_dogAngryTimer > 0f)
                {
                    _dogAngryTimer -= dt;
                    PlayerModeController pm = PlayerModeController.Instance;
                    bool stopChase = pm == null || pm.IsModalLocked || pm.CurrentMode != ControlMode.OnFoot ||
                                     (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsKnockedOut);
                    if (stopChase)
                    {
                        _dogAngryTimer = 0f;
                    }
                    else
                    {
                        Vector3 pPos = pm.ActiveWorldPosition;
                        Vector3 toP = pPos - _shepherdDog.position;
                        toP.y = 0f;
                        float pDist = toP.magnitude;
                        if (pDist > 26f)
                        {
                            _dogAngryTimer = 0f;
                        }
                        else
                        {
                            dogChasing = true;
                            if (pDist > 0.05f)
                            {
                                Vector3 dir = toP / pDist;
                                _shepherdDog.rotation = Quaternion.Slerp(_shepherdDog.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-12f * dt));
                                if (pDist > 1.25f)
                                {
                                    Vector3 next = _shepherdDog.position + dir * (4.85f * dt);
                                    next.y = _dogHome.y;
                                    _shepherdDog.position = next;
                                    if (_shepherdSkel != null && _shepherdSkel.HasClips)
                                    {
                                        _shepherdSkel.PlayClip("Gallop", 0.14f, 1.45f);
                                    }
                                    else
                                    {
                                        _dogWalkPhase += dt * 16f;
                                        for (int i = 0; i < _shepherdLegs.Count; i++)
                                        {
                                            if (_shepherdLegs[i] == null) continue;
                                            float ang = Mathf.Sin(_dogWalkPhase + DogTrotPhase[i] * Mathf.PI * 2f) * 38f;
                                            _shepherdLegs[i].localRotation = Quaternion.Euler(ang, 0f, 0f);
                                        }
                                    }
                                }
                                else if (_dogBiteCooldown <= 0f)
                                {
                                    _dogBiteCooldown = 1.45f;
                                    NeighborhoodConsequenceSystem.EnsureInstance().PlayDogBark(0.82f);
                                    bool enB = LocalizationService.IsEnglish;
                                    NpcSpeechBubbleManager.Say(_shepherdDog, "Karabaş", enB ? "CHOMP!! GRRR!!" : "HAV!! ISIRDI!!", 2.0f, NpcBubbleMood.Alert, 1.35f);
                                    PlayerNeedsController.Instance?.TakeDamage(18f, _shepherdDog.position, enB ? "Karabaş (Guard Dog)" : "Çoban Köpeği Karabaş", heavyHit: true);
                                }
                            }
                        }
                    }
                }

                if (!dogChasing)
                {
                _dogPhase += dt * 0.52f;
                // Keep patrol safely between chicken coop fence (x=170) and sheep pen fence (x=180)
                Vector3 target = _dogHome + new Vector3(Mathf.Sin(_dogPhase) * 2.0f - 1.2f, 0f, Mathf.Cos(_dogPhase * 0.7f) * 3.4f);
                bool greetingPlayer = false;
                if (PlayerModeController.Instance != null)
                {
                    Vector3 pPos = PlayerModeController.Instance.ActiveWorldPosition;
                    Vector3 fromHome = pPos - _dogHome;
                    fromHome.y = 0f;
                    if (fromHome.sqrMagnitude < 14f * 14f)
                    {
                        greetingPlayer = true;
                        Vector3 greetSpot = _dogHome + Vector3.ClampMagnitude(fromHome, 6.5f);
                        Vector3 toP = pPos - greetSpot;
                        toP.y = 0f;
                        float toPMag = toP.magnitude;
                        if (toPMag < 1.8f && toPMag > 0.001f) greetSpot = pPos - (toP / toPMag) * 1.8f;
                        // Keep clear of chicken fence (x=170) and solid sheep fence (x=180, open gate at z=-19.6..-16.4)
                        bool inSheepGate = greetSpot.z >= -19.6f && greetSpot.z <= -16.4f;
                        greetSpot.x = Mathf.Clamp(greetSpot.x, 171.2f, inSheepGate ? 184f : 179.0f);
                        target = new Vector3(greetSpot.x, _dogHome.y, greetSpot.z);
                    }
                }

                Vector3 delta = target - _shepherdDog.position;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (!_dogMoving && dist > 0.36f) _dogMoving = true;
                else if (_dogMoving && dist < 0.16f) _dogMoving = false;

                if (!_dogGalloping && greetingPlayer && dist > 2.6f) _dogGalloping = true;
                else if (_dogGalloping && (!greetingPlayer || dist < 1.7f)) _dogGalloping = false;

                float desiredDogSpd = 0f;
                if (_dogMoving && dist > 0.02f)
                {
                    Vector3 dir = delta / dist;
                    float turnRate = greetingPlayer ? 7.5f : 5.8f;
                    _shepherdDog.rotation = Quaternion.Slerp(_shepherdDog.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-turnRate * dt));

                    float alignDot = Vector3.Dot(_shepherdDog.forward, dir);
                    float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.90f, alignDot));
                    float arriveGate = Mathf.Clamp01((dist - 0.10f) / 0.85f);
                    float maxDogSpd = _dogGalloping ? 2.65f : (greetingPlayer ? 1.85f : 1.45f);
                    desiredDogSpd = maxDogSpd * alignGate * arriveGate;
                }
                else if (greetingPlayer && PlayerModeController.Instance != null)
                {
                    Vector3 lookP = PlayerModeController.Instance.ActiveWorldPosition - _shepherdDog.position;
                    lookP.y = 0f;
                    if (lookP.sqrMagnitude > 0.02f)
                    {
                        _shepherdDog.rotation = Quaternion.Slerp(_shepherdDog.rotation, Quaternion.LookRotation(lookP.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
                    }
                }

                _dogCurrentSpeed = Mathf.Lerp(_dogCurrentSpeed, desiredDogSpd, 1f - Mathf.Exp(-7f * dt));
                bool dogBlocked = false;
                if (_dogCurrentSpeed > 0.1f && (Time.frameCount & 1) == 0)
                {
                    int hits = Physics.SphereCastNonAlloc(_shepherdDog.position + Vector3.up * 0.62f, 0.28f,
                        _shepherdDog.forward, DogHits, 0.65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < hits; h++)
                    {
                        if (DogHits[h].collider == null || DogHits[h].collider.transform.IsChildOf(_shepherdDog)) continue;
                        if (DogHits[h].normal.y > 0.65f) continue;
                        if (DogHits[h].collider.GetComponentInParent<PlayerFootController>() != null) continue;
                        dogBlocked = true;
                        break;
                    }
                }
                if (dogBlocked)
                {
                    _dogCurrentSpeed = 0f;
                }
                else if (_dogCurrentSpeed > 0.01f)
                {
                    Vector3 nextDog = _shepherdDog.position + _shepherdDog.forward * (_dogCurrentSpeed * dt);
                    nextDog.y = _dogHome.y;
                    _shepherdDog.position = nextDog;
                }

                bool actuallyMoving = _dogCurrentSpeed > 0.12f;
                if (_shepherdSkel != null && _shepherdSkel.HasClips)
                {
                    if (actuallyMoving)
                    {
                        bool useGallop = _dogGalloping && _dogCurrentSpeed > 1.75f;
                        float clipSpd = Mathf.Clamp(_dogCurrentSpeed / (useGallop ? 2.4f : 1.4f), 0.6f, 1.6f);
                        _shepherdSkel.PlayClip(useGallop ? "Gallop" : "Walk", 0.24f, clipSpd);
                    }
                    else
                    {
                        _shepherdSkel.PlayClip(greetingPlayer ? "Happy" : "Idle", 0.28f, 1.0f);
                    }
                }
                else
                {
                    _dogWalkBlend = Mathf.Lerp(_dogWalkBlend, actuallyMoving ? Mathf.Clamp01(_dogCurrentSpeed / 1.45f) : 0f, 1f - Mathf.Exp(-9f * dt));
                    if (actuallyMoving || _dogWalkBlend > 0.02f)
                    {
                        if (actuallyMoving)
                        {
                            _dogWalkPhase += dt * (_dogCurrentSpeed * 5.8f);
                        }
                        float amp = (_dogGalloping ? 32f : 24f) * _dogWalkBlend;
                        float legRate = 1f - Mathf.Exp(-14f * dt);
                        for (int i = 0; i < _shepherdLegs.Count; i++)
                        {
                            if (_shepherdLegs[i] == null) continue;
                            float ang = Mathf.Sin(_dogWalkPhase + DogTrotPhase[i] * Mathf.PI * 2f) * amp;
                            _shepherdLegs[i].localRotation = Quaternion.Slerp(_shepherdLegs[i].localRotation, Quaternion.Euler(ang, 0f, 0f), legRate);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _shepherdLegs.Count; i++)
                        {
                            if (_shepherdLegs[i] != null)
                                _shepherdLegs[i].localRotation = Quaternion.Slerp(_shepherdLegs[i].localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
                        }
                    }
                    if (_shepherdHead != null)
                    {
                        float headPitch = greetingPlayer ? -12f : Mathf.Sin(_dogWalkPhase * 0.5f) * 5f * _dogWalkBlend;
                        float headYaw = greetingPlayer ? Mathf.Sin(Time.time * 2.5f) * 8f : Mathf.Sin(Time.time * 1.4f) * 16f;
                        _shepherdHead.localRotation = Quaternion.Slerp(_shepherdHead.localRotation, Quaternion.Euler(headPitch, headYaw, greetingPlayer ? 8f : 0f), 1f - Mathf.Exp(-8f * dt));
                    }
                    if (_shepherdTail != null)
                    {
                        float wagFreq = greetingPlayer ? 16f : 8.5f;
                        float wagAmp = greetingPlayer ? 38f : 22f;
                        Quaternion targetTail = Quaternion.Euler(greetingPlayer ? 22f : 12f, Mathf.Sin(Time.time * wagFreq) * wagAmp, 0f);
                        _shepherdTail.localRotation = Quaternion.Slerp(_shepherdTail.localRotation, targetTail, 1f - Mathf.Exp(-12f * dt));
                    }
                }
                }
            }

            // Spin auto-sprinklers if unlocked
            bool hasSprinkler = FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler;
            if (hasSprinkler)
            {
                for (int i = 0; i < _sprinklerHeads.Count; i++)
                {
                    if (_sprinklerHeads[i] != null)
                    {
                        _sprinklerHeads[i].Rotate(0f, 180f * dt, 0f, Space.Self);
                    }
                }
            }
        }

        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _meadowMat = MakeMat(lit, "MAT_Exp_Meadow", new Color(0.24f, 0.45f, 0.19f), 0.10f);
            _hillNearMat = MakeMat(lit, "MAT_Exp_HillNear", Color.white, 0.02f);
            _hillFarMat = MakeMat(lit, "MAT_Exp_HillFar", new Color(0.84f, 0.90f, 0.95f), 0.02f);
            Texture2D hillNearTex = BuildAydosHillTexture(256, isFarRange: false);
            Texture2D hillFarTex = BuildAydosHillTexture(256, isFarRange: true);
            AssignAlbedoTexture(_hillNearMat, hillNearTex, new Vector2(2f, 1f));
            AssignAlbedoTexture(_hillFarMat, hillFarTex, new Vector2(2f, 1f));
            DisableDistantSpecular(_hillNearMat);
            DisableDistantSpecular(_hillFarMat);
            _roadCountryMat = MakeMat(lit, "MAT_Exp_CountryRoad", new Color(0.30f, 0.29f, 0.27f), 0.18f);
            _stonePathMat = MakeMat(lit, "MAT_Exp_StonePath", new Color(0.68f, 0.64f, 0.56f), 0.16f);
            _woodTimberMat = MakeMat(lit, "MAT_Exp_WoodDark", new Color(0.42f, 0.26f, 0.14f), 0.15f);
            _woodLightMat = MakeMat(lit, "MAT_Exp_WoodLight", new Color(0.68f, 0.48f, 0.28f), 0.18f);
            _barnRedMat = MakeMat(lit, "MAT_Exp_BarnRed", new Color(0.72f, 0.19f, 0.16f), 0.20f);
            _roofTileMat = MakeMat(lit, "MAT_Exp_RoofTile", new Color(0.62f, 0.24f, 0.14f), 0.22f);
            _whiteTrimMat = MakeMat(lit, "MAT_Exp_WhiteTrim", new Color(0.94f, 0.93f, 0.90f), 0.25f);
            _drySoilMat = MakeMat(lit, "MAT_Exp_DrySoil", new Color(0.48f, 0.34f, 0.20f), 0.06f);
            _wetSoilMat = MakeMat(lit, "MAT_Exp_WetSoil", new Color(0.24f, 0.15f, 0.08f), 0.35f);
            _hayGoldMat = MakeMat(lit, "MAT_Exp_HayGold", new Color(0.86f, 0.72f, 0.30f), 0.10f);
            _pineDarkMat = MakeMat(lit, "MAT_Exp_PineDark", new Color(0.12f, 0.34f, 0.16f), 0.12f);
            _leafBrightMat = MakeMat(lit, "MAT_Exp_LeafBright", new Color(0.22f, 0.54f, 0.20f), 0.16f);
            _blossomPinkMat = MakeMat(lit, "MAT_Exp_BlossomPink", new Color(0.95f, 0.60f, 0.74f), 0.22f);
            _flowerRedMat = MakeMat(lit, "MAT_Exp_FlowerRed", new Color(0.92f, 0.20f, 0.22f), 0.28f);
            _flowerYellowMat = MakeMat(lit, "MAT_Exp_FlowerYellow", new Color(0.98f, 0.82f, 0.18f), 0.28f);
            _flowerPurpleMat = MakeMat(lit, "MAT_Exp_FlowerPurple", new Color(0.64f, 0.34f, 0.88f), 0.28f);
            _lakeWaterMat = MakeMat(lit, "MAT_Exp_LakeWater", new Color(0.22f, 0.70f, 0.82f, 0.64f), 0.92f);
            if (_lakeWaterMat != null)
            {
                if (_lakeWaterMat.HasProperty("_Surface")) _lakeWaterMat.SetFloat("_Surface", 1f);
                if (_lakeWaterMat.HasProperty("_SrcBlend")) _lakeWaterMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (_lakeWaterMat.HasProperty("_DstBlend")) _lakeWaterMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (_lakeWaterMat.HasProperty("_ZWrite")) _lakeWaterMat.SetInt("_ZWrite", 0);
                _lakeWaterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _lakeWaterMat.renderQueue = (int)RenderQueue.Transparent;
            }
            Texture2D lakeTex = BuildLakeRippleTexture(128);
            AssignAlbedoTexture(_lakeWaterMat, lakeTex, new Vector2(10f, 10f));
            _sandShoreMat = MakeMat(lit, "MAT_Exp_SandShore", new Color(0.78f, 0.72f, 0.56f), 0.14f);
            _tractorRedMat = MakeMat(lit, "MAT_Exp_TractorRed", new Color(0.84f, 0.18f, 0.14f), 0.65f);
            _darkMetalMat = MakeMat(lit, "MAT_Exp_DarkMetal", new Color(0.18f, 0.19f, 0.21f), 0.45f);
        }

        private static void DisableDistantSpecular(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        private static void AssignAlbedoTexture(Material m, Texture2D tex, Vector2 tiling)
        {
            if (m == null || tex == null) return;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", tiling);
            }
        }

        /// <summary>
        /// Generates a natural rolling Anatolian mountain-ridge mesh (unit radius, unit height,
        /// UV.y = elevation 0..1) so horizon hills read as sculpted forested ridges instead of smooth spheres.
        /// </summary>
        private Mesh BuildOrganicMountainMesh(int seed)
        {
            const int segs = 22;
            const int rings = 9;
            float p1 = seed * 0.73f;
            float p2 = seed * 1.41f;
            float p3 = seed * 2.17f;

            var verts = new System.Collections.Generic.List<Vector3>((segs + 1) * (rings + 1));
            var uvs = new System.Collections.Generic.List<Vector2>((segs + 1) * (rings + 1));
            var tris = new System.Collections.Generic.List<int>(segs * rings * 6);

            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings; // 0 = base skirt, 1 = summit
                float baseRadius = 1f - v;
                // Natural bell-ridge profile with broad sweeping foothills and distinct crest
                float elevProfile = Mathf.Pow(Mathf.Sin(v * Mathf.PI * 0.5f), 1.25f);

                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float theta = u * Mathf.PI * 2f;

                    float ridge1 = Mathf.Sin(theta * 2f + p1) * 0.16f;
                    float ridge2 = Mathf.Cos(theta * 3f - p2) * 0.11f;
                    float ridge3 = Mathf.Sin(theta * 5f + p3 + v * 2.4f) * 0.055f;

                    // Shoulder asymmetry tapers smoothly to 0 at summit & base skirt
                    float midMask = Mathf.Sin(v * Mathf.PI);
                    float rad = baseRadius * (1f + (ridge1 + ridge2 * 0.8f) * (0.4f + 0.6f * midMask));
                    float y = elevProfile * (1f + (ridge1 * 0.45f + ridge2 * 0.35f + ridge3) * (0.25f + 0.75f * midMask));
                    // Secondary shoulder peak offset
                    float shoulder = Mathf.Exp(-Mathf.Pow(v - 0.55f, 2f) * 16f) * Mathf.Max(0f, Mathf.Cos(theta - p1)) * 0.22f;
                    y = Mathf.Max(0f, y + shoulder);

                    verts.Add(new Vector3(Mathf.Cos(theta) * rad, y, Mathf.Sin(theta) * rad));
                    uvs.Add(new Vector2(u, Mathf.Clamp01(y)));
                }
            }

            int stride = segs + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int a = r * stride + s;
                    int b = a + 1;
                    int c = (r + 1) * stride + s;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = $"AydosRidgeMesh_{seed}" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _runtimeMeshes.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// Pure-albedo procedural pine-forest + olive scrub + sunlit limestone ridge texture
        /// (zero normal map = zero distance shimmer on mobile/desktop!).
        /// </summary>
        private Texture2D BuildAydosHillTexture(int size, bool isFarRange)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _runtimeTextures.Add(tex);

            Color forestLow = isFarRange ? new Color(0.16f, 0.28f, 0.24f) : new Color(0.14f, 0.31f, 0.14f);
            Color pineMid   = isFarRange ? new Color(0.22f, 0.34f, 0.27f) : new Color(0.22f, 0.42f, 0.17f);
            Color oliveHi   = isFarRange ? new Color(0.32f, 0.40f, 0.33f) : new Color(0.36f, 0.46f, 0.23f);
            Color rockRidge = isFarRange ? new Color(0.46f, 0.50f, 0.48f) : new Color(0.56f, 0.52f, 0.40f);

            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1); // 0 = foothill forest, 1 = sunlit ridge crest
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // Seamless periodic tree-canopy dapple using trigonometric harmonics + Perlin
                    float canopy = 0.5f
                        + 0.25f * Mathf.Sin(u * Mathf.PI * 12f + Mathf.Sin(v * 14f) * 1.4f)
                        + 0.15f * Mathf.Cos(u * Mathf.PI * 24f - v * 28f)
                        + 0.10f * Mathf.PerlinNoise(u * 10f + 3.1f, v * 10f + 7.4f);

                    float elev = Mathf.Clamp01(v + (canopy - 0.5f) * 0.22f);
                    Color c;
                    if (elev < 0.42f)
                    {
                        c = Color.Lerp(forestLow, pineMid, elev / 0.42f);
                    }
                    else if (elev < 0.76f)
                    {
                        c = Color.Lerp(pineMid, oliveHi, (elev - 0.42f) / 0.34f);
                    }
                    else
                    {
                        c = Color.Lerp(oliveHi, rockRidge, (elev - 0.76f) / 0.24f);
                    }

                    // Subtle dappled pine shadows so slopes feel richly vegetated from any distance
                    float dapple = Mathf.Lerp(0.84f, 1.12f, Mathf.Clamp01(canopy));
                    pix[y * size + x] = c * dapple;
                }
            }
            tex.SetPixels(pix);
            tex.Apply(true, false);
            return tex;
        }

        private Texture2D BuildLakeRippleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _runtimeTextures.Add(tex);

            Color deepAqua = new Color(0.32f, 0.72f, 0.84f);
            Color crestGlint = new Color(0.88f, 0.98f, 1.00f);

            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float w1 = Mathf.Sin((u * 4f + v * 2f) * Mathf.PI * 2f);
                    float w2 = Mathf.Cos((u * 3f - v * 5f) * Mathf.PI * 2f);
                    float w3 = Mathf.Sin((u * 7f + v * 6f) * Mathf.PI * 2f);
                    float caustic = Mathf.Pow(Mathf.Clamp01((w1 + w2 + w3 * 0.6f) * 0.25f + 0.5f), 2.8f);
                    pix[y * size + x] = Color.Lerp(deepAqua, crestGlint, caustic * 0.55f);
                }
            }
            tex.SetPixels(pix);
            tex.Apply(true, false);
            return tex;
        }

        private static void ClearLegacyTightPerimeterWalls()
        {
            string[] legacyObstacles =
            {
                "Hamidiye_DistrictPerimeter",
                "World_CountryFencePerimeter",
                // Legacy West-side street kiosks that overlapped the expanded Sultanbeyli Lake promenade & south gazebo
                "Eyyubi_WestBufe_3",
                "Eyyubi_WestBufe_4",
                "Eyyubi_WestBufe_5"
            };
            for (int i = 0; i < legacyObstacles.Length; i++)
            {
                GameObject old = GameObject.Find(legacyObstacles[i]);
                if (old != null)
                {
                    old.SetActive(false);
                }
            }

            // Offset Havadis_Mailbox cleanly away from Hamidiye_AveLamp_01 (-56.0, 180.5) so the streetlamp pole doesn't impale it!
            GameObject mailbox = GameObject.Find("Havadis_Mailbox");
            if (mailbox != null)
            {
                Vector3 mp = mailbox.transform.position;
                if (Mathf.Abs(mp.x + 56.0f) < 0.8f && Mathf.Abs(mp.z - 180.6f) < 0.8f)
                {
                    mailbox.transform.position = new Vector3(-53.6f, mp.y, mp.z);
                }
            }
        }

        // =====================================================================
        // 1. EXPANDED 980x980 VALLEY FLOOR + 360-DEGREE ROLLING AYDOS HILLS
        // =====================================================================
        private void BuildValleyGroundAndAydosHills()
        {
            Transform envRoot = new GameObject("ValleyGroundAndAydosHills").transform;
            envRoot.SetParent(transform, false);

            // Walkable lush valley ground split into 4 slabs (1280x1280m) leaving an open 48x36m swimming basin
            // directly under Sultanbeyli Lake (X:-222..-174, Z:-18..+18) so players can genuinely swim & dive,
            // and accommodating all 6 neighboring Sultanbeyli districts seamlessly!
            (string name, Vector3 pos, Vector3 size)[] valleySlabs =
            {
                ("ExpandedValleyFloor_East",  new Vector3( 233f, -0.545f,    0f), new Vector3(814f, 1.0f, 1280f)),
                ("ExpandedValleyFloor_West",  new Vector3(-431f, -0.545f,    0f), new Vector3(418f, 1.0f, 1280f)),
                ("ExpandedValleyFloor_North", new Vector3(-198f, -0.545f,  329f), new Vector3( 48f, 1.0f,  622f)),
                ("ExpandedValleyFloor_South", new Vector3(-198f, -0.545f, -329f), new Vector3( 48f, 1.0f,  622f))
            };
            for (int s = 0; s < valleySlabs.Length; s++)
            {
                GameObject slab = CreateBox(valleySlabs[s].name, envRoot, valleySlabs[s].pos, valleySlabs[s].size, _meadowMat, collider: true);
                slab.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Sunlit sandy lakebed floor at Y = -1.70m so underwater pearls & M23 sunken watch are reachable and visible!
            GameObject lakeBed = CreateBox("SultanbeyliLakeBed", envRoot, new Vector3(-198f, -1.95f, 0f),
                new Vector3(54f, 0.50f, 42f), _sandShoreMat, collider: true);
            lakeBed.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 3 sculpted organic Anatolian mountain-ridge meshes reused across all 28 horizon hills!
            Mesh[] hillMeshes =
            {
                BuildOrganicMountainMesh(101),
                BuildOrganicMountainMesh(202),
                BuildOrganicMountainMesh(303)
            };

            // 28 layered rolling sunlit Aydos Mountain Ridges encircling the 360-degree horizon safely OUTSIDE ±435m!
            System.Random rng = new System.Random(1453);
            for (int i = 0; i < 28; i++)
            {
                float angle = (i / 28f) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.12 - 0.06);
                bool outer = (i % 2) == 1;
                float dist = outer ? 545f + (float)rng.NextDouble() * 48f : 475f + (float)rng.NextDouble() * 34f;
                float hx = Mathf.Cos(angle) * dist;
                float hz = Mathf.Sin(angle) * dist;
                float radiusX = 82f + (float)rng.NextDouble() * 38f;
                float radiusZ = 74f + (float)rng.NextDouble() * 34f;
                float height = outer ? (54f + (float)rng.NextDouble() * 38f) : (28f + (float)rng.NextDouble() * 20f);

                // Keep Aydos horizon slopes & hilltop pines completely clear of Aydos Shooting Range (X:64..140, Z:-342..-200)
                float hillFootprint = Mathf.Max(radiusX, radiusZ) + 22f;
                for (int push = 0; push < 6; push++)
                {
                    float cx = Mathf.Clamp(hx, 64f, 140f);
                    float cz = Mathf.Clamp(hz, -342f, -200f);
                    float dx = hx - cx;
                    float dz = hz - cz;
                    if (dx * dx + dz * dz >= hillFootprint * hillFootprint) break;
                    dist += 40f;
                    hx = Mathf.Cos(angle) * dist;
                    hz = Mathf.Sin(angle) * dist;
                }

                GameObject hill = new GameObject($"AydosHill_{i:00}");
                hill.transform.SetParent(envRoot, false);
                hill.transform.position = new Vector3(hx, -2.0f, hz);
                hill.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                hill.transform.localScale = new Vector3(radiusX, height, radiusZ);
                hill.AddComponent<MeshFilter>().sharedMesh = hillMeshes[i % hillMeshes.Length];
                MeshRenderer hr = hill.AddComponent<MeshRenderer>();
                hr.sharedMaterial = outer ? _hillFarMat : _hillNearMat;
                hr.shadowCastingMode = ShadowCastingMode.Off;
                hr.receiveShadows = false;
                hr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                hr.lightProbeUsage = LightProbeUsage.Off;

                // Hilltop cypress & pine forest silhouettes on inner hills for rich Istanbul Aydos skyline!
                if (!outer)
                {
                    for (int t = 0; t < 6; t++)
                    {
                        float ta = (t / 6f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
                        float tr = 8f + (float)rng.NextDouble() * 22f;
                        float ox = Mathf.Cos(ta) * tr;
                        float oz = Mathf.Sin(ta) * tr;
                        float treeY = Mathf.Lerp(height * 0.72f, height * 0.36f, tr / 34f);
                        SpawnPineTree(envRoot, new Vector3(hx + ox, treeY, hz + oz), 1.6f + (float)rng.NextDouble() * 1.0f);
                    }
                }
            }

            // Ensure main camera far clip plane comfortably clears the outer 450m Aydos Hills even from Atlas AVM / Gölet
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.farClipPlane < 720f)
            {
                mainCam.farClipPlane = 720f;
            }
        }

        // =====================================================================
        // 2. HOME GARDEN BOSTAN (4 Starter Raised Plots beside Deniz's No:48 House!)
        // =====================================================================
        private void BuildHomeGardenBostan()
        {
            Transform homeRoot = new GameObject("HomeGardenBostan_No48").transform;
            homeRoot.SetParent(transform, false);

            // Cozy sunlit lawn & stepping stones at x=[-58.4..-52.0], z=[6.4..13.6] right beside No:48 driveway!
            CreateBox("HomeBostan_Path", homeRoot, new Vector3(-55.2f, 0.015f, 10.0f), new Vector3(6.4f, 0.03f, 7.2f), _stonePathMat, collider: false);

            // Low rustic whitewashed wooden garden fence (open south entry facing Selman St.!)
            CreateBox("FenceW", homeRoot, new Vector3(-58.4f, 0.42f, 10.0f), new Vector3(0.14f, 0.84f, 7.2f), _whiteTrimMat, collider: true);
            CreateBox("FenceE", homeRoot, new Vector3(-52.0f, 0.42f, 10.0f), new Vector3(0.14f, 0.84f, 7.2f), _whiteTrimMat, collider: true);
            CreateBox("FenceN", homeRoot, new Vector3(-55.2f, 0.42f, 13.6f), new Vector3(6.5f, 0.84f, 0.14f), _whiteTrimMat, collider: true);

            // Entrance sign
            CreateWorldSign(homeRoot, "EVİMİN BOSTANI", new Vector3(-57.6f, 0f, 6.5f), 0f, _woodTimberMat, new Color(0.24f, 0.64f, 0.34f));

            // 4 Starter Raised Soil Plots (pre-seed first plot with ripe Tomatoes on brand new saves!)
            Vector3[] homePlotCoords =
            {
                new Vector3(-56.6f, 0.02f,  8.4f),
                new Vector3(-53.8f, 0.02f,  8.4f),
                new Vector3(-56.6f, 0.02f, 11.4f),
                new Vector3(-53.8f, 0.02f, 11.4f)
            };

            for (int i = 0; i < homePlotCoords.Length; i++)
            {
                string pid = $"home_plot_{i + 1:00}";
                if (i == 0) EnsureStarterCropIfFresh(pid, FarmCropType.Tomato, 1.0f);
                SpawnFarmPlot(homeRoot, pid, homePlotCoords[i]);
            }

            // Cute cherry-blossom tree + flowerbed + cozy warm garden lantern framing Deniz's home garden
            SpawnBlossomTree(homeRoot, new Vector3(-59.2f, 0f, 13.8f), 0.92f);
            SpawnFlowerBed(homeRoot, new Vector3(-53.2f, 0.02f, 6.1f), 0f);
            SpawnFarmLantern(homeRoot, new Vector3(-52.4f, 0f, 12.8f));
        }

        // =====================================================================
        // 3. EAST EXPANSION: AYDOS YEŞİL VADİ · ÇİFTLİK & BOSTAN
        // =====================================================================
        private void BuildEastAydosFarmValley()
        {
            _farmRoot = new GameObject("AydosFarmValley_East").transform;
            _farmRoot.SetParent(transform, false);

            // Scenic country road continuing straight East from Selman Sokağı (x = 108..148, z = 0)
            CreateBox("FarmCountryRoad", _farmRoot, new Vector3(128f, 0.02f, 0f), new Vector3(44f, 0.04f, 8.6f), _roadCountryMat, collider: false);
            CreateBox("FarmCourtyard", _farmRoot, new Vector3(165f, 0.015f, 0f), new Vector3(42f, 0.03f, 14f), _stonePathMat, collider: false);

            // Cypress & Blossom tree boulevard lining Aydos Çiftlik Yolu (keep south x=[116..132] clear for Aydos Car Wash!)
            for (int i = 0; i < 4; i++)
            {
                float rx = 113f + i * 7.8f;
                SpawnBlossomTree(_farmRoot, new Vector3(rx, 0f, 6.8f), 0.95f);
                if (rx + 2f < 116f || rx + 2f > 132f)
                {
                    SpawnPineTree(_farmRoot, new Vector3(rx + 2f, 0f, -6.8f), 1.05f);
                }
                if (i % 2 == 0)
                {
                    SpawnFarmLantern(_farmRoot, new Vector3(rx + 3.5f, 0f, 5.2f));
                }
            }

            // Grand Wooden Entrance Archway at (138, 0, 0)
            BuildFarmArchway(_farmRoot, new Vector3(138f, 0f, 0f));

            // Village Market Stand (Sell Harvest!) at (146, 0, -5.8) & Farm Management Board at (146, 0, +5.8)
            BuildVillageMarketStand(_farmRoot, new Vector3(146f, 0f, -5.8f));
            BuildFarmManagementBoard(_farmRoot, new Vector3(146f, 0f, 5.8f));

            // --- 12 RAISED FARM PLOTS (2 rows x 6 beds, x = 152..177, z = +11.5 & +17.5) ---
            int plotIdx = 1;
            for (int row = 0; row < 2; row++)
            {
                float pz = row == 0 ? 11.5f : 17.5f;
                for (int col = 0; col < 6; col++)
                {
                    float px = 152f + col * 4.6f;
                    string pid = $"aydos_plot_{plotIdx:00}";
                    if (plotIdx == 1) EnsureStarterCropIfFresh(pid, FarmCropType.Tomato, 1.0f);
                    else if (plotIdx == 2) EnsureStarterCropIfFresh(pid, FarmCropType.Sunflower, 1.0f);
                    else if (plotIdx == 3) EnsureStarterCropIfFresh(pid, FarmCropType.Strawberry, 0.65f);
                    SpawnFarmPlot(_farmRoot, pid, new Vector3(px, 0.02f, pz));
                    plotIdx++;
                }
            }

            // Stone Water Well + Auto-Sprinkler poles in the garden
            BuildStoneWaterWell(_farmRoot, new Vector3(163.5f, 0f, 5.8f));
            BuildSprinklerPole(_farmRoot, new Vector3(156.6f, 0f, 14.5f));
            BuildSprinklerPole(_farmRoot, new Vector3(170.4f, 0f, 14.5f));
            HandleSprinklerUnlocked();

            // --- CHICKEN COOP & RUN (x = 146..170, z = -30..-12) ---
            BuildChickenCoopAndPasture(_farmRoot);

            // --- RED TIMBER BARN & COW PASTURE (x = 184..220, z = +10..+42) ---
            BuildRedBarnAndCowPasture(_farmRoot);

            // --- SHEEP PEN & SHEPHERD DOG KARABAŞ (x = 180..216, z = -34..-10) ---
            BuildSheepPenAndDog(_farmRoot);

            // --- ICONIC ROTATING AYDOS WINDMILL at (215, 0, 0) ---
            BuildRotatingWindmill(_farmRoot, new Vector3(215f, 0f, 0f));

            // --- COZY FARM TEA GAZEBO & VINTAGE TRACTOR DECOR ---
            BuildGazebo(_farmRoot, new Vector3(172f, 0f, -5.0f));
            BuildFarmTractorDecor(_farmRoot, new Vector3(179f, 0f, 3.8f), 210f);

            // Warm farmyard lanterns illuminating courtyard, crop plots, barn/cow pasture, chicken coop, sheep pen & windmill!
            SpawnFarmLantern(_farmRoot, new Vector3(144f, 0f,  4.2f));
            SpawnFarmLantern(_farmRoot, new Vector3(154f, 0f,  8.0f));
            SpawnFarmLantern(_farmRoot, new Vector3(174f, 0f, 14.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(158f, 0f, -11.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(183f, 0f, -8.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(198f, 0f, -15.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(190f, 0f, 11.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(206f, 0f, 20.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(204f, 0f,  4.5f));

            // Perimeter scenic pines & blossoms framing Aydos Farm Valley
            Vector3[] borderTrees =
            {
                new Vector3(145f, 0f,  26f), new Vector3(158f, 0f,  28f), new Vector3(172f, 0f,  27f),
                new Vector3(224f, 0f,  24f), new Vector3(226f, 0f,   8f), new Vector3(225f, 0f, -14f),
                new Vector3(222f, 0f, -34f), new Vector3(196f, 0f, -40f), new Vector3(162f, 0f, -36f),
                new Vector3(140f, 0f, -26f), new Vector3(140f, 0f,  18f)
            };
            for (int i = 0; i < borderTrees.Length; i++)
            {
                if (i % 3 == 0) SpawnBlossomTree(_farmRoot, borderTrees[i], 1.05f);
                else SpawnPineTree(_farmRoot, borderTrees[i], 1.15f + (i % 3) * 0.15f);
            }
        }

        private void BuildFarmArchway(Transform parent, Vector3 pos)
        {
            Transform arch = new GameObject("AydosFarmArchway").transform;
            arch.SetParent(parent, false);
            arch.position = pos;

            CreateBox("PillarN", arch, new Vector3(0f, 2.4f,  4.8f), new Vector3(0.45f, 4.8f, 0.45f), _woodTimberMat, collider: true);
            CreateBox("PillarS", arch, new Vector3(0f, 2.4f, -4.8f), new Vector3(0.45f, 4.8f, 0.45f), _woodTimberMat, collider: true);
            CreateBox("TopBeam", arch, new Vector3(0f, 4.7f,  0.0f), new Vector3(0.55f, 0.65f, 10.6f), _woodTimberMat, collider: false);
            CreateBox("SignBoard", arch, new Vector3(-0.30f, 4.7f, 0f), new Vector3(0.10f, 1.05f, 7.6f), _barnRedMat, collider: false);

            GameObject txtObj = new GameObject("ArchText");
            txtObj.transform.SetParent(arch, false);
            txtObj.transform.localPosition = new Vector3(-0.38f, 4.72f, 0f);
            txtObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = "AYDOS ÇİFTLİĞİ · BOSTAN & MANDIRA";
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.94f, 0.78f);

            SpawnHangingLantern(arch, new Vector3(0f, 4.15f,  3.6f));
            SpawnHangingLantern(arch, new Vector3(0f, 4.15f, -3.6f));
        }

        private void BuildVillageMarketStand(Transform parent, Vector3 pos)
        {
            GameObject stand = new GameObject("VillageFarmMarketStand");
            stand.transform.SetParent(parent, false);
            stand.transform.position = pos;

            CreateBox("Counter", stand.transform, new Vector3(0f, 0.52f, 0f), new Vector3(4.2f, 1.04f, 1.4f), _woodLightMat, collider: true);
            CreateBox("PostL", stand.transform, new Vector3(-1.9f, 1.4f, -0.55f), new Vector3(0.16f, 2.8f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("PostR", stand.transform, new Vector3( 1.9f, 1.4f, -0.55f), new Vector3(0.16f, 2.8f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("AwningRed", stand.transform, new Vector3(0f, 2.75f, 0.15f), new Vector3(4.6f, 0.14f, 2.1f), _barnRedMat, collider: false).transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            CreateBox("AwningTrim", stand.transform, new Vector3(0f, 2.52f, 1.12f), new Vector3(4.6f, 0.24f, 0.08f), _whiteTrimMat, collider: false);

            // Colorful produce crates on counter
            CreateBox("CrateTom", stand.transform, new Vector3(-1.2f, 1.18f, 0f), new Vector3(0.7f, 0.28f, 0.6f), _woodTimberMat, collider: false);
            CreateSphere("TomDisplay", stand.transform, new Vector3(-1.2f, 1.38f, 0f), new Vector3(0.55f, 0.22f, 0.45f), _flowerRedMat);
            CreateBox("CrateSun", stand.transform, new Vector3(0f, 1.18f, 0f), new Vector3(0.7f, 0.28f, 0.6f), _woodTimberMat, collider: false);
            CreateSphere("SunDisplay", stand.transform, new Vector3(0f, 1.38f, 0f), new Vector3(0.55f, 0.22f, 0.45f), _flowerYellowMat);
            CreateBox("CrateMilk", stand.transform, new Vector3(1.2f, 1.24f, 0f), new Vector3(0.32f, 0.42f, 0.32f), _whiteTrimMat, collider: false);

            InteractableTrigger trig = stand.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmMarket, "aydos_farm_market", "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt("Ürünleri Sat · Köy Tezgâhı", "Sell Crops · Village Market Stand");

            // Overhead glowing stand sign (faces courtyard cleanly!)
            GameObject signTxt = new GameObject("MarketSign");
            signTxt.transform.SetParent(stand.transform, false);
            signTxt.transform.localPosition = new Vector3(0f, 3.05f, 0.65f);
            signTxt.transform.localRotation = Quaternion.identity;
            TextMesh tm = signTxt.AddComponent<TextMesh>();
            tm.text = "KÖY PAZARI TEZGÂHI";
            tm.characterSize = 0.06f;
            tm.fontSize = 42;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.92f, 0.65f);
        }

        private void BuildFarmManagementBoard(Transform parent, Vector3 pos)
        {
            GameObject board = new GameObject("FarmManagementBoard");
            board.transform.SetParent(parent, false);
            board.transform.position = pos;

            CreateBox("PostL", board.transform, new Vector3(-1.1f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), _woodTimberMat, collider: true);
            CreateBox("PostR", board.transform, new Vector3( 1.1f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), _woodTimberMat, collider: true);
            CreateBox("Panel", board.transform, new Vector3(0f, 1.55f, 0f), new Vector3(2.2f, 1.25f, 0.12f), _woodLightMat, collider: false);
            CreateBox("Roof", board.transform, new Vector3(0f, 2.32f, 0f), new Vector3(2.7f, 0.18f, 0.72f), _roofTileMat, collider: false);

            GameObject txtObj = new GameObject("BoardText");
            txtObj.transform.SetParent(board.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 1.60f, -0.09f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = "TOHUM & ÇİFTLİK\nYÖNETİMİ";
            tm.characterSize = 0.055f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.12f, 0.24f, 0.14f);

            InteractableTrigger trig = board.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmBoard, "aydos_farm_board", "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt("Tohum & Çiftlik Yönetimi", "Seeds & Farm Management");
        }

        private void BuildChickenCoopAndPasture(Transform parent)
        {
            Transform coopRoot = new GameObject("ChickenCoopAndRun").transform;
            coopRoot.SetParent(parent, false);

            // Rustic raised timber chicken coop at (158, 0, -26.5)
            Vector3 cp = new Vector3(158f, 0f, -26.5f);
            CreateBox("CoopBody", coopRoot, cp + new Vector3(0f, 1.35f, 0f), new Vector3(4.8f, 2.1f, 3.2f), _woodLightMat, collider: true);
            CreateBox("CoopRoof", coopRoot, cp + new Vector3(0f, 2.65f, 0f), new Vector3(5.4f, 0.45f, 3.8f), _roofTileMat, collider: false);
            CreateBox("CoopDoor", coopRoot, cp + new Vector3(0f, 0.9f, 1.62f), new Vector3(0.95f, 1.1f, 0.1f), _darkMetalMat, collider: false);
            CreateBox("CoopRamp", coopRoot, cp + new Vector3(0f, 0.30f, 2.35f), new Vector3(0.9f, 0.10f, 1.6f), _woodTimberMat, collider: false).transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

            // Fenced yard (x = 146..170, z = -30..-12, wide open gate on North side at x = 158, z = -12)
            BuildPaddockFence(coopRoot, 146f, 170f, -30f, -12f, gateSide: 0, gatePos: 158f, gateHalfW: 2.4f);
            CreateWorldSign(coopRoot, "TAVUK KÜMESİ", new Vector3(154.5f, 0f, -11.5f), 180f, _woodTimberMat, new Color(0.86f, 0.56f, 0.16f));

            int totalChickens = 5 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraChickens : 0);
            for (int i = 0; i < totalChickens; i++)
            {
                SpawnLivestock(FarmAnimalKind.Chicken, i);
            }
        }

        private void BuildRedBarnAndCowPasture(Transform parent)
        {
            Transform barnRoot = new GameObject("RedBarnAndCowPasture").transform;
            barnRoot.SetParent(parent, false);

            // Iconic Red Timber Barn at (204, 0, 34)
            Vector3 bp = new Vector3(204f, 0f, 34f);
            CreateBox("BarnBody", barnRoot, bp + new Vector3(0f, 3.0f, 0f), new Vector3(12.5f, 6.0f, 9.2f), _barnRedMat, collider: true);
            CreateBox("BarnRoofL", barnRoot, bp + new Vector3(0f, 7.1f, -2.5f), new Vector3(13.4f, 0.35f, 5.8f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            CreateBox("BarnRoofR", barnRoot, bp + new Vector3(0f, 7.1f,  2.5f), new Vector3(13.4f, 0.35f, 5.8f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler( 26f, 0f, 0f);
            CreateBox("BarnDoorFrame", barnRoot, bp + new Vector3(0f, 2.2f, -4.64f), new Vector3(4.2f, 4.4f, 0.14f), _whiteTrimMat, collider: false);
            CreateBox("BarnDoorInner", barnRoot, bp + new Vector3(0f, 2.2f, -4.70f), new Vector3(3.6f, 3.9f, 0.10f), _barnRedMat, collider: false);
            CreateBox("HayLoftWin", barnRoot, bp + new Vector3(0f, 5.0f, -4.64f), new Vector3(1.8f, 1.4f, 0.14f), _whiteTrimMat, collider: false);

            // Golden hay bales + water trough
            SpawnHayBale(barnRoot, new Vector3(193f, 0.55f, 30f));
            SpawnHayBale(barnRoot, new Vector3(194.6f, 0.55f, 30.8f));
            SpawnHayBale(barnRoot, new Vector3(193.8f, 1.55f, 30.4f));
            CreateBox("WaterTrough", barnRoot, new Vector3(190f, 0.38f, 24f), new Vector3(2.8f, 0.76f, 1.1f), _woodTimberMat, collider: true);
            CreateBox("WaterTroughFill", barnRoot, new Vector3(190f, 0.72f, 24f), new Vector3(2.5f, 0.12f, 0.85f), _lakeWaterMat, collider: false);

            // Pasture fence (x = 184..218, z = 10..40, open West gate at x = 184, z = 19)
            BuildPaddockFence(barnRoot, 184f, 218f, 10f, 40f, gateSide: 2, gatePos: 19f, gateHalfW: 2.8f);
            CreateWorldSign(barnRoot, "İNEK AHIRI · MANDIRA", new Vector3(183.5f, 0f, 14.5f), 90f, _woodTimberMat, new Color(0.72f, 0.19f, 0.16f));

            int totalCows = 3 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraCows : 0);
            for (int i = 0; i < totalCows; i++)
            {
                SpawnLivestock(FarmAnimalKind.Cow, i);
            }
        }

        private void BuildSheepPenAndDog(Transform parent)
        {
            Transform sheepRoot = new GameObject("SheepPenAndKarabas").transform;
            sheepRoot.SetParent(parent, false);

            // Timber shade shelter at (204, 0, -29)
            Vector3 sp = new Vector3(204f, 0f, -29f);
            CreateBox("ShelterBack", sheepRoot, sp + new Vector3(0f, 1.4f, -1.8f), new Vector3(7.6f, 2.8f, 0.24f), _woodTimberMat, collider: true);
            CreateBox("ShelterRoof", sheepRoot, sp + new Vector3(0f, 2.95f, 0f), new Vector3(8.2f, 0.22f, 4.4f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            SpawnHayBale(sheepRoot, sp + new Vector3(-2.6f, 0.55f, 0.6f));
            SpawnHayBale(sheepRoot, sp + new Vector3( 2.6f, 0.55f, 0.6f));

            // Sheep paddock fence (x = 180..216, z = -34..-10, open West gate at x = 180, z = -18)
            BuildPaddockFence(sheepRoot, 180f, 216f, -34f, -10f, gateSide: 2, gatePos: -18f, gateHalfW: 2.6f);
            CreateWorldSign(sheepRoot, "KOYUN AĞILI", new Vector3(179.5f, 0f, -13.8f), 90f, _woodTimberMat, new Color(0.24f, 0.54f, 0.68f));

            int totalSheep = 4 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraSheep : 0);
            for (int i = 0; i < totalSheep; i++)
            {
                SpawnLivestock(FarmAnimalKind.Sheep, i);
            }

            // Friendly Anatolian Shepherd Dog "Karabaş" guarding outside the sheep pen!
            BuildShepherdDog(sheepRoot, new Vector3(177f, 0f, -22f));
        }

        private void HandleExtraAnimalPurchased(FarmAnimalKind kind)
        {
            if (kind == FarmAnimalKind.Chicken) SpawnLivestock(kind, _spawnedChickens);
            else if (kind == FarmAnimalKind.Sheep) SpawnLivestock(kind, _spawnedSheep);
            else SpawnLivestock(kind, _spawnedCows);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void SpawnLivestock(FarmAnimalKind kind, int idx)
        {
            if (_farmRoot == null) return;

            string[] chickenNames = { "Çilli", "Pamuk", "Kınalı", "Sarı", "Benekli", "Fındık", "Badem", "Zeytin", "Boncuk", "Şeker" };
            string[] cowNames = { "Sarıkız", "Alaca", "Karakız", "Nazlı", "Sultan", "Gülbahar" };
            string[] sheepNames = { "Yumak", "Bulut", "Kar", "Pofuduk", "Kuzu", "Duman", "İnci", "Tüylü" };

            Vector3 center;
            Vector2 half;
            string name;
            float radius;

            if (kind == FarmAnimalKind.Chicken)
            {
                center = new Vector3(158f, 0.02f, -19.5f);
                half = new Vector2(8.5f, 5.5f);
                name = chickenNames[idx % chickenNames.Length];
                radius = 2.6f;
                _spawnedChickens++;
            }
            else if (kind == FarmAnimalKind.Cow)
            {
                center = new Vector3(198f, 0.02f, 21f);
                half = new Vector2(11.0f, 7.5f);
                name = cowNames[idx % cowNames.Length];
                radius = 3.6f;
                _spawnedCows++;
            }
            else
            {
                center = new Vector3(197f, 0.02f, -20.5f);
                half = new Vector2(12.5f, 6.5f);
                name = sheepNames[idx % sheepNames.Length];
                radius = 3.0f;
                _spawnedSheep++;
            }

            GameObject go = new GameObject($"FarmAnimal_{kind}_{name}_{idx}");
            go.transform.SetParent(_farmRoot, false);
            float ox = ((idx * 37) % 10 - 5) * (half.x * 0.16f);
            float oz = ((idx * 53) % 10 - 5) * (half.y * 0.16f);
            go.transform.position = center + new Vector3(ox, 0f, oz);
            go.transform.rotation = Quaternion.Euler(0f, idx * 67f, 0f);

            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmAnimal, $"farm_animal_{kind}_{idx}", "ui.btn.talk", radius);

            FarmAnimalController ctrl = go.AddComponent<FarmAnimalController>();
            ctrl.Initialize(kind, name, center, half, trig, idx, startReady: idx < 3);
        }

        private void SpawnFarmPlot(Transform parent, string plotId, Vector3 worldPos)
        {
            GameObject go = new GameObject($"FarmPlot_{plotId}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            // Raised timber frame + rich soil top
            CreateBox("FrameN", go.transform, new Vector3(0f, 0.14f,  0.96f), new Vector3(2.44f, 0.28f, 0.14f), _woodTimberMat, collider: false);
            CreateBox("FrameS", go.transform, new Vector3(0f, 0.14f, -0.96f), new Vector3(2.44f, 0.28f, 0.14f), _woodTimberMat, collider: false);
            CreateBox("FrameW", go.transform, new Vector3(-1.16f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 1.84f), _woodTimberMat, collider: false);
            CreateBox("FrameE", go.transform, new Vector3( 1.16f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 1.84f), _woodTimberMat, collider: false);

            GameObject soil = CreateBox("SoilBed", go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(2.22f, 0.22f, 1.80f), _drySoilMat, collider: false);
            MeshRenderer soilMr = soil.GetComponent<MeshRenderer>();

            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmPlot, plotId, "ui.btn.talk", 2.8f);

            FarmPlotInteractable plot = go.AddComponent<FarmPlotInteractable>();
            plot.Initialize(plotId, trig, soilMr, _drySoilMat, _wetSoilMat);
        }

        private static void EnsureStarterCropIfFresh(string plotId, FarmCropType crop, float progress)
        {
            SaveData d = FarmEconomyManager.Data;
            if (d == null) return;
            if (d.farmPlots == null) d.farmPlots = new List<FarmPlotSaveEntry>();
            for (int i = 0; i < d.farmPlots.Count; i++)
            {
                if (d.farmPlots[i] != null && d.farmPlots[i].plotId == plotId) return; // Already persisted
            }
            d.farmPlots.Add(new FarmPlotSaveEntry
            {
                plotId = plotId,
                cropType = (int)crop,
                growthProgress = progress,
                isWatered = true
            });
        }

        private void BuildRotatingWindmill(Transform parent, Vector3 pos)
        {
            Transform wm = new GameObject("AydosWindmill").transform;
            wm.SetParent(parent, false);
            wm.SetPositionAndRotation(pos, Quaternion.Euler(0f, -90f, 0f));

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_Windmill", wm, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                BoxCollider col = wm.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 3.2f, 0f);
                col.size = new Vector3(4.8f, 6.4f, 4.8f);
                _windmillBlades = OurWorldModelLibrary.FindPart(fbx.transform, "BladesPivot")
                               ?? OurWorldModelLibrary.FindPart(fbx.transform, "WindmillBlades");
                // Decorative golden wheat & corn bundles framing the stone windmill base!
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Wheat", wm, new Vector3(3.1f, 0f, 2.2f), Quaternion.Euler(0f, 20f, 0f), Vector3.one * 1.35f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Wheat", wm, new Vector3(-3.0f, 0f, 2.4f), Quaternion.Euler(0f, -35f, 0f), Vector3.one * 1.25f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Corn", wm, new Vector3(3.3f, 0f, -2.0f), Quaternion.Euler(0f, 15f, 0f), Vector3.one * 1.15f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Corn", wm, new Vector3(-3.2f, 0f, -1.9f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.10f);
                return;
            }

            // Fallback procedural windmill
            wm.rotation = Quaternion.identity;
            CreateCylinder("TowerBase", wm, new Vector3(0f, 3.2f, 0f), new Vector3(4.6f, 3.2f, 4.6f), _whiteTrimMat, collider: true);
            CreateCylinder("TowerTop", wm, new Vector3(0f, 7.6f, 0f), new Vector3(3.6f, 2.2f, 3.6f), _whiteTrimMat, collider: false);
            CreateSphere("RoofDome", wm, new Vector3(0f, 10.2f, 0f), new Vector3(4.2f, 2.8f, 4.2f), _roofTileMat);
            CreateBox("Door", wm, new Vector3(-2.25f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 1.25f), _woodTimberMat, collider: false);

            GameObject hub = new GameObject("WindmillBladeHub");
            hub.transform.SetParent(wm, false);
            hub.transform.localPosition = new Vector3(-2.1f, 8.6f, 0f);
            hub.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _windmillBlades = hub.transform;

            CreateCylinder("HubCore", _windmillBlades, Vector3.zero, new Vector3(0.6f, 0.35f, 0.6f), _woodTimberMat, collider: false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                Transform arm = new GameObject($"Blade_{i}").transform;
                arm.SetParent(_windmillBlades, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                CreateBox("Spar", arm, new Vector3(0f, 2.8f, 0f), new Vector3(0.22f, 5.6f, 0.14f), _woodTimberMat, collider: false);
                CreateBox("Sail", arm, new Vector3(0.48f, 3.3f, 0.04f), new Vector3(0.95f, 4.0f, 0.05f), _whiteTrimMat, collider: false);
            }
        }

        private void BuildStoneWaterWell(Transform parent, Vector3 pos)
        {
            Transform w = new GameObject("FarmWaterWell").transform;
            w.SetParent(parent, false);
            w.position = pos;
            CreateCylinder("WellRing", w, new Vector3(0f, 0.55f, 0f), new Vector3(2.1f, 0.55f, 2.1f), _stonePathMat, collider: true);
            CreateCylinder("WellWater", w, new Vector3(0f, 0.92f, 0f), new Vector3(1.65f, 0.10f, 1.65f), _lakeWaterMat, collider: false);
            CreateBox("WellPostL", w, new Vector3(-0.95f, 1.6f, 0f), new Vector3(0.16f, 2.1f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("WellPostR", w, new Vector3( 0.95f, 1.6f, 0f), new Vector3(0.16f, 2.1f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("WellRoof", w, new Vector3(0f, 2.7f, 0f), new Vector3(2.6f, 0.24f, 1.8f), _roofTileMat, collider: false);
        }

        private void BuildSprinklerPole(Transform parent, Vector3 pos)
        {
            Transform sp = new GameObject("AutoSprinkler").transform;
            sp.SetParent(parent, false);
            sp.position = pos;
            CreateCylinder("Pipe", sp, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), _darkMetalMat, collider: false);
            GameObject head = new GameObject("Head");
            head.transform.SetParent(sp, false);
            head.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            CreateBox("Bar", head.transform, Vector3.zero, new Vector3(0.65f, 0.08f, 0.08f), _flowerYellowMat, collider: false);
            _sprinklerHeads.Add(head.transform);
            sp.gameObject.SetActive(false);
        }

        private void HandleSprinklerUnlocked()
        {
            bool active = FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler;
            for (int i = 0; i < _sprinklerHeads.Count; i++)
            {
                if (_sprinklerHeads[i] != null && _sprinklerHeads[i].parent != null)
                {
                    _sprinklerHeads[i].parent.gameObject.SetActive(active);
                }
            }
        }

        private void BuildFarmTractorDecor(Transform parent, Vector3 pos, float yaw)
        {
            Transform tr = new GameObject("VintageFarmTractor").transform;
            tr.SetParent(parent, false);
            tr.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_FarmTractor", tr, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                BoxCollider col = tr.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.2f, 0.1f);
                col.size = new Vector3(2.0f, 2.4f, 3.3f);
                return;
            }

            CreateBox("Hood", tr, new Vector3(0f, 1.15f, 0.75f), new Vector3(1.1f, 0.95f, 1.75f), _tractorRedMat, collider: true);
            CreateBox("Cab", tr, new Vector3(0f, 1.65f, -0.55f), new Vector3(1.35f, 1.35f, 1.15f), _tractorRedMat, collider: true);
            CreateBox("CabRoof", tr, new Vector3(0f, 2.38f, -0.55f), new Vector3(1.50f, 0.12f, 1.35f), _whiteTrimMat, collider: false);
            CreateCylinder("Exhaust", tr, new Vector3(0.45f, 2.0f, 1.15f), new Vector3(0.12f, 0.55f, 0.12f), _darkMetalMat, collider: false);
            // Big rear wheels + smaller front wheels
            CreateCylinder("WheelRL", tr, new Vector3(-0.85f, 0.72f, -0.65f), new Vector3(1.44f, 0.24f, 1.44f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelRR", tr, new Vector3( 0.85f, 0.72f, -0.65f), new Vector3(1.44f, 0.24f, 1.44f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelFL", tr, new Vector3(-0.72f, 0.45f,  1.20f), new Vector3(0.90f, 0.18f, 0.90f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelFR", tr, new Vector3( 0.72f, 0.45f,  1.20f), new Vector3(0.90f, 0.18f, 0.90f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void BuildShepherdDog(Transform parent, Vector3 pos)
        {
            GameObject dog = new GameObject("ShepherdDog_Karabas");
            dog.transform.SetParent(parent, false);
            dog.transform.position = pos;
            WorldPhysicsSolidifier.EnsureKinematicActorPhysics(dog, radius: 0.30f, height: 0.78f, centerY: 0.39f);
            _shepherdDog = dog.transform;
            _dogHome = pos;
            _shepherdLegs.Clear();

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_ShepherdDog", _shepherdDog, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                _shepherdSkel = fbx.AddComponent<SkeletalClipPlayer>();
                if (!_shepherdSkel.Initialize("OW_ShepherdDog", "Idle", 0.5f))
                {
                    _shepherdSkel = null;
                    _shepherdHead = OurWorldModelLibrary.FindPart(fbx.transform, "HeadPivot");
                    _shepherdTail = OurWorldModelLibrary.FindPart(fbx.transform, "TailPivot");
                    string[] legNames = { "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
                    for (int i = 0; i < legNames.Length; i++)
                    {
                        Transform leg = OurWorldModelLibrary.FindPart(fbx.transform, legNames[i]);
                        if (leg != null) _shepherdLegs.Add(leg);
                    }
                }
                return;
            }

            Material tanMat = MakeMat(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"), "MAT_DogTan", new Color(0.84f, 0.74f, 0.56f), 0.18f);
            CreateBox("Body", _shepherdDog, new Vector3(0f, 0.52f, 0f), new Vector3(0.38f, 0.34f, 0.86f), tanMat, collider: false);
            CreateBox("Head", _shepherdDog, new Vector3(0f, 0.72f, 0.46f), new Vector3(0.28f, 0.26f, 0.32f), tanMat, collider: false);
            CreateBox("Snout", _shepherdDog, new Vector3(0f, 0.66f, 0.66f), new Vector3(0.16f, 0.14f, 0.16f), _darkMetalMat, collider: false);
            CreateBox("EarL", _shepherdDog, new Vector3(-0.14f, 0.82f, 0.42f), new Vector3(0.08f, 0.14f, 0.08f), _darkMetalMat, collider: false);
            CreateBox("EarR", _shepherdDog, new Vector3( 0.14f, 0.82f, 0.42f), new Vector3(0.08f, 0.14f, 0.08f), _darkMetalMat, collider: false);
            CreateBox("LegFL", _shepherdDog, new Vector3(-0.12f, 0.22f,  0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegFR", _shepherdDog, new Vector3( 0.12f, 0.22f,  0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegBL", _shepherdDog, new Vector3(-0.12f, 0.22f, -0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegBR", _shepherdDog, new Vector3( 0.12f, 0.22f, -0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);

            GameObject tailPiv = new GameObject("Tail");
            tailPiv.transform.SetParent(_shepherdDog, false);
            tailPiv.transform.localPosition = new Vector3(0f, 0.62f, -0.42f);
            _shepherdTail = tailPiv.transform;
            CreateBox("TailMesh", _shepherdTail, new Vector3(0f, 0.14f, -0.06f), new Vector3(0.07f, 0.30f, 0.07f), tanMat, collider: false);
        }

        // =====================================================================
        // 4. WEST EXPANSION: SULTANBEYLİ GÖLETİ & AYDOS ÇAM KORUSU
        // =====================================================================
        private void BuildWestSultanbeyliLakeAndGrove()
        {
            Transform lakeRoot = new GameObject("SultanbeyliLakeAndPineGrove_West").transform;
            lakeRoot.SetParent(transform, false);

            // Scenic boulevard connecting West end of Selman Sokağı (x = -112..-168, z = 0)
            CreateBox("LakeBoulevard", lakeRoot, new Vector3(-138f, 0.02f, 0f), new Vector3(56f, 0.04f, 8.6f), _roadCountryMat, collider: false);
            CreateBox("LakePromenade", lakeRoot, new Vector3(-168f, 0.025f, 0f), new Vector3(8.5f, 0.05f, 38f), _stonePathMat, collider: false);

            // Shimmering Translucent Turquoise Pond (-198, 0, 0) + Sandy Shore Rim & Sloped Wade-In Banks!
            CreateBox("LakeShore_East",  lakeRoot, new Vector3(-171.5f, -0.025f,   0f), new Vector3(5.5f, 0.04f, 46f), _sandShoreMat, collider: false);
            CreateBox("LakeShore_West",  lakeRoot, new Vector3(-225.0f, -0.025f,   0f), new Vector3(6.5f, 0.04f, 46f), _sandShoreMat, collider: false);
            CreateBox("LakeShore_North", lakeRoot, new Vector3(-198.0f, -0.025f,  20.5f), new Vector3(56f, 0.04f, 5.5f), _sandShoreMat, collider: false);
            CreateBox("LakeShore_South", lakeRoot, new Vector3(-198.0f, -0.025f, -20.5f), new Vector3(56f, 0.04f, 5.5f), _sandShoreMat, collider: false);
            // Gentle sandy underwater shore slopes around all 4 edges so players can wade/swim straight back onto land anywhere!
            CreateBox("LakeBankRamp_East",  lakeRoot, new Vector3(-176.5f, -0.92f,   0f), new Vector3(5.8f, 0.24f, 36f), _sandShoreMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            CreateBox("LakeBankRamp_West",  lakeRoot, new Vector3(-219.5f, -0.92f,   0f), new Vector3(5.8f, 0.24f, 36f), _sandShoreMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            CreateBox("LakeBankRamp_North", lakeRoot, new Vector3(-198.0f, -0.92f,  15.5f), new Vector3(48f, 0.24f, 5.8f), _sandShoreMat, collider: true).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            CreateBox("LakeBankRamp_South", lakeRoot, new Vector3(-198.0f, -0.92f, -15.5f), new Vector3(48f, 0.24f, 5.8f), _sandShoreMat, collider: true).transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            GameObject water = CreateCylinder("LakeWater", lakeRoot, new Vector3(-198f, 0.04f, 0f), new Vector3(56f, 0.04f, 42f), _lakeWaterMat, collider: false);
            _lakeSurface = water.transform;

            // Wooden Viewing Pier jutting out over the turquoise lake (pilings grounded deep into sandy lakebed!)
            CreateBox("PierDeck", lakeRoot, new Vector3(-175.5f, 0.36f, 0f), new Vector3(15f, 0.16f, 4.2f), _woodLightMat, collider: true);
            CreateBox("PierRamp", lakeRoot, new Vector3(-167.2f, 0.18f, 0f), new Vector3(2.2f, 0.14f, 4.2f), _woodLightMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            for (int p = 0; p < 4; p++)
            {
                float px = -170f - p * 4.0f;
                CreateCylinder($"PierPilingN_{p}", lakeRoot, new Vector3(px, -0.52f,  1.95f), new Vector3(0.22f, 1.42f, 0.22f), _woodTimberMat, collider: true);
                CreateCylinder($"PierPilingS_{p}", lakeRoot, new Vector3(px, -0.52f, -1.95f), new Vector3(0.22f, 1.42f, 0.22f), _woodTimberMat, collider: true);
            }
            SpawnFarmLantern(lakeRoot, new Vector3(-182.2f, 0.44f,  1.8f));
            SpawnFarmLantern(lakeRoot, new Vector3(-182.2f, 0.44f, -1.8f));
            CreateWorldSign(lakeRoot, "SULTANBEYLİ GÖLETİ", new Vector3(-166f, 0f, 4.8f), -90f, _woodTimberMat, new Color(0.14f, 0.56f, 0.68f));

            // Floating Emerald Lily Pads & Pink Lotus Blossoms
            Vector3[] lilies =
            {
                new Vector3(-192f, 0.07f,  9f), new Vector3(-196f, 0.07f, 11f), new Vector3(-206f, 0.07f,  6f),
                new Vector3(-202f, 0.07f, -10f), new Vector3(-189f, 0.07f, -8f), new Vector3(-212f, 0.07f, -4f)
            };
            for (int i = 0; i < lilies.Length; i++)
            {
                CreateCylinder($"LilyPad_{i}", lakeRoot, lilies[i], new Vector3(1.15f, 0.03f, 1.15f), _leafBrightMat, collider: false);
                if (i % 2 == 0)
                {
                    CreateSphere($"Lotus_{i}", lakeRoot, lilies[i] + Vector3.up * 0.14f, new Vector3(0.40f, 0.22f, 0.40f), _blossomPinkMat);
                }
            }

            // 3 Rigged & Skeletal-Animated Swimming Pond Ducks (Blender MCP OW_PondDuck FBX!)
            for (int d = 0; d < 3; d++)
            {
                Transform duck = new GameObject($"PondDuck_{d}").transform;
                duck.SetParent(lakeRoot, false);
                SkeletalClipPlayer duckSkel = null;
                GameObject duckFbx = OurWorldModelLibrary.InstantiateModel("OW_PondDuck", duck, Vector3.zero, Quaternion.identity);
                if (duckFbx != null)
                {
                    if (d > 0)
                    {
                        OurWorldModelLibrary.TintMaterialByKeyword(duckFbx, "DuckEmerald", new Color(0.96f, 0.95f, 0.92f));
                    }
                    duckSkel = duckFbx.AddComponent<SkeletalClipPlayer>();
                    if (!duckSkel.Initialize("OW_PondDuck", "Swim", d * 1.4f))
                    {
                        duckSkel = null;
                    }
                }
                else
                {
                    CreateSphere("DuckBody", duck, new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.52f), _whiteTrimMat);
                    CreateSphere("DuckHead", duck, new Vector3(0f, 0.28f, 0.22f), new Vector3(0.20f, 0.20f, 0.22f), d == 0 ? _leafBrightMat : _whiteTrimMat);
                    CreateBox("DuckBeak", duck, new Vector3(0f, 0.26f, 0.36f), new Vector3(0.09f, 0.06f, 0.14f), _flowerYellowMat, collider: false);
                }
                _ducks.Add((duck, duckSkel, new Vector3(-198f + (d - 1) * 7f, 0f, (d - 1) * 5f), 5.5f + d * 2.2f, 0.36f + d * 0.08f, d * 2.1f));
            }

            // Lakeside Gazebos + Warm Promenade Lanterns + Lush Aydos Pine & Cherry-Blossom Grove!
            BuildGazebo(lakeRoot, new Vector3(-168f, 0f,  15f));
            BuildGazebo(lakeRoot, new Vector3(-168f, 0f, -15f));
            SpawnFarmLantern(lakeRoot, new Vector3(-164.5f, 0f,  11.8f));
            SpawnFarmLantern(lakeRoot, new Vector3(-164.5f, 0f, -11.8f));
            SpawnFarmLantern(lakeRoot, new Vector3(-164.5f, 0f,   3.4f));
            SpawnFlowerBed(lakeRoot, new Vector3(-166f, 0.03f,  8.5f), 90f);
            SpawnFlowerBed(lakeRoot, new Vector3(-166f, 0.03f, -8.5f), 90f);

            for (int i = 0; i < 24; i++)
            {
                float a = (i / 24f) * Mathf.PI * 2f;
                // Keep East pier approach clear
                if (Mathf.Cos(a) > 0.72f && Mathf.Abs(Mathf.Sin(a)) < 0.38f) continue;
                float rx = -198f + Mathf.Cos(a) * (35f + (i % 3) * 5f);
                float rz = Mathf.Sin(a) * (27f + (i % 2) * 6f);
                if (i % 4 == 0) SpawnBlossomTree(lakeRoot, new Vector3(rx, 0f, rz), 1.05f);
                else SpawnPineTree(lakeRoot, new Vector3(rx, 0f, rz), 1.15f + (i % 3) * 0.2f);
            }

            // Boulevard blossom trees & warm lanterns (keep x=[-156..-136] north side clear for Aydos Car Wash!)
            for (int i = 0; i < 5; i++)
            {
                float bx = -120f - i * 9.5f;
                if (bx > -136f || bx < -156f)
                {
                    SpawnBlossomTree(lakeRoot, new Vector3(bx, 0f, 6.8f), 0.95f);
                }
                SpawnPineTree(lakeRoot, new Vector3(bx, 0f, -6.8f), 1.05f);
                if (i % 2 == 0) SpawnFarmLantern(lakeRoot, new Vector3(bx + 4f, 0f, -5.2f));
            }
        }

        // =====================================================================
        // 5. HAMİDİYE NEIGHBORHOOD SCENIC BEAUTIFICATION
        // =====================================================================
        private void BeautifyHamidiyePlazasAndAvenues()
        {
            Transform beautyRoot = new GameObject("HamidiyeScenicBeautification").transform;
            beautyRoot.SetParent(transform, false);

            // Vibrant flowerbeds & cherry-blossom accents framing Fatih Bulvarı Clock Tower Plaza!
            SpawnFlowerBed(beautyRoot, new Vector3(-11.5f, 0.02f, 134.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 11.5f, 0.02f, 134.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3(-11.5f, 0.02f, 147.0f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 11.5f, 0.02f, 147.0f), 0f);
            SpawnBlossomTree(beautyRoot, new Vector3(-17.5f, 0f, 145.5f), 1.05f);
            SpawnBlossomTree(beautyRoot, new Vector3( 17.5f, 0f, 145.5f), 1.05f);
            SpawnPineTree(beautyRoot, new Vector3(-26f, 0f, 148f), 1.2f);
            SpawnPineTree(beautyRoot, new Vector3( 26f, 0f, 148f), 1.2f);

            // Scenic directional guideposts on Selman Sokağı pointing East to Aydos Farm & West to Lake!
            CreateWorldSign(beautyRoot, "AYDOS ÇİFTLİĞİ →", new Vector3(106f, 0f, 5.8f), 0f, _woodTimberMat, new Color(0.20f, 0.62f, 0.34f));
            CreateWorldSign(beautyRoot, "← SULTANBEYLİ GÖLETİ", new Vector3(-106f, 0f, 5.8f), 0f, _woodTimberMat, new Color(0.14f, 0.56f, 0.68f));

            // Colorful flowerbeds & spring blossom trees near Sports Courts & Mosque approach
            SpawnFlowerBed(beautyRoot, new Vector3(-28f, 0.02f, -26.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3(  2f, 0.02f, -26.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 30f, 0.02f, -26.5f), 0f);
            SpawnBlossomTree(beautyRoot, new Vector3( 70f, 0f, -21.5f), 0.96f);
            SpawnBlossomTree(beautyRoot, new Vector3( 94f, 0f, -21.5f), 0.96f);
            SpawnBlossomTree(beautyRoot, new Vector3( 29f, 0f, -26.5f), 0.92f);
        }

        // =====================================================================
        // SHARED MODULAR PROP HELPERS
        // =====================================================================
        private void BuildPaddockFence(Transform parent, float minX, float maxX, float minZ, float maxZ, int gateSide, float gatePos, float gateHalfW)
        {
            // gateSide: 0=North(maxZ), 1=South(minZ), 2=West(minX), 3=East(maxX)
            BuildFenceLine(parent, new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ), gateSide == 0, gatePos, gateHalfW, alongX: true);
            BuildFenceLine(parent, new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), gateSide == 1, gatePos, gateHalfW, alongX: true);
            BuildFenceLine(parent, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), gateSide == 2, gatePos, gateHalfW, alongX: false);
            BuildFenceLine(parent, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), gateSide == 3, gatePos, gateHalfW, alongX: false);
        }

        private void BuildFenceLine(Transform parent, Vector3 a, Vector3 b, bool hasGate, float gateCoord, float gateHalfW, bool alongX)
        {
            float start = alongX ? a.x : a.z;
            float end = alongX ? b.x : b.z;
            float fixedCoord = alongX ? a.z : a.x;

            if (!hasGate)
            {
                BuildFenceSpan(parent, start, end, fixedCoord, alongX);
            }
            else
            {
                float g0 = Mathf.Max(start, gateCoord - gateHalfW);
                float g1 = Mathf.Min(end, gateCoord + gateHalfW);
                if (g0 - start > 1f) BuildFenceSpan(parent, start, g0, fixedCoord, alongX);
                if (end - g1 > 1f) BuildFenceSpan(parent, g1, end, fixedCoord, alongX);
            }
        }

        private void BuildFenceSpan(Transform parent, float s0, float s1, float fixedCoord, bool alongX)
        {
            float len = s1 - s0;
            float mid = (s0 + s1) * 0.5f;
            Vector3 railPos1 = alongX ? new Vector3(mid, 0.88f, fixedCoord) : new Vector3(fixedCoord, 0.88f, mid);
            Vector3 railPos2 = alongX ? new Vector3(mid, 0.44f, fixedCoord) : new Vector3(fixedCoord, 0.44f, mid);
            Vector3 railScale = alongX ? new Vector3(len, 0.12f, 0.08f) : new Vector3(0.08f, 0.12f, len);
            CreateBox("FenceRailTop", parent, railPos1, railScale, _woodLightMat, collider: true);
            CreateBox("FenceRailMid", parent, railPos2, railScale, _woodLightMat, collider: false);

            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 3.4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float c = Mathf.Lerp(s0, s1, i / (float)(posts - 1));
                Vector3 p = alongX ? new Vector3(c, 0.58f, fixedCoord) : new Vector3(fixedCoord, 0.58f, c);
                CreateBox("FencePost", parent, p, new Vector3(0.16f, 1.16f, 0.16f), _woodTimberMat, collider: false);
            }
        }

        private void SpawnHayBale(Transform parent, Vector3 pos)
        {
            CreateCylinder("HayBale", parent, pos, new Vector3(1.15f, 0.65f, 1.15f), _hayGoldMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 25f, 90f);
        }

        private void BuildGazebo(Transform parent, Vector3 pos)
        {
            Transform g = new GameObject("ScenicGazebo").transform;
            g.SetParent(parent, false);
            g.position = pos;
            CreateBox("Deck", g, new Vector3(0f, 0.10f, 0f), new Vector3(4.8f, 0.20f, 4.8f), _woodLightMat, collider: true);
            Vector3[] posts = { new Vector3(-2.1f, 1.55f, -2.1f), new Vector3(2.1f, 1.55f, -2.1f), new Vector3(-2.1f, 1.55f, 2.1f), new Vector3(2.1f, 1.55f, 2.1f) };
            for (int i = 0; i < posts.Length; i++) CreateBox("Post", g, posts[i], new Vector3(0.18f, 2.9f, 0.18f), _woodTimberMat, collider: true);
            CreateBox("Roof", g, new Vector3(0f, 3.12f, 0f), new Vector3(5.6f, 0.28f, 5.6f), _roofTileMat, collider: false);
            CreateBox("Table", g, new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 0.12f, 1.0f), _woodTimberMat, collider: true);
            CreateBox("BenchN", g, new Vector3(0f, 0.45f,  1.2f), new Vector3(1.8f, 0.12f, 0.45f), _woodLightMat, collider: true);
            CreateBox("BenchS", g, new Vector3(0f, 0.45f, -1.2f), new Vector3(1.8f, 0.12f, 0.45f), _woodLightMat, collider: true);
        }

        private void SpawnPineTree(Transform parent, Vector3 pos, float scale)
        {
            Transform t = new GameObject("AydosPine").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.localScale = Vector3.one * scale;
            CreateCylinder("Trunk", t, new Vector3(0f, 1.3f, 0f), new Vector3(0.45f, 1.3f, 0.45f), _woodTimberMat, collider: true);
            CreateSphere("Tier1", t, new Vector3(0f, 2.7f, 0f), new Vector3(2.9f, 2.0f, 2.9f), _pineDarkMat);
            CreateSphere("Tier2", t, new Vector3(0f, 4.1f, 0f), new Vector3(2.2f, 1.8f, 2.2f), _leafBrightMat);
            CreateSphere("Tier3", t, new Vector3(0f, 5.2f, 0f), new Vector3(1.3f, 1.4f, 1.3f), _pineDarkMat);
        }

        private void SpawnBlossomTree(Transform parent, Vector3 pos, float scale)
        {
            Transform t = new GameObject("SpringBlossomTree").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.localScale = Vector3.one * scale;
            CreateCylinder("Trunk", t, new Vector3(0f, 1.4f, 0f), new Vector3(0.42f, 1.4f, 0.42f), _woodTimberMat, collider: true);
            CreateSphere("CrownA", t, new Vector3(0f, 3.2f, 0f), new Vector3(3.1f, 2.2f, 3.1f), _blossomPinkMat);
            CreateSphere("CrownB", t, new Vector3(0.8f, 3.7f, 0.5f), new Vector3(2.0f, 1.7f, 2.0f), _blossomPinkMat);
            CreateSphere("CrownC", t, new Vector3(-0.7f, 3.5f, -0.5f), new Vector3(1.9f, 1.6f, 1.9f), _blossomPinkMat);
        }

        private void SpawnFlowerBed(Transform parent, Vector3 pos, float yaw)
        {
            Transform fb = new GameObject("ScenicFlowerBed").transform;
            fb.SetParent(parent, false);
            fb.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            CreateBox("PlanterBorder", fb, new Vector3(0f, 0.16f, 0f), new Vector3(3.6f, 0.32f, 1.2f), _stonePathMat, collider: true);
            CreateBox("PlanterSoil", fb, new Vector3(0f, 0.28f, 0f), new Vector3(3.2f, 0.12f, 0.9f), _wetSoilMat, collider: false);
            Material[] fMats = { _flowerRedMat, _flowerYellowMat, _flowerPurpleMat };
            for (int i = 0; i < 6; i++)
            {
                float fx = -1.25f + i * 0.50f;
                CreateSphere($"Leaf_{i}", fb, new Vector3(fx, 0.42f, 0f), new Vector3(0.42f, 0.24f, 0.42f), _leafBrightMat);
                CreateSphere($"Bloom_{i}", fb, new Vector3(fx, 0.56f, (i % 2 == 0 ? 0.10f : -0.10f)), new Vector3(0.24f, 0.18f, 0.24f), fMats[i % fMats.Length]);
            }
        }

        private void SpawnFarmLantern(Transform parent, Vector3 pos)
        {
            Transform l = new GameObject("FarmLanternPost").transform;
            l.SetParent(parent, false);
            l.position = pos;
            CreateCylinder("Post", l, new Vector3(0f, 2.2f, 0f), new Vector3(0.16f, 2.2f, 0.16f), _woodTimberMat, collider: true);
            CreateBox("Arm", l, new Vector3(0f, 4.25f, 0.35f), new Vector3(0.12f, 0.12f, 0.8f), _woodTimberMat, collider: false);
            SpawnHangingLantern(l, new Vector3(0f, 4.0f, 0.65f));
        }

        private void SpawnHangingLantern(Transform parent, Vector3 localPos)
        {
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "WarmFarmLantern";
            bulb.transform.SetParent(parent, false);
            bulb.transform.localPosition = localPos;
            bulb.transform.localScale = Vector3.one * 0.32f;
            Destroy(bulb.GetComponent<Collider>());
            MeshRenderer mr = bulb.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _whiteTrimMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            Light pl = bulb.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1.0f, 0.82f, 0.48f);
            pl.range = 36f;
            pl.intensity = 44f;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            NightLight nl = bulb.AddComponent<NightLight>();
            nl.Configure(pl, new[] { mr }, new Color(1.0f, 0.84f, 0.50f) * 4.2f);
            if (NeighborhoodNightLighting.Instance != null)
            {
                NeighborhoodNightLighting.Instance.RegisterDynamicOutdoorLight(nl);
            }
        }

        private void CreateWorldSign(Transform parent, string text, Vector3 pos, float yaw, Material postMat, Color boardCol)
        {
            Transform s = new GameObject($"Sign_{text}").transform;
            s.SetParent(parent, false);
            s.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            CreateCylinder("Post", s, new Vector3(0f, 1.05f, 0f), new Vector3(0.14f, 1.05f, 0.14f), postMat, collider: true);
            Material bm = MakeMat(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"), "MAT_SignBoard", boardCol, 0.25f);
            CreateBox("Board", s, new Vector3(0f, 2.0f, 0f), new Vector3(2.8f, 0.65f, 0.10f), bm, collider: false);
            GameObject to = new GameObject("Label");
            to.transform.SetParent(s, false);
            to.transform.localPosition = new Vector3(0f, 2.0f, -0.07f);
            TextMesh tm = to.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.048f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private void RedistributeClusteredSelmanCars()
        {
            // Move excess parked cars that were clustered right around No:48 / Selman St.
            // out to outer exploration districts (Lake, Farm, Park, Aziziye) so PlayerSedan stays at home
            // while players discover vehicles everywhere across the expanded map!
            (string carName, Vector3 targetPos, float yaw)[] relocations =
            {
                ("Car_No50_RedVanHatch",      new Vector3(-156.5f, 0.25f,   5.2f), 180f), // Sultanbeyli Göleti İskelesi
                ("Car_No52_WhiteDeliveryVan", new Vector3( 144.0f, 0.25f,  -4.4f),  90f), // Aydos Çiftliği Girişi
                ("Car_No58_MPV",              new Vector3( -32.0f, 0.25f,  43.2f),  90f), // Hamidiye Parkı · Mürşit Cd.
                ("Car_No48_DrivewayPickup",   new Vector3( -22.0f, 0.25f, -112.0f), 90f)  // Aziziye Cd. · İlkokul Önü
            };

            for (int i = 0; i < relocations.Length; i++)
            {
                var rel = relocations[i];
                GameObject car = GameObject.Find(rel.carName);
                if (car == null) continue;
                // Only relocate if still sitting near Selman corridor (avoids overriding already-moved scene builds)
                if (Mathf.Abs(car.transform.position.z) > 22f || Mathf.Abs(car.transform.position.x) > 100f) continue;

                Quaternion rot = Quaternion.Euler(0f, rel.yaw, 0f);
                Rigidbody rb = car.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = rel.targetPos;
                    rb.rotation = rot;
                }
                car.transform.SetPositionAndRotation(rel.targetPos, rot);
            }
        }

        private Material MakeMat(Shader sh, string name, Color col, float smoothness)
        {
            Material m = new Material(sh) { name = name };
            _runtimeMats.Add(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
