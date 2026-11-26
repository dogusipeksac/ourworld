using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Interactive raised soil garden/farm plot supporting 4 crop varieties (Tomato, Pepper, Strawberry, Sunflower):
    /// - Plant selected seed -> Water (or auto-water in Istanbul summer rain / with Auto-Sprinkler) -> Harvest!
    /// - Visually grows through 4 3D procedural plant & fruit stages with gentle wind sway & ripe glow.
    /// - Persisted automatically in SaveData.farmPlots.
    /// </summary>
    public sealed class FarmPlotInteractable : MonoBehaviour
    {
        public static readonly List<FarmPlotInteractable> AllPlots = new List<FarmPlotInteractable>(24);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllPlots.Clear();
            _stemMat = null;
            _leafMat = null;
            _tomatoMat = null;
            _pepperMat = null;
            _strawberryMat = null;
            _sunflowerPetalMat = null;
            _sunflowerCoreMat = null;
            _haloMat = null;
        }

        private string _plotId;
        private InteractableTrigger _trigger;
        private MeshRenderer _soilRenderer;
        private Material _drySoilMat;
        private Material _wetSoilMat;
        private Transform _plantRoot;
        private GameObject _ripeHalo;

        private FarmCropType _crop = FarmCropType.None;
        private float _growthProgress; // 0..1
        private bool _isWatered;
        private int _builtVisualStage = -1;
        private FarmCropType _builtVisualCrop = FarmCropType.None;
        private float _saveSyncTimer;
        private float _promptRefreshTimer;
        private float _swayPhase;

        private static Material _stemMat;
        private static Material _leafMat;
        private static Material _tomatoMat;
        private static Material _pepperMat;
        private static Material _strawberryMat;
        private static Material _sunflowerPetalMat;
        private static Material _sunflowerCoreMat;
        private static Material _haloMat;

        public string PlotId => _plotId;
        public FarmCropType CurrentCrop => _crop;
        public bool IsWatered => _isWatered;
        public bool IsRipe => _crop != FarmCropType.None && _growthProgress >= 0.999f;
        public float GrowthProgress => _growthProgress;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public void Initialize(string plotId, InteractableTrigger trigger, MeshRenderer soilRenderer, Material drySoil, Material wetSoil)
        {
            _plotId = plotId;
            _trigger = trigger;
            _soilRenderer = soilRenderer;
            _drySoilMat = drySoil;
            _wetSoilMat = wetSoil;
            _swayPhase = Random.Range(0f, Mathf.PI * 2f);

            EnsureSharedPlantMaterials();

            GameObject pRoot = new GameObject("PlantVisualRoot");
            pRoot.transform.SetParent(transform, false);
            pRoot.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            _plantRoot = pRoot.transform;

            // Subtle warm glowing ring/orb above ripe crop so harvest-ready beds stand out immediately!
            _ripeHalo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ripeHalo.name = "RipeHarvestGlow";
            _ripeHalo.transform.SetParent(transform, false);
            _ripeHalo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            _ripeHalo.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            Destroy(_ripeHalo.GetComponent<Collider>());
            MeshRenderer hr = _ripeHalo.GetComponent<MeshRenderer>();
            hr.sharedMaterial = _haloMat;
            hr.shadowCastingMode = ShadowCastingMode.Off;
            _ripeHalo.SetActive(false);

            LoadFromSave();
            RefreshSoilAndVisuals(force: true);
            RefreshPrompt();
        }

        private void OnEnable()
        {
            if (!AllPlots.Contains(this)) AllPlots.Add(this);
            FarmEconomyManager.OnFarmDataChanged += RefreshPrompt;
            FarmEconomyManager.OnAutoSprinklerUnlocked += HandleSprinklerUnlocked;
        }

        private void OnDisable()
        {
            AllPlots.Remove(this);
            FarmEconomyManager.OnFarmDataChanged -= RefreshPrompt;
            FarmEconomyManager.OnAutoSprinklerUnlocked -= HandleSprinklerUnlocked;
        }

        private void HandleSprinklerUnlocked()
        {
            if (_crop != FarmCropType.None && !_isWatered)
            {
                _isWatered = true;
                SaveToEntry(persistDisk: false);
                RefreshSoilAndVisuals(force: false);
                RefreshPrompt();
            }
        }

        private void LoadFromSave()
        {
            FarmPlotSaveEntry entry = FarmEconomyManager.GetOrCreatePlotEntry(_plotId);
            _crop = (FarmCropType)Mathf.Clamp(entry.cropType, 0, 4);
            _growthProgress = Mathf.Clamp01(entry.growthProgress);
            _isWatered = entry.isWatered;
        }

        private void SaveToEntry(bool persistDisk)
        {
            FarmPlotSaveEntry entry = FarmEconomyManager.GetOrCreatePlotEntry(_plotId);
            entry.cropType = (int)_crop;
            entry.growthProgress = _growthProgress;
            entry.isWatered = _isWatered;
            if (persistDisk && SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool raining = NeighborhoodWeatherSystem.Instance != null &&
                           NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower;
            bool hasSprinkler = FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler;

            if (_crop != FarmCropType.None)
            {
                if (!_isWatered && (raining || hasSprinkler))
                {
                    _isWatered = true;
                    RefreshSoilAndVisuals(force: false);
                    RefreshPrompt();
                }

                if (_growthProgress < 1f)
                {
                    FarmCropSpec spec = FarmEconomyManager.GetCropSpec(_crop);
                    // Watered crops grow at full speed (and 1.35x faster in refreshing rain!); dry crops grow slowly at 0.18x
                    float speedMult = _isWatered ? (raining ? 1.35f : 1.0f) : 0.18f;
                    _growthProgress = Mathf.Clamp01(_growthProgress + (dt * speedMult) / Mathf.Max(8f, spec.GrowSeconds));

                    int desiredStage = ComputeVisualStage();
                    if (desiredStage != _builtVisualStage)
                    {
                        RefreshSoilAndVisuals(force: false);
                        RefreshPrompt();
                        if (_growthProgress >= 0.999f)
                        {
                            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.7f, spec.FruitColor, count: 8, spread: 0.4f);
                        }
                    }

                    _saveSyncTimer += dt;
                    if (_saveSyncTimer >= 5.0f)
                    {
                        _saveSyncTimer = 0f;
                        SaveToEntry(persistDisk: false);
                    }

                    _promptRefreshTimer += dt;
                    if (_promptRefreshTimer >= 1.5f)
                    {
                        _promptRefreshTimer = 0f;
                        RefreshPrompt();
                    }
                }

                // Gentle sunlit breeze sway synced to Istanbul wind + ripe harvest indicator bob
                if (_plantRoot != null)
                {
                    float windAmp = 1.6f + IstanbulLivingSoundscape.WindStrength01 * 4.4f;
                    float sway = Mathf.Sin(Time.time * (1.8f + IstanbulLivingSoundscape.WindStrength01 * 1.4f) + _swayPhase) * windAmp;
                    _plantRoot.localRotation = Quaternion.Euler(sway * 0.5f, 0f, sway);
                }
                if (_ripeHalo != null && _ripeHalo.activeSelf)
                {
                    float bob = Mathf.Sin(Time.time * 3.4f + _swayPhase) * 0.08f;
                    _ripeHalo.transform.localPosition = new Vector3(0f, 1.08f + bob, 0f);
                }
            }
        }

        public void Interact(PlayerModeController mode)
        {
            if (mode == null || PlayerActionAnimator.IsBusyOn(mode)) return;

            // 1. Empty plot -> Plant selected seed (sprouts right when Deniz finishes patting the soil!)
            if (_crop == FarmCropType.None)
            {
                FarmCropSpec spec = FarmEconomyManager.SelectedCropSpec;
                if (SaveManager.Instance != null && SaveManager.Instance.Data.money < spec.SeedCost)
                {
                    mode.RaiseStatusMessage(IsEn
                        ? $"Need {spec.SeedCost} ₺ for {spec.NameEn} seeds."
                        : $"{spec.NameTr} tohumu için {spec.SeedCost} ₺ gerekiyor.");
                    return;
                }

                PlayerActionAnimator.Play(
                    mode,
                    transform,
                    PlayerActionKind.FarmPlantAndDig,
                    onClimax: () =>
                    {
                        if (SaveManager.Instance != null)
                        {
                            SaveManager.Instance.TrySpend(spec.SeedCost);
                        }

                        _crop = spec.Type;
                        _growthProgress = 0.04f;
                        bool autoWater = (FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler) ||
                                         (NeighborhoodWeatherSystem.Instance != null &&
                                          NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower);
                        _isWatered = autoWater;

                        SaveToEntry(persistDisk: true);
                        RefreshSoilAndVisuals(force: true);
                        RefreshPrompt();

                        GameFeelAudio.PlayEggPickup();
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.35f, new Color(0.42f, 0.78f, 0.28f), count: 8, spread: 0.45f);
                        mode.RaiseStatusMessage(IsEn
                            ? $"Planted {spec.NameEn} (-{spec.SeedCost} ₺)! {(autoWater ? "Auto-watered!" : "Tap again to water it.")}"
                            : $"{spec.NameTr} tohumu ekildi (-{spec.SeedCost} ₺)! {(autoWater ? "Otomatik sulandı!" : "Şimdi sulayarak hızla büyüt.")}");
                    },
                    startStatus: IsEn ? $"Planting {spec.NameEn}..." : $"{spec.NameTr} tohumu ekiliyor...");
                return;
            }

            // 2. Ripe crop -> Harvest physically into wicker basket on climax!
            if (IsRipe)
            {
                FarmCropType harvested = _crop;
                FarmCropSpec spec = FarmEconomyManager.GetCropSpec(harvested);
                string cropName = IsEn ? spec.NameEn : spec.NameTr;

                PlayerActionAnimator.Play(
                    mode,
                    transform,
                    PlayerActionKind.FarmHarvest,
                    onClimax: () =>
                    {
                        int yieldCount = Random.value < 0.25f ? 2 : 1;
                        _crop = FarmCropType.None;
                        _growthProgress = 0f;
                        _isWatered = false;

                        SaveToEntry(persistDisk: false);
                        FarmEconomyManager.AddCropHarvest(harvested, yieldCount);
                        RefreshSoilAndVisuals(force: true);
                        RefreshPrompt();

                        GameFeelAudio.PlayCoinChime();
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.65f, spec.FruitColor, count: 14, spread: 0.55f);
                        mode.RaiseStatusMessage(IsEn
                            ? $"Harvested +{yieldCount} fresh {cropName}! Sell at Farm Market or Selman Market."
                            : $"+{yieldCount} taze {cropName} hasat edildi! Köy Tezgâhı veya Selman Bakkal'da satabilirsin.");
                    },
                    startStatus: IsEn ? $"Harvesting {cropName}..." : $"{cropName} hasat ediliyor...");
                return;
            }

            // 3. Growing & dry -> Water with 3D watering can (soil darkens right as water pours!)
            if (!_isWatered)
            {
                string cropName = FarmEconomyManager.GetCropName(_crop);
                PlayerActionAnimator.Play(
                    mode,
                    transform,
                    PlayerActionKind.FarmWatering,
                    onClimax: () =>
                    {
                        _isWatered = true;
                        _growthProgress = Mathf.Min(1f, _growthProgress + 0.08f);
                        SaveToEntry(persistDisk: true);
                        RefreshSoilAndVisuals(force: false);
                        RefreshPrompt();

                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.55f, new Color(0.32f, 0.78f, 0.98f), count: 12, spread: 0.50f);
                        mode.RaiseStatusMessage(IsEn
                            ? $"Watered {cropName}! Growing fast now."
                            : $"{cropName} sulandı! Şimdi hızla büyüyor.");
                    },
                    startStatus: IsEn ? $"Watering {cropName}..." : $"{cropName} sulanıyor...");
                return;
            }

            // 4. Growing & watered -> Gentle tend (+14% growth boost on climax!)
            string tendName = FarmEconomyManager.GetCropName(_crop);
            PlayerActionAnimator.Play(
                mode,
                transform,
                PlayerActionKind.FarmPlantAndDig,
                onClimax: () =>
                {
                    _growthProgress = Mathf.Min(1f, _growthProgress + 0.14f);
                    SaveToEntry(persistDisk: false);
                    RefreshSoilAndVisuals(force: false);
                    RefreshPrompt();

                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.45f, new Color(0.55f, 0.88f, 0.36f), count: 6, spread: 0.35f);
                    int pct = Mathf.RoundToInt(_growthProgress * 100f);
                    mode.RaiseStatusMessage(IsEn
                        ? $"Tended {tendName} ({pct}% grown)."
                        : $"{tendName} bakımı yapıldı (%{pct}).");
                },
                startStatus: IsEn ? $"Tending {tendName}..." : $"{tendName} bakımı yapılıyor...");
        }

        private int ComputeVisualStage()
        {
            if (_crop == FarmCropType.None) return 0;
            if (_growthProgress >= 0.999f) return 3;
            if (_growthProgress >= 0.42f) return 2;
            return 1;
        }

        private void RefreshSoilAndVisuals(bool force)
        {
            if (_soilRenderer != null)
            {
                _soilRenderer.sharedMaterial = _isWatered ? _wetSoilMat : _drySoilMat;
            }

            int stage = ComputeVisualStage();
            if (_ripeHalo != null)
            {
                _ripeHalo.SetActive(stage == 3);
            }

            if (!force && stage == _builtVisualStage && _crop == _builtVisualCrop) return;
            _builtVisualStage = stage;
            _builtVisualCrop = _crop;

            if (_plantRoot == null) return;
            for (int i = _plantRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_plantRoot.GetChild(i).gameObject);
            }

            if (stage == 0) return;

            // Build 4 clusters across the raised bed (2x2 grid)
            Vector3[] offsets =
            {
                new Vector3(-0.55f, 0f, -0.35f),
                new Vector3( 0.55f, 0f, -0.35f),
                new Vector3(-0.55f, 0f,  0.35f),
                new Vector3( 0.55f, 0f,  0.35f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                BuildPlantCluster(offsets[i], stage, i);
            }
        }

        private void BuildPlantCluster(Vector3 localPos, int stage, int idx)
        {
            GameObject cluster = new GameObject($"Plant_{idx}");
            cluster.transform.SetParent(_plantRoot, false);
            cluster.transform.localPosition = localPos;
            cluster.transform.localRotation = Quaternion.Euler(0f, idx * 47f, 0f);

            if (stage == 1)
            {
                // Fresh sprouting green seedlings
                AddPart(cluster.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.08f, 0.05f), Quaternion.identity, _stemMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(-0.07f, 0.16f, 0f), new Vector3(0.14f, 0.05f, 0.09f), Quaternion.Euler(0f, 0f, 22f), _leafMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0.07f, 0.16f, 0f), new Vector3(0.14f, 0.05f, 0.09f), Quaternion.Euler(0f, 0f, -22f), _leafMat);
                return;
            }

            bool isSunflower = _crop == FarmCropType.Sunflower;
            float bushScale = stage == 2 ? 0.68f : 1.02f;

            // Use our sculpted Blender MCP 3D Crop Models (OW_Crop_Sunflower & OW_Crop_Tomato)!
            string cropModel = isSunflower ? "OW_Crop_Sunflower" : "OW_Crop_Tomato";
            GameObject cropFbx = OurWorldModelLibrary.InstantiateModel(cropModel, cluster.transform, Vector3.zero, Quaternion.identity, Vector3.one * bushScale);
            if (cropFbx != null)
            {
                if (!isSunflower)
                {
                    if (stage == 2)
                    {
                        // Unripe green buds before harvest stage
                        OurWorldModelLibrary.TintMaterialByKeyword(cropFbx, "TomatoRed", new Color(0.24f, 0.62f, 0.20f));
                    }
                    else if (_crop == FarmCropType.Pepper)
                    {
                        OurWorldModelLibrary.TintMaterialByKeyword(cropFbx, "TomatoRed", new Color(0.26f, 0.84f, 0.22f));
                    }
                    else if (_crop == FarmCropType.Strawberry)
                    {
                        OurWorldModelLibrary.TintMaterialByKeyword(cropFbx, "TomatoRed", new Color(0.96f, 0.16f, 0.36f));
                    }
                }
                return;
            }

            if (isSunflower)
            {
                float stemH = stage == 2 ? 0.34f : 0.62f;
                AddPart(cluster.transform, PrimitiveType.Cylinder, new Vector3(0f, stemH * 0.5f, 0f), new Vector3(0.06f, stemH * 0.5f, 0.06f), Quaternion.identity, _stemMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(-0.14f, stemH * 0.45f, 0f), new Vector3(0.24f, 0.06f, 0.14f), Quaternion.Euler(0f, 0f, 18f), _leafMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0.14f, stemH * 0.60f, 0f), new Vector3(0.24f, 0.06f, 0.14f), Quaternion.Euler(0f, 0f, -18f), _leafMat);
                if (stage == 3)
                {
                    // Iconic bright golden sunflower head!
                    AddPart(cluster.transform, PrimitiveType.Cylinder, new Vector3(0f, stemH + 0.08f, 0.04f), new Vector3(0.34f, 0.03f, 0.34f), Quaternion.Euler(74f, 0f, 0f), _sunflowerPetalMat);
                    AddPart(cluster.transform, PrimitiveType.Cylinder, new Vector3(0f, stemH + 0.08f, 0.06f), new Vector3(0.18f, 0.04f, 0.18f), Quaternion.Euler(74f, 0f, 0f), _sunflowerCoreMat);
                }
                return;
            }

            // Lush green garden bush (Tomato / Pepper / Strawberry)
            AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0f, 0.22f * bushScale, 0f), new Vector3(0.46f, 0.34f, 0.46f) * bushScale, Quaternion.identity, _leafMat);
            AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0.10f, 0.34f * bushScale, 0.06f), new Vector3(0.32f, 0.26f, 0.32f) * bushScale, Quaternion.identity, _stemMat);

            if (stage == 3)
            {
                Material fruitMat = _crop switch
                {
                    FarmCropType.Pepper => _pepperMat,
                    FarmCropType.Strawberry => _strawberryMat,
                    _ => _tomatoMat
                };
                Vector3 fruitScale = _crop == FarmCropType.Pepper
                    ? new Vector3(0.09f, 0.18f, 0.09f)
                    : (_crop == FarmCropType.Strawberry ? new Vector3(0.11f, 0.13f, 0.11f) : new Vector3(0.15f, 0.14f, 0.15f));

                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(-0.16f, 0.28f, 0.14f), fruitScale, Quaternion.identity, fruitMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0.17f, 0.25f, -0.10f), fruitScale, Quaternion.identity, fruitMat);
                AddPart(cluster.transform, PrimitiveType.Sphere, new Vector3(0.02f, 0.42f, 0.12f), fruitScale, Quaternion.identity, fruitMat);
            }
        }

        private static void AddPart(Transform parent, PrimitiveType prim, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(prim);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void RefreshPrompt()
        {
            if (_trigger == null) return;
            bool isEn = IsEn;

            if (_crop == FarmCropType.None)
            {
                FarmCropSpec spec = FarmEconomyManager.SelectedCropSpec;
                string name = isEn ? spec.NameEn : spec.NameTr;
                _trigger.SetCustomPrompt(isEn
                    ? $"Plant {name} ({spec.SeedCost} ₺)"
                    : $"{name} Ek ({spec.SeedCost} ₺)");
            }
            else if (IsRipe)
            {
                string name = FarmEconomyManager.GetCropName(_crop);
                _trigger.SetCustomPrompt(isEn ? $"Harvest {name}!" : $"{name} Hasat Et!");
            }
            else if (!_isWatered)
            {
                string name = FarmEconomyManager.GetCropName(_crop);
                _trigger.SetCustomPrompt(isEn ? $"Water {name}" : $"{name} Sula");
            }
            else
            {
                int pct = Mathf.RoundToInt(_growthProgress * 100f);
                string name = FarmEconomyManager.GetCropName(_crop);
                _trigger.SetCustomPrompt(isEn ? $"Tend {name} ({pct}%)" : $"{name} Bakımı (%{pct})");
            }
        }

        private static void EnsureSharedPlantMaterials()
        {
            if (_stemMat != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? lit;

            _stemMat = MakeMat(lit, "MAT_Farm_Stem", new Color(0.18f, 0.54f, 0.20f), 0.18f);
            _leafMat = MakeMat(lit, "MAT_Farm_Leaf", new Color(0.24f, 0.68f, 0.24f), 0.22f);
            _tomatoMat = MakeMat(lit, "MAT_Farm_Tomato", new Color(0.92f, 0.16f, 0.12f), 0.65f);
            _pepperMat = MakeMat(lit, "MAT_Farm_Pepper", new Color(0.28f, 0.84f, 0.22f), 0.60f);
            _strawberryMat = MakeMat(lit, "MAT_Farm_Strawberry", new Color(0.96f, 0.16f, 0.34f), 0.55f);
            _sunflowerPetalMat = MakeMat(lit, "MAT_Farm_SunflowerPetal", new Color(0.99f, 0.82f, 0.12f), 0.30f);
            _sunflowerCoreMat = MakeMat(lit, "MAT_Farm_SunflowerCore", new Color(0.28f, 0.16f, 0.08f), 0.15f);

            _haloMat = new Material(unlit) { name = "MAT_Farm_RipeHalo" };
            if (_haloMat.HasProperty("_BaseColor")) _haloMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.30f, 1f));
            if (_haloMat.HasProperty("_Color")) _haloMat.SetColor("_Color", new Color(1.0f, 0.88f, 0.30f, 1f));
        }

        private static Material MakeMat(Shader sh, string name, Color col, float smoothness)
        {
            Material m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
