using System.Collections;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Player
{
    public enum ConsumeAnimKind
    {
        None = 0,
        DrinkWater = 1,
        DrinkHotCup = 2,
        EatFood = 3,
        EatIceCream = 4
    }

    /// <summary>
    /// Realistic 3-stage shop purchase & consumption animation on the player character:
    /// 1) Walks smoothly to the nearest matching shelf / fridge / counter inside the shop
    /// 2) Reaches right arm onto the shelf with 2-bone IK and grabs a visible 3D item prop
    /// 3) Raises the bottle / tea glass / ice-cream cone / snack to the mouth with realistic
    ///    sipping or biting animations (while allowing the player to walk freely once grabbed!).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PlayerConsumeAnimator : MonoBehaviour
    {
        private static PlayerConsumeAnimator _activeInstance;

        private PlayerFootController _foot;
        private CharacterController _cc;
        private Coroutine _activeRoutine;
        private GameObject _heldProp;
        private float _heldPropBaseScale = 1f;
        private ConsumeAnimKind _heldKind;
        private float _sipTiltPhase;
        private float _putAwayProgress;
        private bool _holdingModalLock;
        // Energy / hunger / health already paid for or taken from backpack but not yet granted;
        // flushed on explicit cancel so an interrupted drink/snack never wastes the player's item.
        private float _pendingEnergy;
        private float _pendingHunger;
        private float _pendingHealth;
        // Guards against a routine that finishes synchronously inside StartCoroutine leaving IsPlaying stuck.
        private int _runSerial;
        private int _completedRunSerial;

        private static readonly string[] DrinkPropNames = { "DrinkFridge", "Fridge", "Kitchen", "Vending", "CayOcagi_Fridge", "CayOcagi_Counter", "Shelf" };
        private static readonly string[] IceCreamPropNames = { "DondurmaDolabi", "DrinkFridge", "Shelf" };
        private static readonly string[] ProducePropNames = { "ManavReyonu", "Shelf", "KasaBandi" };
        private static readonly string[] DeliPropNames = { "Sarkuteri", "Shelf", "KasaBandi", "CayOcagi_Counter" };
        private static readonly string[] HotCupPropNames = { "TeaTable", "Kettle", "CayOcagi_Counter", "CayOcagi_Fridge", "KasaBandi", "Shelf" };

        public bool IsPlaying => _activeRoutine != null;
        internal static bool IsAnyPlaying => _activeInstance != null && _activeInstance.IsPlaying;

        public static void PlayPurchase(
            PlayerFootController foot,
            string shopId,
            ConsumeAnimKind kind,
            string itemLabel,
            float energyToRestore,
            Transform explicitTarget = null)
        {
            if (foot == null || kind == ConsumeAnimKind.None)
            {
                if (energyToRestore > 0f && PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(energyToRestore);
                }
                return;
            }

            PlayerConsumeAnimator anim = foot.GetComponent<PlayerConsumeAnimator>();
            if (anim == null)
            {
                anim = foot.gameObject.AddComponent<PlayerConsumeAnimator>();
            }
            anim.StartSequence(foot, shopId, kind, itemLabel, energyToRestore, explicitTarget);
        }

        /// <summary>
        /// Pulls a real 3D fruit bag, snack, or drink directly out of the player's Backpack
        /// and eats/drinks it with 2 realistic bites/sips while allowing free walking!
        /// </summary>
        public static void PlayFromBackpack(
            PlayerFootController foot,
            ShopPropVisual propVisual,
            ConsumeAnimKind kind,
            string itemLabel,
            float hungerRestore,
            float energyRestore,
            float healthRestore)
        {
            if (foot == null || kind == ConsumeAnimKind.None)
            {
                PlayerNeedsController.Instance?.ApplyMealRestore(hungerRestore, energyRestore, healthRestore);
                return;
            }

            PlayerConsumeAnimator anim = foot.GetComponent<PlayerConsumeAnimator>();
            if (anim == null)
            {
                anim = foot.gameObject.AddComponent<PlayerConsumeAnimator>();
            }
            anim.StartBackpackSequence(foot, propVisual, kind, itemLabel, hungerRestore, energyRestore, healthRestore);
        }

        private void Awake()
        {
            _foot = GetComponent<PlayerFootController>();
            _cc = GetComponent<CharacterController>();
            _activeInstance = this;
        }

        private void OnDisable()
        {
            CancelActiveInternal(grantPendingEnergy: false);
        }

        private void OnDestroy()
        {
            CancelActiveInternal(grantPendingEnergy: false);
            if (_activeInstance == this) _activeInstance = null;
        }

        /// <summary>Stops the sequence, restores pose/locks and grants any energy that was paid for but not yet applied.</summary>
        public void CancelActive()
        {
            CancelActiveInternal(grantPendingEnergy: true);
        }

        private void CancelActiveInternal(bool grantPendingEnergy)
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            CleanupVisualsAndLocks();

            float energy = _pendingEnergy;
            float hunger = _pendingHunger;
            float health = _pendingHealth;
            _pendingEnergy = 0f;
            _pendingHunger = 0f;
            _pendingHealth = 0f;
            if (grantPendingEnergy && PlayerNeedsController.Instance != null)
            {
                if (hunger > 0f || health > 0f)
                {
                    PlayerNeedsController.Instance.ApplyMealRestore(hunger, energy, health);
                }
                else if (energy > 0f)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(energy);
                }
            }
        }

        private void StartBackpackSequence(
            PlayerFootController foot,
            ShopPropVisual propVisual,
            ConsumeAnimKind kind,
            string itemLabel,
            float hungerRestore,
            float energyRestore,
            float healthRestore)
        {
            _foot = foot;
            if (_cc == null) _cc = foot.GetComponent<CharacterController>();
            PlayerActionAnimator actionAnim = foot.GetComponent<PlayerActionAnimator>();
            if (actionAnim != null && actionAnim.IsPlaying)
            {
                actionAnim.CancelActive();
            }
            CancelActive();
            int serial = ++_runSerial;
            Coroutine routine = StartCoroutine(ConsumeFromBackpackRoutine(
                serial, propVisual, kind, itemLabel ?? string.Empty, hungerRestore, energyRestore, healthRestore));
            if (_completedRunSerial != serial && _runSerial == serial)
            {
                _activeRoutine = routine;
            }
        }

        private IEnumerator ConsumeFromBackpackRoutine(
            int serial,
            ShopPropVisual propVisual,
            ConsumeAnimKind kind,
            string itemLabel,
            float hungerRestore,
            float energyRestore,
            float healthRestore)
        {
            ProceduralHumanoid hum = _foot != null ? _foot.Humanoid : null;
            PlayerModeController mode = PlayerModeController.Instance;
            if (hum == null || mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                PlayerNeedsController.Instance?.ApplyMealRestore(hungerRestore, energyRestore, healthRestore);
                _completedRunSerial = serial;
                _activeRoutine = null;
                yield break;
            }

            _pendingHunger = Mathf.Max(0f, hungerRestore);
            _pendingEnergy = Mathf.Max(0f, energyRestore);
            _pendingHealth = Mathf.Max(0f, healthRestore);

            _heldKind = kind;
            _sipTiltPhase = 0f;
            _putAwayProgress = 0f;
            if (propVisual == ShopPropVisual.FreshFruit)
            {
                _heldPropBaseScale = 1f;
                _heldProp = SpawnHandFruitProp(itemLabel);
            }
            else
            {
                _heldPropBaseScale = 0.78f;
                _heldProp = ShopPropFactory.BuildProp(null, propVisual, Vector3.zero, uniformScale: _heldPropBaseScale);
            }
            SyncHeldPropToPalm();

            bool isDrink = kind == ConsumeAnimKind.DrinkWater || kind == ConsumeAnimKind.DrinkHotCup;
            int poseMode = isDrink ? 1 : 2;
            bool en = LocalizationService.IsEnglish;

            mode.RaiseStatusMessage(isDrink
                ? (en ? $"Drinking {itemLabel} from Backpack..." : $"Çantadan {itemLabel} içiliyor...")
                : (en ? $"Eating {itemLabel} from Backpack..." : $"Çantadan {itemLabel} yeniyor..."));

            const float consumeTotal = 2.25f;
            bool midApplied = false;
            float t = 0f;

            while (t < consumeTotal)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / consumeTotal);

                float raiseIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / 0.14f));
                float lowerOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.80f) / 0.20f));
                float armWeight = Mathf.Min(raiseIn, lowerOut);
                _putAwayProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.84f) / 0.16f));

                float b1 = Mathf.Sin(Mathf.Clamp01((norm - 0.12f) / 0.28f) * Mathf.PI);
                float b2 = Mathf.Sin(Mathf.Clamp01((norm - 0.48f) / 0.28f) * Mathf.PI);
                float cycle = Mathf.SmoothStep(0f, 1f, Mathf.Max(b1, b2));

                hum.SetConsumePose(poseMode, armWeight, cycle);
                _sipTiltPhase = cycle;

                if (!midApplied && norm >= 0.44f)
                {
                    midApplied = true;
                    _pendingHunger = 0f;
                    _pendingEnergy = 0f;
                    _pendingHealth = 0f;
                    PlayerNeedsController.Instance?.ApplyMealRestore(hungerRestore, energyRestore, healthRestore);
                    GameFeelAudio.PlayUiClick();
                    Color fxColor = isDrink ? new Color(0.32f, 0.86f, 0.98f) : new Color(0.98f, 0.78f, 0.26f);
                    Vector3 fxPos = hum.HeadTransform != null
                        ? hum.HeadTransform.position + Vector3.up * 0.12f
                        : _foot.transform.position + Vector3.up * 1.65f;
                    GameFeelAudio.SpawnBurst(fxPos, fxColor, count: 10, spread: 0.35f);
                    mode.RaiseStatusMessage(en
                        ? $"Delicious! {itemLabel} (+{Mathf.RoundToInt(hungerRestore)} Food · +{Mathf.RoundToInt(energyRestore)} Energy)"
                        : $"Afiyet olsun! {itemLabel} (+{Mathf.RoundToInt(hungerRestore)} Tokluk · +{Mathf.RoundToInt(energyRestore)} Enerji)");
                }
                yield return null;
            }

            _pendingHunger = 0f;
            _pendingEnergy = 0f;
            _pendingHealth = 0f;
            if (!midApplied)
            {
                PlayerNeedsController.Instance?.ApplyMealRestore(hungerRestore, energyRestore, healthRestore);
            }

            _completedRunSerial = serial;
            CleanupVisualsAndLocks();
            _activeRoutine = null;
        }

        private void StartSequence(PlayerFootController foot, string shopId, ConsumeAnimKind kind, string itemLabel, float energyToRestore, Transform explicitTarget = null)
        {
            _foot = foot;
            if (_cc == null) _cc = foot.GetComponent<CharacterController>();
            PlayerActionAnimator actionAnim = foot.GetComponent<PlayerActionAnimator>();
            if (actionAnim != null && actionAnim.IsPlaying)
            {
                actionAnim.CancelActive();
            }
            CancelActive();
            int serial = ++_runSerial;
            Coroutine routine = StartCoroutine(PurchaseAndConsumeRoutine(serial, shopId ?? string.Empty, kind, itemLabel ?? string.Empty, energyToRestore, explicitTarget));
            if (_completedRunSerial != serial && _runSerial == serial)
            {
                _activeRoutine = routine;
            }
        }

        private IEnumerator PurchaseAndConsumeRoutine(int serial, string shopId, ConsumeAnimKind kind, string itemLabel, float energyToRestore, Transform explicitTarget)
        {
            ProceduralHumanoid hum = _foot != null ? _foot.Humanoid : null;
            PlayerModeController mode = PlayerModeController.Instance;
            if (hum == null || mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                if (energyToRestore > 0f && PlayerNeedsController.Instance != null)
                    PlayerNeedsController.Instance.RestoreEnergy(energyToRestore);
                _completedRunSerial = serial;
                _activeRoutine = null;
                yield break;
            }

            _pendingEnergy = Mathf.Max(0f, energyToRestore);

            // Lock player controls briefly during walk-to-shelf & grab (~1.1s total)
            mode.SetModalLock(this, true);
            _holdingModalLock = true;

            // --- STAGE 1: Find nearest matching shelf / fridge / stand & walk up to it ---
            Transform shelfTarget = explicitTarget != null ? explicitTarget : FindBestShelfTarget(shopId, kind, _foot.transform.position);
            Vector3 grabWorldPos;
            Vector3 faceDir = _foot.transform.forward;

            if (shelfTarget != null)
            {
                Vector3 shelfPos = shelfTarget.position;
                Vector3 toPlayer = _foot.transform.position - shelfPos;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 0.04f) toPlayer = -_foot.transform.forward;
                Vector3 standDir = toPlayer.normalized;
                Vector3 standPos = new Vector3(shelfPos.x, _foot.transform.position.y, shelfPos.z) + standDir * 0.92f;

                float walkElapsed = 0f;
                float stuckTimer = 0f;
                const float maxWalkTime = 1.35f;
                while (walkElapsed < maxWalkTime)
                {
                    if (mode.CurrentMode != ControlMode.OnFoot) break;
                    Vector3 delta = standPos - _foot.transform.position;
                    delta.y = 0f;
                    float dist = delta.magnitude;
                    if (dist <= 0.22f) break;

                    float dt = Time.deltaTime;
                    walkElapsed += dt;
                    Vector3 dir = delta / Mathf.Max(0.001f, dist);
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    _foot.transform.rotation = Quaternion.Slerp(_foot.transform.rotation, targetRot, 1f - Mathf.Exp(-10f * dt));

                    float moveSpeed = Mathf.Min(2.8f, dist * 4.5f);
                    Vector3 prevPos = _foot.transform.position;
                    if (_cc != null && _cc.enabled)
                    {
                        _cc.Move((dir * moveSpeed + Vector3.down * 4f) * dt);
                    }
                    // Drive the gait from the distance actually covered so feet don't slide/moonwalk
                    // when the CharacterController is blocked by the shelf, counter or another collider.
                    Vector3 moved = Vector3.ProjectOnPlane(_foot.transform.position - prevPos, Vector3.up);
                    float actualSpeed = dt > 0.0001f ? moved.magnitude / dt : 0f;
                    hum.SetLocomotion(actualSpeed, true, 0f, dir);
                    if (actualSpeed < 0.16f)
                    {
                        stuckTimer += dt;
                        if (stuckTimer >= 0.18f) break;
                    }
                    else
                    {
                        stuckTimer = 0f;
                    }
                    yield return null;
                }

                hum.SetLocomotion(0f, true, 0f, _foot.transform.forward);
                Vector3 lookDelta = shelfPos - _foot.transform.position;
                lookDelta.y = 0f;
                if (lookDelta.sqrMagnitude > 0.01f) faceDir = lookDelta.normalized;

                float defaultGrabY = kind == ConsumeAnimKind.EatIceCream ? 0.96f : 1.24f;
                float footY = _foot.transform.position.y;
                float grabY = (explicitTarget != null && shelfPos.y >= footY + 0.62f && shelfPos.y <= footY + 1.58f)
                    ? shelfPos.y
                    : footY + defaultGrabY;
                float horizToShelf = Mathf.Clamp(lookDelta.magnitude - 0.22f, 0.36f, 0.62f);
                grabWorldPos = new Vector3(_foot.transform.position.x, grabY, _foot.transform.position.z) + faceDir * horizToShelf;
            }
            else
            {
                // Graceful fallback: reach cleanly forward to the counter/stand in front of player
                grabWorldPos = _foot.transform.position + faceDir * 0.56f + Vector3.up * 1.18f;
            }

            // Turn smoothly to face the shelf/fridge
            float turnT = 0f;
            Quaternion shelfFaceRot = Quaternion.LookRotation(faceDir, Vector3.up);
            hum.SetLookAtPoint(true, grabWorldPos);
            while (turnT < 0.30f && Quaternion.Angle(_foot.transform.rotation, shelfFaceRot) > 5f)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                turnT += Time.deltaTime;
                _foot.transform.rotation = Quaternion.Slerp(_foot.transform.rotation, shelfFaceRot, 1f - Mathf.Exp(-14f * Time.deltaTime));
                hum.SetLocomotion(0f, true, 0f, faceDir);
                yield return null;
            }

            // --- STAGE 2: Reach right arm onto the shelf with 2-bone IK, grasp item, bring smoothly to chest hold ---
            const float reachOutDur = 0.34f;
            const float grabHoldDur = 0.16f;
            const float pullBackDur = 0.28f;

            float t = 0f;
            while (t < reachOutDur)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                t += Time.deltaTime;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / reachOutDur));
                hum.SetShelfReach(true, grabWorldPos, w);
                yield return null;
            }

            // Spawn the visible 3D item right in the player's right hand as fingers touch the shelf!
            _heldKind = kind;
            _heldPropBaseScale = 1f;
            _sipTiltPhase = 0f;
            _putAwayProgress = 0f;
            _heldProp = SpawnHandProp(kind);
            SyncHeldPropToPalm();
            GameFeelAudio.PlayUiClick();
            GameFeelAudio.SpawnBurst(grabWorldPos, new Color(0.45f, 0.88f, 0.96f), count: 6, spread: 0.25f);

            t = 0f;
            while (t < grabHoldDur)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                t += Time.deltaTime;
                hum.SetShelfReach(true, grabWorldPos, 1f);
                yield return null;
            }

            // Pull item smoothly from shelf directly into the chest hold pose (seamless handoff to Stage 3, no arm drop!)
            t = 0f;
            while (t < pullBackDur)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pullBackDur));
                Vector3 holdPos = GetChestHoldPosition(hum);
                Vector3 carryPoint = Vector3.Lerp(grabWorldPos, holdPos, u);
                hum.SetShelfReach(true, carryPoint, 1f);
                hum.SetLookAtPoint(true, carryPoint);
                yield return null;
            }

            // Unlock movement now so player can walk around while drinking/eating!
            if (_holdingModalLock && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
                _holdingModalLock = false;
            }

            // --- STAGE 3: Drink water/cup or Eat snack/ice cream with realistic sips/bites & smooth put-away ---
            bool isDrink = kind == ConsumeAnimKind.DrinkWater || kind == ConsumeAnimKind.DrinkHotCup;
            int poseMode = isDrink ? 1 : 2;
            const float consumeTotal = 2.45f;
            bool midEnergyApplied = false;

            hum.SetShelfReach(false, Vector3.zero, 0f);
            hum.SetLookAtPoint(false, Vector3.zero);
            hum.SetConsumePose(poseMode, 1f, 0f);

            if (PlayerModeController.Instance != null)
            {
                bool enVerb = LocalizationService.IsEnglish;
                string verb = kind == ConsumeAnimKind.DrinkWater
                    ? (enVerb ? "Drinking cold drink..." : "İçecek içiliyor...")
                    : (kind == ConsumeAnimKind.DrinkHotCup
                        ? (enVerb ? "Sipping hot drink..." : "Sıcak içecek yudumlanıyor...")
                        : (kind == ConsumeAnimKind.EatIceCream
                            ? (enVerb ? "Enjoying ice cream..." : "Dondurma yeniyor...")
                            : (enVerb ? "Enjoying meal..." : "Afiyetle yeniyor...")));
                PlayerModeController.Instance.RaiseStatusMessage(verb);
            }

            t = 0f;
            while (t < consumeTotal)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / consumeTotal);

                // Already raised seamlessly at weight 1.0 from Stage 2; lower smoothly to side in final 20%
                float armWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.80f) / 0.20f));
                _putAwayProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.84f) / 0.16f));

                // Two distinct natural sips (for drinking) or two crisp bites (for eating) with ease-in/out pause for swallow/chew
                float cycle;
                if (isDrink)
                {
                    float sip1 = Mathf.Sin(Mathf.Clamp01((norm - 0.08f) / 0.32f) * Mathf.PI);
                    float sip2 = Mathf.Sin(Mathf.Clamp01((norm - 0.47f) / 0.31f) * Mathf.PI);
                    cycle = Mathf.SmoothStep(0f, 1f, Mathf.Max(sip1, sip2));
                }
                else
                {
                    float bite1 = Mathf.Sin(Mathf.Clamp01((norm - 0.09f) / 0.28f) * Mathf.PI);
                    float bite2 = Mathf.Sin(Mathf.Clamp01((norm - 0.47f) / 0.29f) * Mathf.PI);
                    cycle = Mathf.SmoothStep(0f, 1f, Mathf.Max(bite1, bite2));
                }

                hum.SetConsumePose(poseMode, armWeight, cycle);

                // Smoothly tilt bottle/cup/snack toward the mouth on each sip/bite
                _sipTiltPhase = cycle;

                // Restore energy + sparkle at the first sip/bite climax
                if (!midEnergyApplied && norm >= 0.45f)
                {
                    midEnergyApplied = true;
                    _pendingEnergy = 0f;
                    if (energyToRestore > 0f && PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(energyToRestore);
                    }
                    GameFeelAudio.PlayUiClick();
                    Color fxColor = isDrink ? new Color(0.32f, 0.84f, 0.96f) : new Color(0.98f, 0.80f, 0.30f);
                    Vector3 fxPos = hum.HeadTransform != null
                        ? hum.HeadTransform.position + Vector3.up * 0.12f
                        : _foot.transform.position + Vector3.up * 1.65f;
                    GameFeelAudio.SpawnBurst(fxPos, fxColor, count: 10, spread: 0.35f);

                    if (PlayerModeController.Instance != null && energyToRestore > 0f)
                    {
                        bool enEnjoy = LocalizationService.IsEnglish;
                        PlayerModeController.Instance.RaiseStatusMessage(enEnjoy
                            ? $"Enjoy! +{Mathf.RoundToInt(energyToRestore)} Energy"
                            : $"Afiyet olsun! +{Mathf.RoundToInt(energyToRestore)} Enerji");
                    }
                }

                yield return null;
            }

            // Guarantee energy is always restored even if interrupted early
            _pendingEnergy = 0f;
            if (!midEnergyApplied && energyToRestore > 0f && PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(energyToRestore);
            }

            _completedRunSerial = serial;
            CleanupVisualsAndLocks();
            _activeRoutine = null;
        }

        private Vector3 GetChestHoldPosition(ProceduralHumanoid hum)
        {
            Transform hTr = hum != null ? hum.transform : _foot.transform;
            if (hum != null && hum.HeadTransform != null)
            {
                return hum.HeadTransform.position + hTr.forward * 0.28f + hTr.right * 0.14f - hTr.up * 0.24f;
            }
            return _foot.transform.position + _foot.transform.forward * 0.32f + _foot.transform.right * 0.14f + Vector3.up * 1.26f;
        }

        private void CleanupVisualsAndLocks()
        {
            if (_foot != null && _foot.Humanoid != null)
            {
                _foot.Humanoid.SetShelfReach(false, Vector3.zero, 0f);
                _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                _foot.Humanoid.SetLookAtPoint(false, Vector3.zero);
            }
            _putAwayProgress = 0f;
            _sipTiltPhase = 0f;

            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }

            if (_holdingModalLock && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
                _holdingModalLock = false;
            }
        }

        private static Transform FindBestShelfTarget(string shopId, ConsumeAnimKind kind, Vector3 playerPos)
        {
            string lowerShop = shopId.ToLowerInvariant();
            string[] preferred;
            if (lowerShop.Contains("manav")) preferred = ProducePropNames;
            else if (lowerShop.Contains("sarkuteri")) preferred = DeliPropNames;
            else if (lowerShop.Contains("dondurma") || kind == ConsumeAnimKind.EatIceCream) preferred = IceCreamPropNames;
            else if (kind == ConsumeAnimKind.DrinkWater) preferred = DrinkPropNames;
            else if (kind == ConsumeAnimKind.DrinkHotCup) preferred = HotCupPropNames;
            else preferred = DeliPropNames;

            Transform best = null;
            float bestScore = float.MaxValue;

            // Search nearby shop props/triggers without expensive full-scene string churn
            Collider[] nearby = Physics.OverlapSphere(playerPos, 11.5f);
            for (int i = 0; i < nearby.Length; i++)
            {
                if (nearby[i] == null) continue;
                Transform tr = nearby[i].transform;
                string objName = tr.name;
                int matchPriority = -1;
                for (int p = 0; p < preferred.Length; p++)
                {
                    if (objName.IndexOf(preferred[p], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchPriority = p;
                        break;
                    }
                }
                if (matchPriority < 0) continue;

                float dist = Vector3.Distance(playerPos, tr.position);
                if (dist > 10.5f) continue;

                // Prioritize preferred prop category, then closest distance
                float score = matchPriority * 4.5f + dist;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = tr;
                }
            }

            return best;
        }

        private void LateUpdate()
        {
            SyncHeldPropToPalm();
        }

        private void SyncHeldPropToPalm()
        {
            if (_heldProp == null || _foot == null || _foot.Humanoid == null || _foot.Humanoid.RightForearm == null) return;

            // RightForearm pivot is at the elbow; palm sits -0.305 m along bone -Y.
            // Orient in world space relative to the character (upright +Y) so bone rotation never flips props sideways/upside-down!
            Vector3 palmWorld = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            bool isDrink = _heldKind == ConsumeAnimKind.DrinkWater || _heldKind == ConsumeAnimKind.DrinkHotCup;
            float tiltX = isDrink ? Mathf.Lerp(0f, -58f, _sipTiltPhase) : Mathf.Lerp(0f, -20f, _sipTiltPhase);
            _heldProp.transform.position = palmWorld - Vector3.up * (0.045f + _putAwayProgress * 0.04f) + _foot.transform.forward * 0.025f;
            _heldProp.transform.rotation = _foot.transform.rotation * Quaternion.Euler(tiltX, 0f, 0f);
            float scaleFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_putAwayProgress - 0.55f) / 0.45f));
            _heldProp.transform.localScale = Vector3.one * Mathf.Max(0.01f, scaleFade * _heldPropBaseScale);
        }

        private static GameObject SpawnHandFruitProp(string itemLabel)
        {
            GameObject root = new GameObject("HeldFruitProp");
            root.transform.localRotation = Quaternion.identity;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            string low = (itemLabel ?? string.Empty).ToLowerInvariant();
            bool isOrange = low.Contains("portakal") || low.Contains("orange");
            bool isStrawberry = low.Contains("çilek") || low.Contains("strawberr");
            Color skin = isOrange
                ? new Color(0.98f, 0.52f, 0.08f)
                : (isStrawberry ? new Color(0.90f, 0.16f, 0.24f) : new Color(0.84f, 0.16f, 0.14f));
            CreatePropPart("FruitBody", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0.045f, 0f), new Vector3(0.088f, 0.084f, 0.088f),
                MakeSimpleMat(urpLit, skin, 0.58f, 0.04f));
            CreatePropPart("Stem", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.092f, 0f), new Vector3(0.010f, 0.018f, 0.010f),
                MakeSimpleMat(urpLit, new Color(0.36f, 0.22f, 0.10f), 0.20f, 0f));
            CreatePropPart("Leaf", PrimitiveType.Cube, root.transform,
                new Vector3(0.022f, 0.095f, 0f), new Vector3(0.036f, 0.008f, 0.020f),
                MakeSimpleMat(urpLit, new Color(0.20f, 0.66f, 0.24f), 0.32f, 0f));
            return root;
        }

        private static GameObject SpawnHandProp(ConsumeAnimKind kind)
        {
            GameObject root = new GameObject($"HeldConsumeProp_{kind}");
            root.transform.localRotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

            switch (kind)
            {
                case ConsumeAnimKind.DrinkWater:
                {
                    // Realistic 0.5L Turkish PET water bottle (clear-blue body + white label + blue cap)
                    CreatePropPart("BottleBody", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.03f, 0f), new Vector3(0.068f, 0.095f, 0.068f),
                        MakeSimpleMat(urpLit, new Color(0.56f, 0.84f, 0.96f), 0.88f, 0.08f));
                    CreatePropPart("BottleLabel", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.025f, 0f), new Vector3(0.071f, 0.035f, 0.071f),
                        MakeSimpleMat(urpLit, new Color(0.12f, 0.44f, 0.86f), 0.45f, 0.0f));
                    CreatePropPart("BottleNeck", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.128f, 0f), new Vector3(0.032f, 0.024f, 0.032f),
                        MakeSimpleMat(urpLit, new Color(0.70f, 0.90f, 0.98f), 0.88f, 0.05f));
                    CreatePropPart("BottleCap", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.152f, 0f), new Vector3(0.036f, 0.014f, 0.036f),
                        MakeSimpleMat(urpLit, new Color(0.08f, 0.36f, 0.84f), 0.50f, 0.0f));
                    break;
                }
                case ConsumeAnimKind.DrinkHotCup:
                {
                    // Classic Turkish tea glass / hot coffee cup with warm amber tea
                    CreatePropPart("CupBody", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.02f, 0f), new Vector3(0.072f, 0.056f, 0.072f),
                        MakeSimpleMat(urpLit, new Color(0.92f, 0.89f, 0.82f), 0.65f, 0.05f));
                    CreatePropPart("TeaLiquid", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.062f, 0f), new Vector3(0.064f, 0.012f, 0.064f),
                        MakeSimpleMat(urpLit, new Color(0.46f, 0.16f, 0.05f), 0.85f, 0.05f));
                    break;
                }
                case ConsumeAnimKind.EatIceCream:
                {
                    // Waffle cone + creamy vanilla scoop
                    CreatePropPart("Cone", PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.01f, 0f), new Vector3(0.054f, 0.075f, 0.054f),
                        MakeSimpleMat(urpLit, new Color(0.84f, 0.62f, 0.30f), 0.20f, 0.0f));
                    CreatePropPart("Scoop", PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.095f, 0f), new Vector3(0.085f, 0.082f, 0.085f),
                        MakeSimpleMat(urpLit, new Color(0.98f, 0.92f, 0.84f), 0.32f, 0.0f));
                    break;
                }
                default:
                {
                    // Warm golden Turkish simit / toasted sandwich snack
                    CreatePropPart("SnackBody", PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.02f, 0.02f), new Vector3(0.11f, 0.055f, 0.095f),
                        MakeSimpleMat(urpLit, new Color(0.86f, 0.60f, 0.24f), 0.18f, 0.0f));
                    CreatePropPart("SnackFill", PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.02f, 0.02f), new Vector3(0.115f, 0.018f, 0.098f),
                        MakeSimpleMat(urpLit, new Color(0.92f, 0.78f, 0.26f), 0.30f, 0.0f));
                    break;
                }
            }

            return root;
        }

        private static void CreatePropPart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<int, Material> SimpleMatCache =
            new System.Collections.Generic.Dictionary<int, Material>(32);

        private static Material MakeSimpleMat(Shader shader, Color col, float smoothness, float metallic)
        {
            Color32 c32 = col;
            int shId = shader != null ? shader.GetHashCode() : 0;
            int key = shId ^ (c32.r << 24) ^ (c32.g << 16) ^ (c32.b << 8) ^ c32.a
                      ^ (Mathf.RoundToInt(smoothness * 100f) * 397)
                      ^ (Mathf.RoundToInt(metallic * 100f) * 7919);
            if (SimpleMatCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            SimpleMatCache[key] = m;
            return m;
        }
    }
}
