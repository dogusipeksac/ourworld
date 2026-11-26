using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.World
{
    /// <summary>
    /// Open-world real-life cause-and-effect detail engine for Hamidiye Mahallesi:
    /// - EDS Red Light Camera enforcement (bright flash + shutter + traffic ticket when running RED lights)
    /// - Residential EDS Speed Radar (speeding > 82 km/h through neighborhood streets triggers radar flash + fine)
    /// - Night driving headlight safety reminders & citation if driving dark at night
    /// - Late-night residential horn noise complaints (22:30–07:00)
    /// - Hot backyard Mangal coal burn hazard if player steps directly onto glowing embers
    /// - Procedural SFX bank (car alarm chirp, EDS camera shutter, hydro-lock engine sputter, ember sizzle, underwater gasp)
    /// </summary>
    public sealed class RealWorldDetailsSystem : MonoBehaviour
    {
        public static RealWorldDetailsSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_RealWorldDetailsSystem");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RealWorldDetailsSystem>();
        }

        private AudioSource _sfx;
        private AudioClip _alarmChirpClip;
        private AudioClip _cameraShutterClip;
        private AudioClip _engineSputterClip;
        private AudioClip _fireSizzleClip;
        private AudioClip _drownGaspClip;

        // Lightweight full-screen EDS white flash overlay
        private CanvasGroup _flashGroup;
        private float _flashAlpha;

        // Traffic enforcement state
        private float _edsCheckTimer;
        private Vector3 _prevCarPos;
        private bool _hasPrevCarPos;
        private float _lastRedLightFineTime = -99f;
        private float _speedingDuration;
        private float _lastSpeedFineTime = -99f;
        private float _darkDrivingDuration;
        private bool _warnedDarkDriving;
        private float _lastDarkFineTime = -99f;

        // Late-night honking noise tracker
        private readonly List<float> _recentNightHorns = new List<float>(8);
        private float _lastNoiseFineTime = -99f;

        // Hot backyard mangal burn cooldown
        private float _lastMangalBurnTime = -99f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildAudioBank();
            BuildFlashOverlay();
        }

        private void OnDestroy()
        {
            if (_alarmChirpClip != null) Destroy(_alarmChirpClip);
            if (_cameraShutterClip != null) Destroy(_cameraShutterClip);
            if (_engineSputterClip != null) Destroy(_engineSputterClip);
            if (_fireSizzleClip != null) Destroy(_fireSizzleClip);
            if (_drownGaspClip != null) Destroy(_drownGaspClip);
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // PROCEDURAL REAL-WORLD AUDIO TRIGGERS
        // =====================================================================
        public static void PlayCarAlarmChirp(Vector3 sourcePos, bool highTone)
        {
            if (Instance == null || Instance._sfx == null || Instance._alarmChirpClip == null) return;
            Vector3 listener = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : sourcePos;
            float dist = Vector3.Distance(listener, sourcePos);
            if (dist > 48f) return;
            float atten = 1f - Mathf.SmoothStep(8f, 48f, dist);
            Instance._sfx.pitch = highTone ? 1.14f : 0.88f;
            Instance._sfx.PlayOneShot(Instance._alarmChirpClip, 0.34f * atten);
        }

        public static void PlayCameraFlashShutter()
        {
            if (Instance == null) return;
            if (Instance._sfx != null && Instance._cameraShutterClip != null)
            {
                Instance._sfx.pitch = UnityEngine.Random.Range(0.96f, 1.05f);
                Instance._sfx.PlayOneShot(Instance._cameraShutterClip, 0.52f);
            }
            Instance.TriggerScreenFlash(0.68f);
        }

        public static void PlayEngineSputter()
        {
            if (Instance == null || Instance._sfx == null || Instance._engineSputterClip == null) return;
            Instance._sfx.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
            Instance._sfx.PlayOneShot(Instance._engineSputterClip, 0.56f);
        }

        public static void PlayFireSizzle()
        {
            if (Instance == null || Instance._sfx == null || Instance._fireSizzleClip == null) return;
            Instance._sfx.pitch = UnityEngine.Random.Range(0.94f, 1.12f);
            Instance._sfx.PlayOneShot(Instance._fireSizzleClip, 0.48f);
        }

        public static void PlayDrownGasp()
        {
            if (Instance == null || Instance._sfx == null || Instance._drownGaspClip == null) return;
            Instance._sfx.pitch = UnityEngine.Random.Range(0.90f, 1.04f);
            Instance._sfx.PlayOneShot(Instance._drownGaspClip, 0.52f);
        }

        public void TriggerScreenFlash(float peakAlpha = 0.65f)
        {
            _flashAlpha = Mathf.Max(_flashAlpha, Mathf.Clamp(peakAlpha, 0.1f, 0.85f));
            if (_flashGroup != null)
            {
                _flashGroup.gameObject.SetActive(true);
                _flashGroup.alpha = _flashAlpha;
            }
        }

        // =====================================================================
        // LATE-NIGHT HORN NOISE COMPLAINT
        // =====================================================================
        public void NotifyPlayerCarHorn(Vector3 carPos)
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null) return;
            float h = cycle.CurrentHour;
            bool quietHours = h >= 22.5f || h < 7.0f;
            if (!quietHours) return;

            // Only residential / commercial streets trigger apartment noise complaints (not far rural hills)
            if (Mathf.Abs(carPos.x) > 145f || Mathf.Abs(carPos.z) > 135f) return;

            float now = Time.time;
            _recentNightHorns.Add(now);
            for (int i = _recentNightHorns.Count - 1; i >= 0; i--)
            {
                if (now - _recentNightHorns[i] > 12f) _recentNightHorns.RemoveAt(i);
            }

            if (_recentNightHorns.Count >= 3 && now - _lastNoiseFineTime > 26f)
            {
                _lastNoiseFineTime = now;
                _recentNightHorns.Clear();
                bool en = LocalizationService.IsEnglish;
                NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
                conseq.PlayZabitaWhistle(0.55f);
                conseq.ReportOffense(
                    NeighborhoodOffenseKind.NoiseDisturbance,
                    carPos,
                    en ? "Late-Night Horn Noise" : "Gece Korna Gürültüsü",
                    repPenalty: 8,
                    immediateFineTl: 35,
                    heatDelta: 0.75f);
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? "🌙 Neighbors called Municipal Security over late-night honking! (-35 ₺ · -8 Reputation)"
                    : "🌙 Gece vakti sürekli korna çaldın! Komşular şikayet etti (-35 ₺ · -8 Saygınlık)");
            }
        }

        private void Update()
        {
            // Fade EDS camera white flash quickly
            if (_flashAlpha > 0f && _flashGroup != null)
            {
                _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.unscaledDeltaTime * 2.8f);
                _flashGroup.alpha = _flashAlpha;
                if (_flashAlpha <= 0.002f)
                {
                    _flashAlpha = 0f;
                    _flashGroup.alpha = 0f;
                    _flashGroup.gameObject.SetActive(false);
                }
            }

            if (OurWorld.UI.MainMenuController.IsMenuActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _edsCheckTimer -= dt;
            if (_edsCheckTimer > 0f) return;
            _edsCheckTimer = 0.16f;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            if (mode.IsDriving && mode.CurrentVehicle != null)
            {
                UpdateDrivingRealWorldRules(mode.CurrentVehicle, 0.16f);
            }
            else
            {
                _hasPrevCarPos = false;
                _speedingDuration = 0f;
                _darkDrivingDuration = 0f;
                _warnedDarkDriving = false;

                if (mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null)
                {
                    UpdateOnFootEnvironmentalHazards(mode.FootController);
                }
            }
        }

        private void UpdateDrivingRealWorldRules(SedanVehicleController car, float stepDt)
        {
            Vector3 carPos = car.transform.position;
            float speedKmh = car.SpeedKmh;
            bool en = LocalizationService.IsEnglish;

            // 1. EDS Red Light Camera Enforcement
            if (_hasPrevCarPos && speedKmh >= 18f && Time.time - _lastRedLightFineTime > 12f)
            {
                AmbientTrafficManager traffic = AmbientTrafficManager.Instance;
                IReadOnlyList<TrafficStopLine> lines = traffic != null ? traffic.StopLines : null;
                if (lines != null && lines.Count > 0)
                {
                    Vector3 moveVec = carPos - _prevCarPos;
                    moveVec.y = 0f;
                    if (moveVec.sqrMagnitude > 0.04f)
                    {
                        Vector3 moveDir = moveVec.normalized;
                        for (int i = 0; i < lines.Count; i++)
                        {
                            TrafficStopLine line = lines[i];
                            if (line == null || line.trafficLight == null || !line.trafficLight.IsRed) continue;
                            if (Vector3.Dot(moveDir, line.approachDirection) < 0.62f) continue;

                            Vector3 prevRel = _prevCarPos - line.stopPosition;
                            Vector3 curRel = carPos - line.stopPosition;
                            prevRel.y = 0f;
                            curRel.y = 0f;

                            float lateral = Mathf.Abs(Vector3.Dot(curRel, new Vector3(-line.approachDirection.z, 0f, line.approachDirection.x)));
                            if (lateral > 4.4f) continue;

                            float prevAlong = Vector3.Dot(prevRel, line.approachDirection);
                            float curAlong = Vector3.Dot(curRel, line.approachDirection);
                            // Crossed the stop line from behind (<= 0.5m) into the intersection (> 0.5m) on RED!
                            if (prevAlong <= 0.6f && curAlong > 0.6f && curAlong < 8.5f)
                            {
                                _lastRedLightFineTime = Time.time;
                                PlayCameraFlashShutter();
                                NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
                                conseq.PlayZabitaWhistle(0.56f);
                                conseq.ReportOffense(
                                    NeighborhoodOffenseKind.TrafficViolation,
                                    carPos,
                                    en ? "EDS Red Light" : "EDS Kırmızı Işık",
                                    repPenalty: 8,
                                    immediateFineTl: 65,
                                    heatDelta: 0.70f);
                                VisualEffectsManager.SpawnFloatingText(
                                    carPos + Vector3.up * 2.5f,
                                    en ? "📸 RED LIGHT -65 ₺" : "📸 KIRMIZI IŞIK -65 ₺",
                                    new Color(1.0f, 0.34f, 0.24f),
                                    scale: 1.10f,
                                    duration: 1.35f);
                                PlayerModeController.Instance?.RaiseStatusMessage(en
                                    ? "📸 EDS Red Light Camera flashed! Running a red light cost -65 ₺ & -8 Reputation."
                                    : "📸 EDS Kırmızı Işık Kamerası patladı! Kırmızı ışık ihlali: -65 ₺ · -8 Saygınlık.");
                                break;
                            }
                        }
                    }
                }
            }

            _prevCarPos = carPos;
            _hasPrevCarPos = true;

            // 2. Residential Street EDS Speed Radar (> 82 km/h sustained in built-up neighborhood core)
            bool inResidentialZone = Mathf.Abs(carPos.x) < 118f && carPos.z > -90f && carPos.z < 92f;
            if (inResidentialZone && speedKmh >= 82f)
            {
                _speedingDuration += stepDt;
                if (_speedingDuration >= 0.75f && Time.time - _lastSpeedFineTime > 22f)
                {
                    _lastSpeedFineTime = Time.time;
                    _speedingDuration = 0f;
                    PlayCameraFlashShutter();
                    NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
                    conseq.PlayZabitaWhistle(0.50f);
                    conseq.ReportOffense(
                        NeighborhoodOffenseKind.TrafficViolation,
                        carPos,
                        en ? "EDS Speed Radar" : "EDS Hız Radarı",
                        repPenalty: 6,
                        immediateFineTl: 55,
                        heatDelta: 0.55f);
                    VisualEffectsManager.SpawnFloatingText(
                        carPos + Vector3.up * 2.5f,
                        en ? $"📸 RADAR {Mathf.RoundToInt(speedKmh)} km/h" : $"📸 RADAR {Mathf.RoundToInt(speedKmh)} km/s",
                        new Color(1.0f, 0.62f, 0.18f),
                        scale: 1.08f,
                        duration: 1.30f);
                    PlayerModeController.Instance?.RaiseStatusMessage(en
                        ? $"📸 Speed Radar caught you at {Mathf.RoundToInt(speedKmh)} km/h in a residential street! (-55 ₺)"
                        : $"📸 Mahalle içi hız radarına {Mathf.RoundToInt(speedKmh)} km/s ile yakalandın! (-55 ₺)");
                }
            }
            else
            {
                _speedingDuration = Mathf.Max(0f, _speedingDuration - stepDt * 1.5f);
            }

            // 3. Night Driving Without Headlights Safety Check
            bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            if (isNight && !car.AreHeadlightsOn && speedKmh >= 26f)
            {
                _darkDrivingDuration += stepDt;
                if (_darkDrivingDuration >= 4.2f && !_warnedDarkDriving)
                {
                    _warnedDarkDriving = true;
                    GameFeelAudio.PlayUiClick();
                    PlayerModeController.Instance?.RaiseStatusMessage(en
                        ? "🔦 Your headlights are OFF at night! Press L or tap Lights so other drivers can see you."
                        : "🔦 Gece farların kapalı! L tuşu veya Farlar butonuyla aç, karanlıkta görünmüyorsun.");
                }
                else if (_darkDrivingDuration >= 11.5f && Time.time - _lastDarkFineTime > 30f)
                {
                    _lastDarkFineTime = Time.time;
                    _darkDrivingDuration = 0f;
                    NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                        NeighborhoodOffenseKind.TrafficViolation,
                        carPos,
                        en ? "No Headlights at Night" : "Gece Farsız Sürüş",
                        repPenalty: 4,
                        immediateFineTl: 35,
                        heatDelta: 0.35f);
                    PlayerModeController.Instance?.RaiseStatusMessage(en
                        ? "🚨 Traffic safety citation: driving at night without headlights (-35 ₺)!"
                        : "🚨 Trafik güvenliği cezası: gece farsız sürüş (-35 ₺)!");
                }
            }
            else
            {
                _darkDrivingDuration = 0f;
                if (car.AreHeadlightsOn) _warnedDarkDriving = false;
            }
        }

        private void UpdateOnFootEnvironmentalHazards(PlayerFootController foot)
        {
            if (PlayerNeedsController.Instance == null || PlayerNeedsController.Instance.IsKnockedOut) return;

            // Hot Backyard Mangal coal burn hazard (only if unlocked & standing directly on glowing grill)
            if (HomeDecorManager.IsUnlocked(HomeDecorUpgradeId.BackyardMangal) && Time.time - _lastMangalBurnTime > 2.6f)
            {
                Vector3 mangalPos = HomeDecorManager.BackyardMangalWorldPos;
                Vector3 p = foot.transform.position;
                float dx = p.x - mangalPos.x;
                float dz = p.z - mangalPos.z;
                if (dx * dx + dz * dz <= 0.72f * 0.72f && Mathf.Abs(p.y - mangalPos.y) < 1.35f)
                {
                    _lastMangalBurnTime = Time.time;
                    PlayFireSizzle();
                    GameFeelAudio.SpawnBurst(mangalPos + Vector3.up * 0.85f, new Color(1.0f, 0.48f, 0.14f), count: 10, spread: 0.45f);
                    PlayerNeedsController.Instance.TakeEnvironmentalDamage(
                        5f,
                        mangalPos,
                        "🔥 Dikkat! Mangal kömürleri çok sıcak!",
                        "🔥 Ouch! Those mangal embers are burning hot!",
                        heavy: false);
                }
            }
        }

        // =====================================================================
        // LIGHTWEIGHT EDS WHITE FLASH OVERLAY
        // =====================================================================
        private void BuildFlashOverlay()
        {
            GameObject cGo = new GameObject("EDS_CameraFlashCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            cGo.transform.SetParent(transform, false);
            Canvas c = cGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 180;

            _flashGroup = cGo.GetComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;
            _flashGroup.blocksRaycasts = false;
            _flashGroup.interactable = false;

            GameObject imgGo = new GameObject("FlashRect", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(cGo.transform, false);
            RectTransform rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = imgGo.GetComponent<Image>();
            img.color = new Color(0.96f, 0.98f, 1.0f, 1f);
            img.raycastTarget = false;

            cGo.SetActive(false);
        }

        // =====================================================================
        // PROCEDURAL SFX SYNTHESIS (ZERO EXTERNAL ASSETS)
        // =====================================================================
        private void BuildAudioBank()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f;
            _sfx.playOnAwake = false;

            const int sr = 22050;
            _alarmChirpClip = SynthCarAlarmChirp(sr);
            _cameraShutterClip = SynthCameraShutter(sr);
            _engineSputterClip = SynthEngineSputter(sr);
            _fireSizzleClip = SynthFireSizzle(sr);
            _drownGaspClip = SynthDrownGasp(sr);
        }

        private static AudioClip SynthCarAlarmChirp(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.24f);
            float[] d = new float[n];
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.12f) * Mathf.PI * 0.5f) *
                            (1f - Mathf.Clamp01((u - 0.80f) / 0.20f));
                float freq = Mathf.Lerp(740f, 980f, u);
                ph += (2.0 * Math.PI * freq) / sr;
                float s = (float)(Math.Sin(ph) * 0.55 + Math.Sin(ph * 2.0) * 0.28 + Math.Sin(ph * 3.0) * 0.15);
                d[i] = Mathf.Clamp(s * env * 0.72f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_CarAlarmChirp", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthCameraShutter(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.14f);
            float[] d = new float[n];
            System.Random rng = new System.Random(8421);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float click1 = Mathf.Exp(-t * 95f);
                float click2 = t > 0.055f ? Mathf.Exp(-(t - 0.055f) * 85f) * 0.75f : 0f;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = w - prev * 0.7f;
                prev = w;
                float ping = Mathf.Sin(2f * Mathf.PI * 2400f * t) * click1 * 0.35f;
                d[i] = Mathf.Clamp((hp * (click1 + click2) * 0.78f + ping), -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_EdsCameraShutter", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthEngineSputter(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.52f);
            float[] d = new float[n];
            System.Random rng = new System.Random(6190);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float cough = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI * 6f));
                float env = (1f - u * 0.65f) * (0.35f + 0.65f * cough);
                ph += (2.0 * Math.PI * Mathf.Lerp(110f, 38f, u)) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.18f * (w - lp);
                float hiss = w * 0.25f * (1f - u);
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.65f + lp * 0.50f + hiss) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_EngineHydroSputter", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthFireSizzle(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.30f);
            float[] d = new float[n];
            System.Random rng = new System.Random(5173);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.12f) * Mathf.PI * 0.5f) * Mathf.Exp(-u * 3.8f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = w - prev * 0.82f;
                prev = w;
                d[i] = Mathf.Clamp(hp * env * 0.72f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_FireSizzle", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthDrownGasp(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.28f);
            float[] d = new float[n];
            System.Random rng = new System.Random(3901);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(u * Mathf.PI);
                ph += (2.0 * Math.PI * Mathf.Lerp(135f, 68f, u)) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.10f * (w - lp);
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.52f + lp * 0.65f) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_DrownGasp", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
