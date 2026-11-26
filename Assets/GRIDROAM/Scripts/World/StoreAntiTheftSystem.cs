using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Physical EAS Anti-Theft Store Door Sensor Gate placed at the entrance doorway
    /// of every walk-in store inside Hamidiye Atlas AVM and Selman Bakkal No:58.
    /// If the player crosses the gate carrying unpaid shelf items or wearing unpaid
    /// Try-On items (sunglasses, street cap, jacket), the gate LEDs flash crimson red,
    /// an EAS alarm siren blares, and store security sprints over to catch the player!
    /// </summary>
    public sealed class StoreSecurityGate : MonoBehaviour
    {
        public string StoreName = "Mağaza";
        public Vector3 SafeReturnWorldPos;
        public Renderer[] LedRenderers;
        public Material StandbyLedMat;
        public Material AlarmLedMat;

        private float _flashTimer;
        private bool _flashing;

        private void Update()
        {
            if (!_flashing) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _flashing = false;
                ApplyLedMaterial(StandbyLedMat);
                return;
            }
            bool redOn = Mathf.Repeat(Time.time * 10f, 1f) < 0.55f;
            ApplyLedMaterial(redOn ? AlarmLedMat : StandbyLedMat);
        }

        public void FlashAlarm(float duration = 2.6f)
        {
            _flashing = true;
            _flashTimer = Mathf.Max(_flashTimer, duration);
        }

        private void ApplyLedMaterial(Material mat)
        {
            if (mat == null || LedRenderers == null) return;
            for (int i = 0; i < LedRenderers.Length; i++)
            {
                if (LedRenderers[i] != null) LedRenderers[i].sharedMaterial = mat;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            CheckTrigger(other);
        }

        private void CheckTrigger(Collider other)
        {
            if (other == null || !PlayerShoppingCart.HasActiveItems) return;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.FootController == null) return;
            if (other.transform.root != mode.FootController.transform.root) return;

            Vector3 localPos = transform.InverseTransformPoint(mode.FootController.transform.position);
            if (Mathf.Abs(localPos.y) > 2.4f) return;

            // Only trigger when items originated INSIDE this store (prevents false alarms when
            // carrying outdoor Manav produce INWARD through Selman Bakkal's door to pay at the register!)
            Vector3 originLocal = transform.InverseTransformPoint(PlayerShoppingCart.ActiveShopOrigin);
            if (originLocal.z > 0.35f) return;

            // Trigger immediately when crossing outward past the doorway sensor line (even walking backward!)
            if (localPos.z < -0.05f) return;

            FlashAlarm(2.8f);
            Vector3 returnSpot = SafeReturnWorldPos.sqrMagnitude > 1f
                ? SafeReturnWorldPos
                : transform.TransformPoint(new Vector3(0f, 0.08f, -1.65f));
            StoreAntiTheftSystem.EnsureInstance().TriggerCatch(mode, StoreName, returnSpot, this, transform.forward);
        }

        /// <summary>
        /// Builds twin physical retail EAS anti-theft doorway pedestals with glowing teal/crimson LED strips
        /// and an invisible trigger spanning the doorway threshold (local +Z points OUTWARD toward corridor/street).
        /// </summary>
        public static StoreSecurityGate BuildPhysicalGate(
            Transform parent,
            string storeName,
            Vector3 localPos,
            float doorWidth,
            Vector3 safeReturnLocalPos,
            Material frameMat = null,
            Material glassMat = null,
            Material standbyLedMat = null,
            Material alarmLedMat = null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");

            if (frameMat == null) frameMat = MakeGateMat(lit, new Color(0.14f, 0.17f, 0.22f), 0.72f, 0.55f);
            if (glassMat == null) glassMat = MakeGateMat(lit, new Color(0.55f, 0.78f, 0.86f), 0.88f, 0.12f);
            if (standbyLedMat == null) standbyLedMat = MakeGateEmissive(lit, new Color(0.14f, 0.86f, 0.80f), 1.6f);
            if (alarmLedMat == null) alarmLedMat = MakeGateEmissive(lit, new Color(1.00f, 0.14f, 0.16f), 2.4f);

            GameObject gateRoot = new GameObject($"EAS_SecurityGate_{storeName}");
            gateRoot.transform.SetParent(parent, false);
            gateRoot.transform.localPosition = localPos;
            gateRoot.transform.localRotation = Quaternion.identity;

            float halfSpan = Mathf.Max(0.95f, doorWidth * 0.5f - 0.12f);
            Renderer[] leds = new Renderer[2];

            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -halfSpan : halfSpan;
                Transform post = new GameObject(i == 0 ? "EAS_Post_L" : "EAS_Post_R").transform;
                post.SetParent(gateRoot.transform, false);
                post.localPosition = new Vector3(sx, 0f, 0f);

                // Weighted metallic floor base
                GateBox("Base", post, new Vector3(0f, 0.06f, 0f), new Vector3(0.18f, 0.12f, 0.34f), frameMat);
                // Twin upright brushed-metal frame rails
                GateBox("RailF", post, new Vector3(0f, 0.78f,  0.11f), new Vector3(0.08f, 1.44f, 0.06f), frameMat);
                GateBox("RailB", post, new Vector3(0f, 0.78f, -0.11f), new Vector3(0.08f, 1.44f, 0.06f), frameMat);
                GateBox("TopCap", post, new Vector3(0f, 1.52f, 0f),    new Vector3(0.10f, 0.08f, 0.30f), frameMat);
                // Center acrylic antenna pane
                GateBox("SensorPane", post, new Vector3(0f, 0.78f, 0f), new Vector3(0.04f, 1.32f, 0.17f), glassMat);
                // Vertical glowing EAS status/alarm LED strip
                GameObject led = GateBox("AlarmLedStrip", post, new Vector3(0f, 0.80f, 0.15f), new Vector3(0.045f, 1.34f, 0.025f), standbyLedMat);
                leds[i] = led.GetComponent<Renderer>();
            }

            BoxCollider trigCol = gateRoot.AddComponent<BoxCollider>();
            trigCol.isTrigger = true;
            trigCol.center = new Vector3(0f, 1.15f, 0.12f);
            trigCol.size = new Vector3(halfSpan * 2f - 0.10f, 2.30f, 0.74f);

            StoreSecurityGate gate = gateRoot.AddComponent<StoreSecurityGate>();
            gate.StoreName = storeName;
            gate.SafeReturnWorldPos = parent.TransformPoint(safeReturnLocalPos);
            gate.LedRenderers = leds;
            gate.StandbyLedMat = standbyLedMat;
            gate.AlarmLedMat = alarmLedMat;
            return gate;
        }

        private static GameObject GateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        private static Material MakeGateMat(Shader sh, Color col, float smooth, float metal)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        private static Material MakeGateEmissive(Shader sh, Color col, float intensity)
        {
            Material m = MakeGateMat(sh, col, 0.65f, 0.1f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", col * intensity);
            }
            return m;
        }
    }

    /// <summary>
    /// Coordinates retail shoplifting alarms, uniformed AVM / Store Security Guard interception,
    /// unpaid merchandise confiscation, Try-On garment reversion, and security fine enforcement.
    /// </summary>
    public sealed class StoreAntiTheftSystem : MonoBehaviour
    {
        public static StoreAntiTheftSystem Instance { get; private set; }
        public static bool IsCatchingActive => Instance != null && Instance._isCatching;

        private const int ShopliftFineTl = 35;
        private const int ShopliftRepPenalty = 15;

        private AudioSource _alarmAudio;
        private AudioClip _alarmClip;
        private bool _isCatching;
        private float _lastCatchFinishedTime = -10f;
        private GameObject _guardObj;
        private ProceduralHumanoid _guardHumanoid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static StoreAntiTheftSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_StoreAntiTheftSystem");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<StoreAntiTheftSystem>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildAlarmAudio();
        }

        private void OnDestroy()
        {
            if (_isCatching && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (_alarmClip != null) Destroy(_alarmClip);
            if (_guardObj != null) Destroy(_guardObj);
            if (Instance == this) Instance = null;
        }

        public void TriggerCatch(
            PlayerModeController mode,
            string storeName,
            Vector3 safeStoreSpot,
            StoreSecurityGate gate = null,
            Vector3? outwardDirection = null)
        {
            if (_isCatching || Time.time - _lastCatchFinishedTime < 2.2f) return;
            if (mode == null || mode.FootController == null || !PlayerShoppingCart.HasActiveItems) return;
            StartCoroutine(SecurityCatchRoutine(mode, storeName, safeStoreSpot, gate, outwardDirection));
        }

        private IEnumerator SecurityCatchRoutine(
            PlayerModeController mode,
            string storeName,
            Vector3 safeStoreSpot,
            StoreSecurityGate gate,
            Vector3? outwardDirection)
        {
            _isCatching = true;
            bool en = LocalizationService.IsEnglish;
            string shopLabel = string.IsNullOrEmpty(storeName) ? (en ? "Store" : "Mağaza") : storeName;

            if (gate != null) gate.FlashAlarm(2.8f);
            if (_alarmAudio != null && _alarmClip != null)
            {
                _alarmAudio.PlayOneShot(_alarmClip, 0.72f);
            }

            PlayerFootController foot = mode.FootController;
            mode.SetModalLock(this, true);
            foot.SetMovementEnabled(false);

            Vector3 playerPos = foot.transform.position;
            Vector3 approachDir = outwardDirection.HasValue && outwardDirection.Value.sqrMagnitude > 0.01f
                ? outwardDirection.Value
                : foot.transform.forward;
            approachDir.y = 0f;
            if (approachDir.sqrMagnitude < 0.01f) approachDir = Vector3.forward;
            approachDir.Normalize();

            // Spawn / move a uniformed Security Guard right on the open corridor/sidewalk side to intercept!
            EnsureSecurityGuard();
            Vector3 guardStart = playerPos + approachDir * 3.2f;
            guardStart.y = playerPos.y;
            Vector3 guardStop = playerPos + approachDir * 1.15f;
            guardStop.y = playerPos.y;

            if (_guardObj != null)
            {
                _guardObj.SetActive(true);
                _guardObj.transform.position = guardStart;
                _guardObj.transform.rotation = Quaternion.LookRotation(-approachDir, Vector3.up);
                string guardName = en ? $"{shopLabel} Security" : $"{shopLabel} Güvenlik";
                NpcSpeechBubbleManager.Say(
                    _guardObj.transform,
                    guardName,
                    en ? "STOP! You can't walk out without paying!" : "DUR! Ödeme yapmadan kapıdan çıkamazsın!",
                    3.2f,
                    NpcBubbleMood.Alert,
                    1.95f);
            }

            mode.RaiseStatusMessage(en
                ? $"🚨 ALARM! {shopLabel} Security spotted unpaid items!"
                : $"🚨 ALARM! {shopLabel} güvenliği ödenmemiş ürünleri fark etti!");

            GameFeelAudio.SpawnBurst(playerPos + Vector3.up * 1.5f, new Color(0.96f, 0.20f, 0.22f), 14, 0.75f);

            // Guard sprints directly up to the player and holds up a stop hand
            float sprintElapsed = 0f;
            const float sprintDur = 0.78f;
            while (sprintElapsed < sprintDur)
            {
                sprintElapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sprintElapsed / sprintDur));
                if (_guardObj != null)
                {
                    _guardObj.transform.position = Vector3.Lerp(guardStart, guardStop, u);
                    Vector3 toPlayer = playerPos - _guardObj.transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        _guardObj.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    }
                    _guardHumanoid?.SetLocomotion(3.6f * (1f - u * 0.4f), true, 0f);
                }
                yield return null;
            }

            if (_guardHumanoid != null)
            {
                _guardHumanoid.SetLocomotion(0f, true, 0f);
                _guardHumanoid.PlayWave();
            }

            // Confiscate unpaid items, revert Try-On clothes/accessories, and apply fine
            PlayerShoppingCart.ConfiscateBySecurity(mode);

            int finePaid = 0;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                finePaid = Mathf.Min(ShopliftFineTl, SaveManager.Instance.Data.money);
                if (finePaid > 0)
                {
                    SaveManager.Instance.TrySpend(finePaid);
                }
            }
            NeighborhoodReputation.DeductReputation(ShopliftRepPenalty);
            GameFeelAudio.SpawnBurst(playerPos + Vector3.up * 1.25f, new Color(1.0f, 0.35f, 0.22f), 12, 0.55f);

            yield return new WaitForSeconds(0.68f);

            // Step player politely back inside the safe doorway threshold
            if (foot != null && safeStoreSpot.sqrMagnitude > 1f)
            {
                Vector3 backDir = safeStoreSpot - playerPos;
                backDir.y = 0f;
                Quaternion facing = backDir.sqrMagnitude > 0.04f
                    ? Quaternion.LookRotation(backDir.normalized, Vector3.up)
                    : foot.transform.rotation;
                foot.TeleportTo(safeStoreSpot, facing);
            }

            mode.RaiseStatusMessage(en
                ? $"🚨 Caught by {shopLabel} Security! Unpaid items taken back (-{finePaid} ₺ fine · -{ShopliftRepPenalty} Reputation)."
                : $"🚨 {shopLabel} güvenliği seni yakaladı! Ödenmemiş ürünler geri alındı (-{finePaid} ₺ ceza · -{ShopliftRepPenalty} Saygınlık).");

            mode.SetModalLock(this, false);
            if (foot != null && mode.CurrentMode == ControlMode.OnFoot)
            {
                foot.SetMovementEnabled(true);
            }

            _lastCatchFinishedTime = Time.time;
            _isCatching = false;

            yield return new WaitForSeconds(1.8f);
            if (!_isCatching && _guardObj != null)
            {
                _guardObj.SetActive(false);
            }
        }

        private void EnsureSecurityGuard()
        {
            if (_guardObj != null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material skin = MakeMat(sh, new Color(0.84f, 0.65f, 0.52f), 0.32f, 0f);
            Material uniform = MakeMat(sh, new Color(0.08f, 0.14f, 0.24f), 0.28f, 0.08f);
            Material pants = MakeMat(sh, new Color(0.07f, 0.08f, 0.12f), 0.18f, 0f);
            Material shoes = MakeMat(sh, new Color(0.06f, 0.06f, 0.07f), 0.45f, 0.1f);
            Material hair = MakeMat(sh, new Color(0.10f, 0.08f, 0.07f), 0.25f, 0f);
            Material badge = MakeMat(sh, new Color(0.96f, 0.78f, 0.22f), 0.80f, 0.75f);

            _guardHumanoid = ProceduralHumanoid.Build(null, skin, uniform, pants, shoes, hair, 1.84f);
            _guardObj = _guardHumanoid.gameObject;
            _guardObj.name = "OurWorld_StoreSecurityGuard";
            DontDestroyOnLoad(_guardObj);

            // Gold security badge on chest + dark security cap on head
            if (_guardHumanoid.HeadPivot != null)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "SecurityCap";
                cap.transform.SetParent(_guardHumanoid.HeadPivot, false);
                cap.transform.localPosition = new Vector3(0f, 0.19f, 0f);
                cap.transform.localScale = new Vector3(0.24f, 0.055f, 0.24f);
                Collider c = cap.GetComponent<Collider>();
                if (c != null) Destroy(c);
                Renderer r = cap.GetComponent<Renderer>();
                if (r != null) { r.sharedMaterial = uniform; r.shadowCastingMode = ShadowCastingMode.Off; }

                GameObject emblem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                emblem.name = "CapBadge";
                emblem.transform.SetParent(cap.transform, false);
                emblem.transform.localPosition = new Vector3(0f, 0.15f, 0.48f);
                emblem.transform.localScale = new Vector3(0.24f, 0.45f, 0.12f);
                Collider c2 = emblem.GetComponent<Collider>();
                if (c2 != null) Destroy(c2);
                Renderer r2 = emblem.GetComponent<Renderer>();
                if (r2 != null) { r2.sharedMaterial = badge; r2.shadowCastingMode = ShadowCastingMode.Off; }
            }

            _guardObj.SetActive(false);
        }

        private void BuildAlarmAudio()
        {
            _alarmAudio = gameObject.AddComponent<AudioSource>();
            _alarmAudio.spatialBlend = 0f;
            _alarmAudio.playOnAwake = false;

            const int sr = 22050;
            const float dur = 1.35f;
            int total = Mathf.RoundToInt(sr * dur);
            float[] samples = new float[total];
            // 4 crisp high-pitched retail EAS door-alarm beeps (2450 Hz + 2900 Hz harmonic)
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)sr;
                float cycle = Mathf.Repeat(t, 0.30f);
                bool beepOn = cycle < 0.17f;
                if (!beepOn) { samples[i] = 0f; continue; }
                float env = Mathf.Sin((cycle / 0.17f) * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * 2450f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.35f;
                samples[i] = tone * env * 0.42f;
            }
            _alarmClip = AudioClip.Create("EAS_StoreAlarm", total, 1, sr, false);
            _alarmClip.SetData(samples, 0);
        }

        private static Material MakeMat(Shader sh, Color col, float smooth, float metal)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }
    }
}
