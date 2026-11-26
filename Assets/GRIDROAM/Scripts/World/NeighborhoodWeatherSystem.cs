using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    public enum NeighborhoodWeatherState
    {
        Clear = 0,
        BreezyOvercast = 1,
        SummerShower = 2
    }

    /// <summary>
    /// Mobile-friendly dynamic Istanbul weather cycle:
    /// - Mostly bright sunny sky (75%), occasional breezy overcast (15%), brief refreshing
    ///   Istanbul summer rain shower (10%) with a lightweight 140-particle camera-orbiting rain emitter.
    /// - Soft rain audio ONLY plays while a summer shower is actually active (completely silent otherwise!).
    /// </summary>
    public sealed class NeighborhoodWeatherSystem : MonoBehaviour
    {
        public static NeighborhoodWeatherSystem Instance { get; private set; }

        public NeighborhoodWeatherState CurrentWeather { get; private set; } = NeighborhoodWeatherState.Clear;

        private ParticleSystem _rainPs;
        private ParticleSystem.EmissionModule _rainEmission;
        private Material _rainMat;
        private AudioSource _rainAudioSource;
        private AudioClip _rainClip;
        private Camera _cachedMainCam;
        private float _stateTimer = 210f; // Start with 3.5 minutes of calm, quiet sunny weather

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<NeighborhoodWeatherSystem>() != null) return;
            GameObject go = new GameObject("Hamidiye_WeatherAndAmbience");
            go.AddComponent<NeighborhoodWeatherSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildRainEmitter();
            BuildRainAudio();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_rainMat != null) Destroy(_rainMat);
            if (_rainClip != null) Destroy(_rainClip);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool raining = CurrentWeather == NeighborhoodWeatherState.SummerShower;
            bool sheltered = IsPlayerIndoors();

            if (_cachedMainCam == null) _cachedMainCam = Camera.main;
            Camera cam = _cachedMainCam;
            if (cam != null && _rainPs != null)
            {
                Vector3 target = cam.transform.position + cam.transform.forward * 7f + Vector3.up * 11f;
                _rainPs.transform.position = target;

                // Stop rain particles whenever player is inside a house or enclosed floors 0..3 of Atlas AVM!
                float desiredRate = (raining && !sheltered) ? 95f : 0f;
                if (!Mathf.Approximately(_rainEmission.rateOverTime.constant, desiredRate))
                {
                    _rainEmission.rateOverTime = desiredRate;
                    if (desiredRate > 0f && !_rainPs.isPlaying) _rainPs.Play();
                    else if (desiredRate <= 0f && sheltered && _rainPs.particleCount > 0) _rainPs.Clear();
                }
            }

            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                AdvanceWeatherState();
            }

            if (_rainAudioSource != null)
            {
                // Muffle rain gently when indoors so roof ambience feels cozy instead of loud outdoor downpour
                float targetRainVol = raining ? (sheltered ? 0.032f : 0.11f) : 0f;
                _rainAudioSource.volume = Mathf.MoveTowards(_rainAudioSource.volume, targetRainVol, dt * 0.08f);
                if (raining && !_rainAudioSource.isPlaying)
                {
                    _rainAudioSource.Play();
                }
                else if (!raining && _rainAudioSource.isPlaying && _rainAudioSource.volume <= 0.001f)
                {
                    _rainAudioSource.Stop();
                }
            }
        }

        public static bool IsPositionSheltered(Vector3 p)
        {
            if (InteriorZone.IsPlayerInsideAny) return true;
            // Hamidiye Atlas AVM enclosed levels 0..3 (Y < 17.8m; open-air 4th floor Rooftop Terrace stays outdoors!)
            if (p.x >= -133.6f && p.x <= -98.4f && p.z >= -50.6f && p.z <= -17.4f && p.y < 17.8f) return true;
            // Aydos Shooting Range covered 4-bay pavilion roof (center 92, -222, width 27m, depth 10m)
            if (p.x >= 78.0f && p.x <= 106.0f && p.z >= -227.5f && p.z <= -216.5f && p.y < 4.0f) return true;
            // Hamidiye Oto Galeri covered showroom canopy (center -115, 71, local Z: -5.6..+8.6)
            if (p.x >= -127.8f && p.x <= -102.2f && p.z >= 65.4f && p.z <= 79.6f && p.y < 5.0f) return true;
            return false;
        }

        private static bool IsPlayerIndoors()
        {
            if (InteriorZone.IsPlayerInsideAny) return true;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return false;
            return IsPositionSheltered(mode.ActiveWorldPosition);
        }

        private void AdvanceWeatherState()
        {
            float roll = Random.value;
            NeighborhoodWeatherState next;
            if (CurrentWeather == NeighborhoodWeatherState.SummerShower)
            {
                next = NeighborhoodWeatherState.Clear;
                _stateTimer = Random.Range(200f, 300f);
            }
            else if (roll < 0.74f)
            {
                next = NeighborhoodWeatherState.Clear;
                _stateTimer = Random.Range(180f, 260f);
            }
            else if (roll < 0.90f)
            {
                next = NeighborhoodWeatherState.BreezyOvercast;
                _stateTimer = Random.Range(70f, 110f);
            }
            else
            {
                next = NeighborhoodWeatherState.SummerShower;
                _stateTimer = Random.Range(45f, 70f);
            }

            SetWeather(next);
        }

        public void SetWeather(NeighborhoodWeatherState state)
        {
            CurrentWeather = state;
            bool raining = state == NeighborhoodWeatherState.SummerShower;

            if (_rainPs != null)
            {
                _rainEmission.rateOverTime = raining ? 95f : 0f;
                if (raining && !_rainPs.isPlaying) _rainPs.Play();
            }

            if (raining && PlayerModeController.Instance != null)
            {
                bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                PlayerModeController.Instance.RaiseStatusMessage(
                    isEn ? "A refreshing Istanbul summer shower begins..." : "Hamidiye'ye serin bir İstanbul yaz yağmuru başladı...");
            }
        }

        private void BuildRainEmitter()
        {
            GameObject rainObj = new GameObject("RainEmitter");
            rainObj.transform.SetParent(transform, false);
            _rainPs = rainObj.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _rainPs.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 140;
            main.startLifetime = 0.85f;
            main.startSpeed = 14.5f;
            main.startSize = 0.045f;
            main.startColor = new Color(0.76f, 0.86f, 0.96f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            _rainEmission = _rainPs.emission;
            _rainEmission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = _rainPs.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 0.5f, 18f);
            rainObj.transform.rotation = Quaternion.Euler(96f, 0f, 0f);

            ParticleSystemRenderer pr = _rainPs.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.renderMode = ParticleSystemRenderMode.Stretch;
                pr.lengthScale = 6.5f;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                _rainMat = new Material(sh);
                if (_rainMat.HasProperty("_BaseColor")) _rainMat.SetColor("_BaseColor", new Color(0.78f, 0.88f, 0.98f, 0.48f));
                pr.sharedMaterial = _rainMat;
            }
        }

        private void BuildRainAudio()
        {
            _rainClip = SynthesizeRainClip(22050, 2.5f);
            _rainAudioSource = gameObject.AddComponent<AudioSource>();
            _rainAudioSource.loop = true;
            _rainAudioSource.spatialBlend = 0f;
            _rainAudioSource.playOnAwake = false;
            _rainAudioSource.clip = _rainClip;
            _rainAudioSource.volume = 0f;
        }

        private static AudioClip SynthesizeRainClip(int sr, float sec)
        {
            int n = Mathf.RoundToInt(sr * sec);
            float[] data = new float[n];
            System.Random rng = new System.Random(5521);
            float lp1 = 0f;
            float lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.14f * (white - lp1);
                lp2 += 0.08f * (lp1 - lp2);
                data[i] = Mathf.Clamp(lp2 * 0.42f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Hamidiye_SummerRain", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
