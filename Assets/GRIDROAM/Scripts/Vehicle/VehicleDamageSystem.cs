using System;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.Vehicle
{
    /// <summary>
    /// Real-time vehicle damage, 3D mesh vertex crumpling/denting, impact sparks,
    /// progressive engine hood smoke, procedural metallic crash audio, and Tamirhane repair.
    /// Automatically attached to every SedanVehicleController at runtime.
    /// </summary>
    [RequireComponent(typeof(SedanVehicleController))]
    public class VehicleDamageSystem : MonoBehaviour
    {
        [Header("Health & Deformation")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxDentDepth = 0.28f;

        private class DeformablePart
        {
            public MeshFilter Filter;
            public Mesh RuntimeMesh;
            public Vector3[] BaseVerts;
            public Vector3[] CurrentVerts;
            public bool Dirty;
        }

        private static AudioClip _sharedHeavyCrashClip;
        private static AudioClip _sharedLightDentClip;
        private static AudioClip _sharedBodyThudClip;
        private static Material _sharedSparkMat;
        private static Material _sharedSmokeMat;

        private readonly List<DeformablePart> _parts = new List<DeformablePart>();
        private SedanVehicleController _vehicle;
        private Rigidbody _rb;
        private AudioSource _impactAudio;
        private ParticleSystem _smokePs;
        private ParticleSystem _sparkPs;
        private ThirdPersonCameraRig _cameraRig;

        private float _health = 100f;
        private float _crashCooldown;
        private bool _meshesInitialized;
        private bool _warnedCritical;
        private bool _warnedStalled;

        public float Health => _health;
        public float HealthNormalized => Mathf.Clamp01(_health / Mathf.Max(1f, maxHealth));
        public bool IsStalled => _health <= 0.01f;

        public float EngineTorqueMultiplier
        {
            get
            {
                if (_health <= 0.01f) return 0f;
                if (_health < 25f) return 0.55f;
                if (_health < 55f) return 0.80f;
                return 1f;
            }
        }

        private void Awake()
        {
            _vehicle = GetComponent<SedanVehicleController>();
            _rb = GetComponent<Rigidbody>();
            _health = maxHealth;
            EnsureAudio();
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            InitializeDeformableMeshes();
            CreateSmokeAndSparkParticles();
            UpdateSmokeState();
        }

        private void Update()
        {
            if (_crashCooldown > 0f)
            {
                _crashCooldown -= Time.deltaTime;
            }
        }

        private void InitializeDeformableMeshes()
        {
            if (_meshesInitialized) return;
            _meshesInitialized = true;
            _parts.Clear();

            Transform root = transform.Find("VisualRoot");
            if (root == null) root = transform;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                if (!mf.sharedMesh.isReadable) continue;

                // Skip interior driver, steering wheel, and wheel visuals
                string n = mf.gameObject.name;
                if (n.Contains("Wheel") || n.Contains("Steering") || n.Contains("Driver") || n.Contains("Humanoid"))
                {
                    continue;
                }
                if (IsUnderExcludedParent(mf.transform, root))
                {
                    continue;
                }

                Mesh cloned = Instantiate(mf.sharedMesh);
                cloned.name = mf.sharedMesh.name + "_RuntimeDamaged";
                cloned.MarkDynamic();
                mf.sharedMesh = cloned;

                Vector3[] baseVerts = cloned.vertices;
                Vector3[] curVerts = new Vector3[baseVerts.Length];
                Array.Copy(baseVerts, curVerts, baseVerts.Length);

                _parts.Add(new DeformablePart
                {
                    Filter = mf,
                    RuntimeMesh = cloned,
                    BaseVerts = baseVerts,
                    CurrentVerts = curVerts
                });
            }
        }

        private static bool IsUnderExcludedParent(Transform tr, Transform stopAt)
        {
            Transform p = tr.parent;
            while (p != null && p != stopAt)
            {
                string pn = p.name;
                if (pn.Contains("SeatedDriver") || pn.Contains("SteeringWheel") || pn.Contains("WheelVisual"))
                {
                    return true;
                }
                p = p.parent;
            }
            return false;
        }

        /// <summary>
        /// Applies collision damage, 3D mesh crumpling, impact sparks, metallic crash audio,
        /// and camera impact shake from a vehicle crash.
        /// </summary>
        public void ApplyCrashImpact(Vector3 worldContactPoint, Vector3 impactPushDirWorld, float relativeSpeedMs, bool playerIsDriving)
        {
            if (_crashCooldown > 0f || relativeSpeedMs < 3.0f) return;
            _crashCooldown = 0.14f;

            if (!_meshesInitialized) InitializeDeformableMeshes();

            float severity = Mathf.Clamp01((relativeSpeedMs - 2.8f) / 18.0f);
            float dmg = Mathf.Lerp(4f, 32f, severity);
            _health = Mathf.Max(0f, _health - dmg);

            float radius = Mathf.Lerp(0.75f, 1.55f, severity);
            float strength = Mathf.Lerp(0.055f, 0.22f, severity);
            DeformMeshes(worldContactPoint, impactPushDirWorld, radius, strength);

            EmitSparks(worldContactPoint, -impactPushDirWorld, Mathf.RoundToInt(Mathf.Lerp(10f, 34f, severity)));
            PlayImpactSound(isHeavy: severity > 0.35f, volume: Mathf.Lerp(0.32f, 0.78f, severity));

            if (playerIsDriving)
            {
                if (_cameraRig == null) _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
                if (_cameraRig != null)
                {
                    _cameraRig.AddShake(Mathf.Lerp(0.10f, 0.38f, severity), Mathf.Lerp(0.16f, 0.32f, severity));
                }
            }

            UpdateSmokeState();
            ReportDamageToast(playerIsDriving);
        }

        /// <summary>
        /// Applies visible fender/door denting, physical rigidbody rocking, sparks & clang SFX
        /// when the player punches or kicks a car on foot.
        /// </summary>
        public void ApplyMeleeHit(Vector3 worldHitPoint, Vector3 strikeDirWorld, int comboStep)
        {
            if (!_meshesInitialized) InitializeDeformableMeshes();

            float dmg = comboStep >= 2 ? 15f : (comboStep == 1 ? 9f : 5f);
            _health = Mathf.Max(0f, _health - dmg);

            float radius = comboStep >= 2 ? 0.95f : 0.68f;
            float strength = comboStep >= 2 ? 0.12f : (comboStep == 1 ? 0.08f : 0.05f);
            DeformMeshes(worldHitPoint, strikeDirWorld, radius, strength);

            if (_rb != null)
            {
                float force = comboStep >= 2 ? 1900f : (comboStep == 1 ? 1150f : 720f);
                _rb.AddForceAtPosition(strikeDirWorld.normalized * force + Vector3.up * 260f, worldHitPoint, ForceMode.Impulse);
            }

            EmitSparks(worldHitPoint, -strikeDirWorld, comboStep >= 2 ? 12 : 6);
            PlayImpactSound(isHeavy: comboStep >= 2, volume: comboStep >= 2 ? 0.56f : 0.42f);

            UpdateSmokeState();
            if (_health <= 0.01f && !_warnedStalled && PlayerModeController.Instance != null)
            {
                _warnedStalled = true;
                PlayerModeController.Instance.RaiseStatusMessage("Araç ağır hasar aldı ve motoru bozuldu!");
            }
        }

        /// <summary>
        /// Subtle hood dent & body-impact thud when striking a pedestrian at speed.
        /// </summary>
        public void ApplyPedestrianHitFeedback(Vector3 worldHitPoint, Vector3 pushDirWorld, float speedKmh, bool playerIsDriving)
        {
            if (!_meshesInitialized) InitializeDeformableMeshes();

            float norm = Mathf.Clamp01(speedKmh / 65f);
            if (norm > 0.25f)
            {
                DeformMeshes(worldHitPoint, pushDirWorld, 0.72f, Mathf.Lerp(0.03f, 0.11f, norm));
                _health = Mathf.Max(0f, _health - Mathf.Lerp(1.5f, 7f, norm));
                UpdateSmokeState();
            }

            if (_impactAudio != null && _sharedBodyThudClip != null)
            {
                _impactAudio.pitch = UnityEngine.Random.Range(0.86f, 1.04f);
                _impactAudio.PlayOneShot(_sharedBodyThudClip, Mathf.Lerp(0.42f, 0.78f, norm));
            }

            if (playerIsDriving)
            {
                if (_cameraRig == null) _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
                if (_cameraRig != null)
                {
                    _cameraRig.AddShake(Mathf.Lerp(0.09f, 0.24f, norm), 0.18f);
                }
            }
        }

        /// <summary>
        /// Restores the car to 100% showroom condition (un-crumples all meshes & clears hood smoke).
        /// </summary>
        public void RepairFull()
        {
            _health = maxHealth;
            _warnedCritical = false;
            _warnedStalled = false;

            for (int i = 0; i < _parts.Count; i++)
            {
                DeformablePart part = _parts[i];
                if (part.RuntimeMesh == null || part.BaseVerts == null) continue;
                Array.Copy(part.BaseVerts, part.CurrentVerts, part.BaseVerts.Length);
                part.RuntimeMesh.vertices = part.CurrentVerts;
                part.RuntimeMesh.RecalculateNormals();
                part.RuntimeMesh.RecalculateBounds();
                part.Dirty = false;
            }

            UpdateSmokeState();
        }

        private void DeformMeshes(Vector3 worldPoint, Vector3 worldPushDir, float radius, float strength)
        {
            if (worldPushDir.sqrMagnitude < 0.0001f) worldPushDir = -transform.forward;
            worldPushDir.Normalize();

            float radiusSq = radius * radius;
            for (int i = 0; i < _parts.Count; i++)
            {
                DeformablePart part = _parts[i];
                if (part.Filter == null || part.RuntimeMesh == null) continue;

                Transform tf = part.Filter.transform;
                Vector3 localHit = tf.InverseTransformPoint(worldPoint);
                Vector3 localPush = tf.InverseTransformDirection(worldPushDir).normalized;

                Vector3[] baseV = part.BaseVerts;
                Vector3[] curV = part.CurrentVerts;
                bool changed = false;

                for (int v = 0; v < curV.Length; v++)
                {
                    Vector3 diff = curV[v] - localHit;
                    float distSq = diff.sqrMagnitude;
                    if (distSq >= radiusSq) continue;

                    float dist = Mathf.Sqrt(distSq);
                    float falloff = 1f - (dist / radius);
                    falloff *= falloff;

                    // Subtle deterministic crumple jitter so dents look organic like crumpled sheet metal
                    float crinkle = 0.82f + 0.36f * Mathf.Abs(Mathf.Sin(v * 12.9898f));
                    Vector3 delta = localPush * (strength * falloff * crinkle);
                    Vector3 candidate = curV[v] + delta;

                    // Clamp total displacement from factory vertex so panels never turn inside-out
                    Vector3 totalOffset = candidate - baseV[v];
                    if (totalOffset.magnitude > maxDentDepth)
                    {
                        candidate = baseV[v] + totalOffset.normalized * maxDentDepth;
                    }

                    curV[v] = candidate;
                    changed = true;
                }

                if (changed)
                {
                    part.RuntimeMesh.vertices = curV;
                    part.RuntimeMesh.RecalculateNormals();
                    part.RuntimeMesh.RecalculateBounds();
                    part.Dirty = true;
                }
            }
        }

        private void ReportDamageToast(bool playerIsDriving)
        {
            if (!playerIsDriving || PlayerModeController.Instance == null) return;

            if (_health <= 0.01f && !_warnedStalled)
            {
                _warnedStalled = true;
                PlayerModeController.Instance.RaiseStatusMessage("Motor ağır hasar gördü! Başka araca binebilir veya Tamirhane'ye çekebilirsin.");
            }
            else if (_health < 35f && !_warnedCritical)
            {
                _warnedCritical = true;
                PlayerModeController.Instance.RaiseStatusMessage("Araç duman çıkarıyor! Tamirhane'ye uğrayıp ücretsiz onarabilirsin.");
            }
        }

        private void CreateSmokeAndSparkParticles()
        {
            EnsureParticleMaterials();

            // 1. Progressive Engine Hood Smoke
            GameObject smokeObj = new GameObject("EngineDamageSmoke");
            smokeObj.transform.SetParent(transform, false);
            smokeObj.transform.localPosition = new Vector3(0f, 1.02f, 1.55f);
            smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            _smokePs = smokeObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule smMain = _smokePs.main;
            smMain.loop = true;
            smMain.playOnAwake = false;
            smMain.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            smMain.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            smMain.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.72f);
            smMain.gravityModifier = -0.18f;
            smMain.simulationSpace = ParticleSystemSimulationSpace.World;
            smMain.maxParticles = 60;

            ParticleSystem.ShapeModule smShape = _smokePs.shape;
            smShape.shapeType = ParticleSystemShapeType.Cone;
            smShape.angle = 22f;
            smShape.radius = 0.28f;

            ParticleSystem.EmissionModule smEm = _smokePs.emission;
            smEm.rateOverTime = 0f;

            ParticleSystemRenderer smRend = smokeObj.GetComponent<ParticleSystemRenderer>();
            smRend.sharedMaterial = _sharedSmokeMat;
            smRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smRend.receiveShadows = false;
            _smokePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 2. Collision Metallic Sparks
            GameObject sparkObj = new GameObject("CollisionSparks");
            sparkObj.transform.SetParent(transform, false);
            _sparkPs = sparkObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule spMain = _sparkPs.main;
            spMain.loop = false;
            spMain.playOnAwake = false;
            spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
            spMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
            spMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            spMain.startColor = new Color(1.0f, 0.78f, 0.28f, 1f);
            spMain.gravityModifier = 1.4f;
            spMain.simulationSpace = ParticleSystemSimulationSpace.World;
            spMain.maxParticles = 80;

            ParticleSystem.EmissionModule spEm = _sparkPs.emission;
            spEm.rateOverTime = 0f;

            ParticleSystem.ShapeModule spShape = _sparkPs.shape;
            spShape.shapeType = ParticleSystemShapeType.Cone;
            spShape.angle = 48f;
            spShape.radius = 0.08f;

            ParticleSystemRenderer spRend = sparkObj.GetComponent<ParticleSystemRenderer>();
            spRend.sharedMaterial = _sharedSparkMat;
            spRend.renderMode = ParticleSystemRenderMode.Stretch;
            spRend.lengthScale = 2.4f;
            spRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spRend.receiveShadows = false;
        }

        private void UpdateSmokeState()
        {
            if (_smokePs == null) return;

            ParticleSystem.MainModule main = _smokePs.main;
            ParticleSystem.EmissionModule em = _smokePs.emission;

            if (_health >= 65f)
            {
                em.rateOverTime = 0f;
                if (_smokePs.isPlaying) _smokePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (_health >= 30f)
            {
                main.startColor = new Color(0.84f, 0.86f, 0.88f, 0.42f);
                em.rateOverTime = 12f;
                if (!_smokePs.isPlaying) _smokePs.Play();
            }
            else
            {
                main.startColor = new Color(0.16f, 0.16f, 0.18f, 0.68f);
                em.rateOverTime = _health <= 0.01f ? 32f : 22f;
                if (!_smokePs.isPlaying) _smokePs.Play();
            }
        }

        private void EmitSparks(Vector3 worldPos, Vector3 outwardDir, int count)
        {
            if (_sparkPs == null || count <= 0) return;
            _sparkPs.transform.position = worldPos;
            if (outwardDir.sqrMagnitude > 0.001f)
            {
                _sparkPs.transform.rotation = Quaternion.LookRotation(outwardDir.normalized, Vector3.up);
            }
            _sparkPs.Emit(count);
        }

        private void EnsureParticleMaterials()
        {
            Shader fallback = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (fallback == null) return;

            if (_sharedSparkMat == null)
            {
                _sharedSparkMat = new Material(fallback) { name = "MAT_VehicleDamageSpark" };
                if (_sharedSparkMat.HasProperty("_BaseColor")) _sharedSparkMat.SetColor("_BaseColor", new Color(1.0f, 0.82f, 0.30f, 1f));
                else _sharedSparkMat.color = new Color(1.0f, 0.82f, 0.30f, 1f);
            }
            if (_sharedSmokeMat == null)
            {
                _sharedSmokeMat = new Material(fallback) { name = "MAT_VehicleDamageSmoke" };
                if (_sharedSmokeMat.HasProperty("_Surface")) _sharedSmokeMat.SetFloat("_Surface", 1f);
                if (_sharedSmokeMat.HasProperty("_BaseColor")) _sharedSmokeMat.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.68f, 0.55f));
                else _sharedSmokeMat.color = new Color(0.65f, 0.65f, 0.68f, 0.55f);
                _sharedSmokeMat.renderQueue = 3000;
            }
        }

        private void EnsureAudio()
        {
            // Place crash AudioSource on a dedicated child so the vehicle root keeps a single
            // AudioSource for EngineAudio.OnAudioFilterRead (prevents Unity's multi-AudioSource filter warning).
            Transform existingChild = transform.Find("ImpactAudio");
            GameObject audioObj = existingChild != null ? existingChild.gameObject : new GameObject("ImpactAudio");
            if (existingChild == null)
            {
                audioObj.transform.SetParent(transform, false);
                audioObj.transform.localPosition = Vector3.zero;
            }
            _impactAudio = audioObj.GetComponent<AudioSource>();
            if (_impactAudio == null)
            {
                _impactAudio = audioObj.AddComponent<AudioSource>();
            }
            _impactAudio.playOnAwake = false;
            _impactAudio.loop = false;
            _impactAudio.spatialBlend = 0.65f;

            if (_sharedHeavyCrashClip == null) _sharedHeavyCrashClip = BuildCrashClip(44100, heavy: true);
            if (_sharedLightDentClip == null) _sharedLightDentClip = BuildCrashClip(44100, heavy: false);
            if (_sharedBodyThudClip == null) _sharedBodyThudClip = BuildBodyThudClip(44100);
        }

        private void PlayImpactSound(bool isHeavy, float volume)
        {
            if (_impactAudio == null) return;
            AudioClip clip = isHeavy ? _sharedHeavyCrashClip : _sharedLightDentClip;
            if (clip == null) return;
            _impactAudio.pitch = UnityEngine.Random.Range(0.88f, 1.12f);
            _impactAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioClip BuildCrashClip(int sampleRate, bool heavy)
        {
            int count = Mathf.RoundToInt(sampleRate * (heavy ? 0.42f : 0.18f));
            float[] data = new float[count];
            System.Random rng = new System.Random(heavy ? 88101 : 88202);
            float lp = 0f;
            double p1 = 0.0, p2 = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float env = Mathf.Exp(-t * (heavy ? 6.2f : 12.5f));
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (heavy ? 0.32f : 0.48f) * (white - lp);
                p1 += (2.0 * Math.PI * Mathf.Lerp(heavy ? 185f : 260f, 52f, t)) / sampleRate;
                p2 += (2.0 * Math.PI * (heavy ? 490f : 640f)) / sampleRate;
                float metal = (float)Math.Sin(p1) * 0.65f + (float)Math.Sin(p2) * 0.25f * Mathf.Exp(-t * 16f);
                float crunch = (white * 0.38f + lp * 0.62f) * (heavy ? 0.85f : 0.55f);
                data[i] = Mathf.Clamp((metal + crunch) * env, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(heavy ? "CarHeavyCrash" : "CarDentBang", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildBodyThudClip(int sampleRate)
        {
            int count = Mathf.RoundToInt(sampleRate * 0.16f);
            float[] data = new float[count];
            System.Random rng = new System.Random(99401);
            float lp = 0f;
            double p = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float env = Mathf.Exp(-t * 13f);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.22f * (white - lp);
                p += (2.0 * Math.PI * Mathf.Lerp(115f, 36f, t)) / sampleRate;
                data[i] = Mathf.Clamp(((float)Math.Sin(p) * 0.86f + lp * 0.45f) * env, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("PedestrianImpactThud", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
