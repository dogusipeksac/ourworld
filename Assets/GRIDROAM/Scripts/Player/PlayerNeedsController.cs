using System;
using System.Collections;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    /// <summary>
    /// Light, non-punishing player energy & world utility interactions:
    /// - Energy (0-100, start 80), drains 1 per in-game hour + slight sprint drain; below 20 reduces run speed 25%
    /// - Sleeping in any bed (1.5 s black fade, +8 hours, energy = 100, save)
    /// - Vending machine drink (5 ₺, +8 energy)
    /// - Gas station pump refuelling (2 ₺ per 1%, fills over 3 s while stopped at pump)
    /// </summary>
    public class PlayerNeedsController : MonoBehaviour
    {
        public static PlayerNeedsController Instance { get; private set; }

        [SerializeField] private float startEnergy = 80f;
        [SerializeField] private float startHealth = 100f;
        [SerializeField] private float startHunger = 88f;
        [SerializeField] private float sprintExtraDrainPerSecond = 0.032f;
        [SerializeField] private CanvasGroup sleepFadeCanvasGroup;

        private float _energy = 80f;
        private float _health = 100f;
        private float _hunger = 88f;
        private bool _lastTiredState;
        private bool _lastHungryState;
        private bool _isBusy;
        private bool _isKnockedOut;
        private float _hitInvulnTimer;
        private float _timeSinceLastDamage = 99f;

        public float Energy => _energy;
        public float Health => _health;
        public float Hunger => _hunger;
        public bool IsTired => _energy < 20f;
        public bool IsLowHealth => _health <= 30f;
        public bool IsHungry => _hunger < 28f;
        public bool IsStarving => _hunger < 10f;
        public bool IsBusy => _isBusy;
        public bool IsKnockedOut => _isKnockedOut;

        public event Action<float, bool> OnEnergyChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<float, bool> OnHungerChanged;
        public event Action<string> OnNeedsMessage;

        public void ConfigureSleepFade(CanvasGroup fadeGroup)
        {
            sleepFadeCanvasGroup = fadeGroup;
            if (sleepFadeCanvasGroup != null)
            {
                sleepFadeCanvasGroup.alpha = 0f;
                sleepFadeCanvasGroup.blocksRaycasts = false;
                sleepFadeCanvasGroup.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _energy = Mathf.Clamp(startEnergy, 0f, 100f);
            _health = Mathf.Clamp(startHealth, 1f, 100f);
            _hunger = Mathf.Clamp(startHunger, 0f, 100f);
        }

        private void Start()
        {
            if (SaveManager.Instance != null)
            {
                _energy = Mathf.Clamp(SaveManager.Instance.Data.energy, 0f, 100f);
                float savedHp = SaveManager.Instance.Data.health;
                _health = savedHp <= 0.01f ? 100f : Mathf.Clamp(savedHp, 1f, 100f);
                float savedHunger = SaveManager.Instance.Data.hunger;
                _hunger = savedHunger <= 0.01f ? 85f : Mathf.Clamp(savedHunger, 0f, 100f);
                SaveManager.Instance.OnBeforeSave += HandleBeforeSave;
                SaveManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            }

            _lastTiredState = IsTired;
            _lastHungryState = IsHungry;
            OnEnergyChanged?.Invoke(_energy, _lastTiredState);
            OnHealthChanged?.Invoke(_health, 0f);
            OnHungerChanged?.Invoke(_hunger, _lastHungryState);
        }

        private void OnDestroy()
        {
            if ((_isBusy || _isKnockedOut) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnBeforeSave -= HandleBeforeSave;
                SaveManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleBeforeSave(SaveData data)
        {
            if (data != null)
            {
                data.energy = _energy;
                data.health = Mathf.Max(1f, _health);
                data.hunger = _hunger;
            }
        }

        private void HandleSaveLoaded(SaveData data)
        {
            if (data != null)
            {
                SetEnergy(data.energy);
                SetHealth(data.health <= 0.01f ? 100f : data.health);
                SetHunger(data.hunger <= 0.01f ? 85f : data.hunger);
            }
        }

        private void Update()
        {
            if (_isBusy || _isKnockedOut || OurWorld.UI.MainMenuController.IsMenuActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_hitInvulnTimer > 0f) _hitInvulnTimer -= dt;
            _timeSinceLastDamage += dt;

            // Realistic gradual natural recovery after 14s out of combat when not exhausted or starving
            if (_health < 100f && _health > 0f && _timeSinceLastDamage >= 14f && _energy >= 35f && _hunger >= 20f)
            {
                float prevHp = _health;
                _health = Mathf.Min(100f, _health + 1.35f * dt);
                if (Mathf.FloorToInt(_health) != Mathf.FloorToInt(prevHp) || _health >= 100f)
                {
                    if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                    {
                        SaveManager.Instance.Data.health = _health;
                    }
                    OnHealthChanged?.Invoke(_health, _health - prevHp);
                }
            }

            float realSecPerHour = DayNightCycle.Instance != null ? DayNightCycle.Instance.RealSecondsPerGameHour : 60f;
            float drain = dt / Mathf.Max(5f, realSecPerHour); // 1 energy per in-game hour
            float hungerDrain = (2.4f * dt) / Mathf.Max(5f, realSecPerHour); // ~2.4 hunger per in-game hour

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null && InputHub.Instance != null)
            {
                if (InputHub.Instance.SprintHeld && mode.FootController.CurrentSpeed > 3.1f)
                {
                    drain += sprintExtraDrainPerSecond * dt;
                    hungerDrain += sprintExtraDrainPerSecond * 0.72f * dt;
                }
            }
            if (IsStarving)
            {
                drain *= 1.45f;
            }

            if (hungerDrain > 0f)
            {
                float prevH = _hunger;
                _hunger = Mathf.Clamp(_hunger - hungerDrain, 0f, 100f);
                bool hungryNow = IsHungry;
                if (hungryNow != _lastHungryState || Mathf.Abs(prevH - _hunger) >= 0.35f)
                {
                    if (hungryNow && !_lastHungryState)
                    {
                        bool en = LocalizationService.IsEnglish;
                        bool hasBagFood = PlayerBackpackInventory.Instance != null && PlayerBackpackInventory.Instance.TotalItemCount > 0;
                        OnNeedsMessage?.Invoke(hasBagFood
                            ? (en
                                ? "🍽️ You're getting hungry! Open your Backpack (B) to eat a fruit or snack."
                                : "🍽️ Karnın acıktı! Çantanı (B) açıp meyve ya da atıştırmalık yiyebilirsin.")
                            : (en
                                ? "🍽️ You're getting hungry! Grab a Fruit Bag or snack from Selman Bakkal or Atlas Market."
                                : "🍽️ Karnın acıktı! Selman Bakkal veya 5M Atlas Market'ten Meyve Çantası ya da atıştırmalık alabilirsin."));
                    }
                    _lastHungryState = hungryNow;
                    OnHungerChanged?.Invoke(_hunger, hungryNow);
                }
            }

            if (drain > 0f)
            {
                float prev = _energy;
                _energy = Mathf.Clamp(_energy - drain, 0f, 100f);
                bool tiredNow = IsTired;
                if (tiredNow != _lastTiredState || Mathf.Abs(prev - _energy) >= 0.25f)
                {
                    if (tiredNow && !_lastTiredState)
                    {
                        OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.needs.tired_toast", "Yorgunsun — Kahve iç veya bir yatakta dinlen."));
                    }
                    _lastTiredState = tiredNow;
                    OnEnergyChanged?.Invoke(_energy, tiredNow);
                }
            }
        }

        public void SetEnergy(float value)
        {
            _energy = Mathf.Clamp(value, 0f, 100f);
            _lastTiredState = IsTired;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.energy = _energy;
            }
            OnEnergyChanged?.Invoke(_energy, _lastTiredState);
        }

        public void RestoreEnergy(float amount)
        {
            if (amount <= 0f) return;
            SetEnergy(_energy + amount);
            // Drinking / eating from cafés, vending machines & stalls also satisfies hunger & restores health!
            if (_hunger < 100f)
            {
                SetHunger(_hunger + amount * 0.55f);
            }
            if (_health < 100f && !_isKnockedOut)
            {
                RestoreHealth(amount * 0.50f);
            }
        }

        public void SetHunger(float value)
        {
            _hunger = Mathf.Clamp(value, 0f, 100f);
            _lastHungryState = IsHungry;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.hunger = _hunger;
            }
            OnHungerChanged?.Invoke(_hunger, _lastHungryState);
        }

        public void ConsumeHunger(float amount)
        {
            if (amount <= 0f) return;
            SetHunger(_hunger - amount);
        }

        /// <summary>
        /// Restores hunger (satiety), energy, and health cleanly when eating/drinking from the Backpack!
        /// </summary>
        public void ApplyMealRestore(float hungerAmount, float energyAmount, float healthAmount)
        {
            if (hungerAmount > 0f)
            {
                SetHunger(_hunger + hungerAmount);
            }
            if (energyAmount > 0f)
            {
                SetEnergy(_energy + energyAmount);
            }
            if (healthAmount > 0f && !_isKnockedOut)
            {
                RestoreHealth(healthAmount);
            }
            if (PlayerModeController.Instance != null)
            {
                bool en = LocalizationService.IsEnglish;
                Vector3 popPos = PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 2.25f;
                string badge = hungerAmount > 0f
                    ? (en ? $"+{Mathf.RoundToInt(hungerAmount)} Food" : $"+{Mathf.RoundToInt(hungerAmount)} Tokluk")
                    : $"+{Mathf.RoundToInt(energyAmount)} ⚡";
                VisualEffectsManager.SpawnFloatingText(popPos, badge, new Color(0.98f, 0.78f, 0.24f), scale: 0.96f, duration: 0.95f);
            }
        }

        public void ConsumeEnergy(float amount)
        {
            if (amount <= 0f) return;
            SetEnergy(_energy - amount);
        }

        public void SetHealth(float value, float delta = 0f)
        {
            _health = Mathf.Clamp(value, 0f, 100f);
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.health = Mathf.Max(1f, _health);
            }
            OnHealthChanged?.Invoke(_health, delta);
        }

        public void RestoreHealth(float amount)
        {
            if (amount <= 0f || _isKnockedOut) return;
            float prev = _health;
            _health = Mathf.Clamp(_health + amount, 0f, 100f);
            float actual = _health - prev;
            if (actual > 0.01f)
            {
                if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                {
                    SaveManager.Instance.Data.health = _health;
                }
                OnHealthChanged?.Invoke(_health, actual);
                if (actual >= 5f && PlayerModeController.Instance != null)
                {
                    VisualEffectsManager.SpawnFloatingText(
                        PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 2.1f,
                        $"+{Mathf.RoundToInt(actual)} ♥",
                        new Color(0.28f, 0.96f, 0.56f),
                        scale: 0.92f,
                        duration: 0.85f);
                }
            }
        }

        /// <summary>
        /// Applies realistic physical combat / impact damage to the player with knockback,
        /// recoil animation, crimson hit flash, and clinic recovery knockout at 0 HP.
        /// </summary>
        public void TakeDamage(float amount, Vector3 attackerWorldPos, string attackerName = null, bool heavyHit = false)
        {
            if (amount <= 0f || _isKnockedOut || _isBusy || OurWorld.UI.MainMenuController.IsMenuActive) return;
            if (_hitInvulnTimer > 0f) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked || mode.CurrentMode != ControlMode.OnFoot || MosquePrayerInteractable.IsPlayerPraying) return;

            _hitInvulnTimer = heavyHit ? 0.52f : 0.40f;
            _timeSinceLastDamage = 0f;

            float prev = _health;
            _health = Mathf.Clamp(_health - amount, 0f, 100f);
            float delta = _health - prev;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.health = Mathf.Max(1f, _health);
            }
            OnHealthChanged?.Invoke(_health, delta);

            bool fatalKnockout = _health <= 0.01f;
            PlayerFootController foot = mode.FootController;
            if (foot != null && mode.CurrentMode == ControlMode.OnFoot)
            {
                Vector3 knockDir = foot.transform.position - attackerWorldPos;
                knockDir.y = 0f;
                if (knockDir.sqrMagnitude < 0.01f) knockDir = -foot.transform.forward;
                else knockDir.Normalize();

                foot.ApplyCombatHitImpact(knockDir, heavyHit || fatalKnockout, fatalKnockout);
                VisualEffectsManager.SpawnFloatingText(
                    foot.transform.position + Vector3.up * 2.15f,
                    $"-{Mathf.RoundToInt(amount)} ♥",
                    heavyHit || fatalKnockout ? new Color(1.0f, 0.22f, 0.24f) : new Color(1.0f, 0.48f, 0.32f),
                    scale: heavyHit || fatalKnockout ? 1.22f : 1.0f,
                    duration: 0.95f);
            }

            bool en = LocalizationService.IsEnglish;
            int dmgInt = Mathf.RoundToInt(amount);
            int hpLeft = Mathf.RoundToInt(_health);

            if (fatalKnockout)
            {
                StartCoroutine(KnockoutRecoveryRoutine(attackerName));
                return;
            }

            string who = string.IsNullOrEmpty(attackerName) ? (en ? "Attack" : "Darbe") : attackerName;
            string lowWarn = _health <= 30f
                ? (en ? " · Critical HP! Eat or visit Pharmacy!" : " · Canın kritik! Yemek ye ya da Eczane'ye uğra!")
                : string.Empty;
            string msg = en
                ? $"💥 {who} hit you! (-{dmgInt} HP · {hpLeft}/100){lowWarn}"
                : $"💥 {who} sana vurdu! (-{dmgInt} Can · {hpLeft}/100){lowWarn}";
            OnNeedsMessage?.Invoke(msg);
        }

        /// <summary>
        /// Applies non-combat real-world environmental damage (hot mangal embers, underwater drowning, etc.)
        /// with tailored feedback and clean Clinic knockout handling at 0 HP.
        /// </summary>
        public void TakeEnvironmentalDamage(float amount, Vector3 hazardWorldPos, string msgTr, string msgEn, bool heavy = false)
        {
            if (amount <= 0f || _isKnockedOut || _isBusy || OurWorld.UI.MainMenuController.IsMenuActive) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked || mode.CurrentMode != ControlMode.OnFoot) return;

            _timeSinceLastDamage = 0f;
            float prev = _health;
            _health = Mathf.Clamp(_health - amount, 0f, 100f);
            float delta = _health - prev;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.health = Mathf.Max(1f, _health);
            }
            OnHealthChanged?.Invoke(_health, delta);

            bool fatal = _health <= 0.01f;
            PlayerFootController foot = mode.FootController;
            if (foot != null)
            {
                Vector3 knockDir = foot.transform.position - hazardWorldPos;
                knockDir.y = 0f;
                if (knockDir.sqrMagnitude < 0.01f) knockDir = -foot.transform.forward;
                else knockDir.Normalize();

                if (!foot.IsSwimming)
                {
                    foot.ApplyCombatHitImpact(knockDir, heavy || fatal, fatal);
                }
                else
                {
                    DayNightCycle.Instance?.PulseImpactChromatic(heavy || fatal ? 0.32f : 0.16f);
                }

                VisualEffectsManager.SpawnFloatingText(
                    foot.transform.position + Vector3.up * 2.15f,
                    $"-{Mathf.RoundToInt(amount)} ♥",
                    new Color(1.0f, 0.36f, 0.22f),
                    scale: heavy || fatal ? 1.18f : 0.96f,
                    duration: 0.95f);
            }

            bool en = LocalizationService.IsEnglish;
            if (fatal)
            {
                StartCoroutine(KnockoutRecoveryRoutine(en ? "Accident" : "Kaza"));
                return;
            }

            int dmgInt = Mathf.RoundToInt(amount);
            int hpLeft = Mathf.RoundToInt(_health);
            string baseMsg = en ? msgEn : msgTr;
            OnNeedsMessage?.Invoke($"{baseMsg} (-{dmgInt} {(en ? "HP" : "Can")} · {hpLeft}/100)");
        }

        /// <summary>
        /// Applies realistic driver injury when slamming a vehicle into a wall/obstacle at high speed.
        /// Minor fender-benders are absorbed safely by the seatbelt; severe crashes drain HP and can
        /// send Deniz to Hamidiye Clinic if Health reaches 0.
        /// </summary>
        public void TakeVehicleCrashDamage(float amount, Vector3 impactWorldPos, bool severe)
        {
            if (amount <= 0f || _isKnockedOut || _isBusy || OurWorld.UI.MainMenuController.IsMenuActive) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked || !mode.IsDriving) return;

            _timeSinceLastDamage = 0f;
            float prev = _health;
            _health = Mathf.Clamp(_health - amount, 0f, 100f);
            float delta = _health - prev;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.health = Mathf.Max(1f, _health);
            }
            OnHealthChanged?.Invoke(_health, delta);

            bool fatal = _health <= 0.01f;
            DayNightCycle.Instance?.PulseImpactChromatic(severe || fatal ? 0.36f : 0.20f);
            VisualEffectsManager.SpawnFloatingText(
                mode.ActiveWorldPosition + Vector3.up * 2.35f,
                $"-{Mathf.RoundToInt(amount)} ♥",
                severe || fatal ? new Color(1.0f, 0.22f, 0.22f) : new Color(1.0f, 0.52f, 0.24f),
                scale: severe || fatal ? 1.24f : 1.05f,
                duration: 1.05f);

            bool en = LocalizationService.IsEnglish;
            if (fatal)
            {
                // Safely pull driver out of the wrecked car before Clinic blackout recovery
                if (mode.CurrentVehicle != null)
                {
                    mode.SetOnFootMode(snapCamera: false);
                }
                StartCoroutine(KnockoutRecoveryRoutine(en ? "Severe Car Crash" : "Ağır Trafik Kazası"));
                return;
            }

            int dmgInt = Mathf.RoundToInt(amount);
            int hpLeft = Mathf.RoundToInt(_health);
            OnNeedsMessage?.Invoke(en
                ? (severe
                    ? $"💥 Heavy crash! Airbag & seatbelt saved your life (-{dmgInt} HP · {hpLeft}/100)"
                    : $"💥 Hard impact! Seatbelt held you in place (-{dmgInt} HP · {hpLeft}/100)")
                : (severe
                    ? $"💥 Ağır çarpışma! Hava yastığı ve kemer seni korudu (-{dmgInt} Can · {hpLeft}/100)"
                    : $"💥 Sert çarpışma! Emniyet kemeri korudu ama sarsıldın (-{dmgInt} Can · {hpLeft}/100)"));
        }

        private IEnumerator KnockoutRecoveryRoutine(string attackerName)
        {
            _isKnockedOut = true;
            _isBusy = true;

            bool en = LocalizationService.IsEnglish;
            PlayerModeController mode = PlayerModeController.Instance;
            PlayerFootController foot = mode != null ? mode.FootController : null;

            if (mode != null)
            {
                CarryableProp.DropHeldPropIfAny();
                StreetCatController.CancelActivePet();
                mode.SetModalLock(this, true);
            }
            if (foot != null)
            {
                foot.SetMovementEnabled(false);
            }

            CarryableProp.DropHeldPropIfAny();
            PlayerShoppingCart.ConfiscateBySecurity(mode);
            TownPedestrian.CalmAllAngryCitizens();
            LivingNeighborhoodPopulator.CalmAllAngryNpcs();
            IstanbulDiverseCitizensPopulator.CalmAllHostileCitizens();
            HamidiyeMallInteractionHandler.CalmAllShoppers();
            WorldExpansionAndFarmBuilder.CalmShepherdDog();
            NeighborhoodConsequenceSystem.CoolDownHeatOnRecovery();

            string koMsg = string.IsNullOrEmpty(attackerName)
                ? (en ? "😵 You were knocked out! Taking you to Hamidiye Clinic..." : "😵 Bayıldın! Hamidiye Sağlık Ocağı'na kaldırılıyorsun...")
                : (en ? $"😵 {attackerName} knocked you out! Heading to Hamidiye Clinic..." : $"😵 {attackerName} seni yere serdi! Hamidiye Sağlık Ocağı'na kaldırılıyorsun...");
            OnNeedsMessage?.Invoke(koMsg);

            yield return new WaitForSecondsRealtime(1.55f);

            // Fade to dark screen using existing sleepFadeCanvasGroup
            if (sleepFadeCanvasGroup != null)
            {
                sleepFadeCanvasGroup.gameObject.SetActive(true);
                sleepFadeCanvasGroup.blocksRaycasts = true;
                float t = 0f;
                while (t < 0.75f)
                {
                    t += Time.unscaledDeltaTime;
                    sleepFadeCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.75f));
                    yield return null;
                }
                sleepFadeCanvasGroup.alpha = 1f;
            }

            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.AdvanceHours(2.0f);
            }

            int clinicFee = 0;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                clinicFee = Mathf.Min(45, SaveManager.Instance.Data.money);
                if (clinicFee > 0)
                {
                    SaveManager.Instance.TrySpend(clinicFee);
                }
            }

            SetHealth(100f, 100f);
            if (_hunger < 65f) SetHunger(65f);
            SetEnergy(Mathf.Max(_energy, 65f));
            _timeSinceLastDamage = 99f;

            // Respawn safely on Selman Sokağı outside Home No:48 after treatment
            if (foot != null)
            {
                if (mode != null && mode.CurrentMode != ControlMode.OnFoot)
                {
                    mode.SetOnFootMode(snapCamera: true);
                }
                Vector3 recoverPos = new Vector3(-3.0f, 0.14f, -14.8f);
                foot.TeleportTo(recoverPos, Quaternion.identity);
                foot.Humanoid?.SetLocomotion(0f, true, 0f);
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }

            yield return new WaitForSecondsRealtime(0.45f);

            if (sleepFadeCanvasGroup != null)
            {
                float t = 0f;
                while (t < 0.75f)
                {
                    t += Time.unscaledDeltaTime;
                    sleepFadeCanvasGroup.alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.75f));
                    yield return null;
                }
                sleepFadeCanvasGroup.alpha = 0f;
                sleepFadeCanvasGroup.blocksRaycasts = false;
                sleepFadeCanvasGroup.gameObject.SetActive(false);
            }

            if (foot != null)
            {
                foot.SetMovementEnabled(true);
            }
            if (mode != null)
            {
                mode.SetModalLock(this, false);
                if (mode.CurrentMode == ControlMode.OnFoot)
                {
                    PlayerActionAnimator.Play(mode, null, PlayerActionKind.WakeStretch);
                }
            }

            _isBusy = false;
            _isKnockedOut = false;

            string feeText = clinicFee > 0
                ? (en ? $"-{clinicFee} ₺ medical fee" : $"-{clinicFee} ₺ tedavi ücreti")
                : (en ? "free community clinic care" : "ücretsiz sağlık ocağı tedavisi");
            OnNeedsMessage?.Invoke(en
                ? $"🏥 Treated & recovered! Health: 100% ({feeText} · +2h)"
                : $"🏥 Tedavi edildin! Can: %100 ({feeText} · +2 saat)");
        }

        public void TryBuyVendingDrink()
        {
            if (_isBusy || SaveManager.Instance == null) return;

            const int cost = 5;
            if (!SaveManager.Instance.TrySpend(cost))
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.shop.no_money", "Yetersiz bakiye!"));
                return;
            }

            if (PlayerModeController.Instance != null &&
                PlayerModeController.Instance.FootController != null &&
                PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot)
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.vending.bought", "Soğuk su alındı, içiliyor..."));
                PlayerConsumeAnimator.PlayPurchase(
                    PlayerModeController.Instance.FootController,
                    "vending",
                    ConsumeAnimKind.DrinkWater,
                    "Soğuk Su",
                    8f);
            }
            else
            {
                RestoreEnergy(8f);
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.vending.bought", "Soğuk içecek aldın! (+8 Enerji)"));
            }
        }

        public void TrySleepInBed(Transform bedTransform = null)
        {
            if (_isBusy) return;
            StartCoroutine(SleepRoutine(bedTransform));
        }

        private IEnumerator SleepRoutine(Transform bedTransform)
        {
            _isBusy = true;
            PlayerModeController mode = PlayerModeController.Instance;
            PlayerFootController foot = mode != null ? mode.FootController : null;
            ProceduralHumanoid hum = foot != null ? foot.Humanoid : null;
            Vector3 preSleepPos = foot != null ? foot.transform.position : Vector3.zero;
            Quaternion preSleepRot = foot != null ? foot.transform.rotation : Quaternion.identity;
            bool laidOnBed = false;

            if (mode != null)
            {
                CarryableProp.DropHeldPropIfAny();
                StreetCatController.CancelActivePet();
                mode.SetModalLock(this, true);
            }

            // Physically lie down on the bed before drifting off to sleep!
            if (foot != null && hum != null && bedTransform != null && mode.CurrentMode == ControlMode.OnFoot)
            {
                laidOnBed = true;
                foot.SetMovementEnabled(false);
                Vector3 bedTop = bedTransform.position + Vector3.up * 0.54f;
                foot.TeleportTo(bedTop, bedTransform.rotation);
                hum.SetLocomotion(0f, true, 0f);
                hum.SetLyingDown(true, 1.2f);
                OnNeedsMessage?.Invoke(LocalizationService.IsEnglish
                    ? "Lying down in bed, drifting off to sleep..."
                    : "Yatağına uzandın, uykuya dalıyorsun...");
                yield return new WaitForSecondsRealtime(1.45f);
            }

            if (sleepFadeCanvasGroup != null)
            {
                sleepFadeCanvasGroup.gameObject.SetActive(true);
                sleepFadeCanvasGroup.blocksRaycasts = true;
                float fadeIn = 0.6f;
                float t = 0f;
                while (t < fadeIn)
                {
                    t += Time.unscaledDeltaTime;
                    sleepFadeCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
                    yield return null;
                }
                sleepFadeCanvasGroup.alpha = 1f;
            }

            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.AdvanceHours(8f);
            }

            SetEnergy(100f);
            SetHealth(100f, 100f - _health);
            TownPedestrian.CalmAllAngryCitizens();
            LivingNeighborhoodPopulator.CalmAllAngryNpcs();
            IstanbulDiverseCitizensPopulator.CalmAllHostileCitizens();
            HamidiyeMallInteractionHandler.CalmAllShoppers();
            WorldExpansionAndFarmBuilder.CalmShepherdDog();
            NeighborhoodConsequenceSystem.CoolDownHeatOnRecovery();
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }

            yield return new WaitForSecondsRealtime(0.3f);

            // Fade out while still lying on the bed so the sit-up & rise animation is visible
            if (sleepFadeCanvasGroup != null)
            {
                float fadeOut = 0.6f;
                float t = 0f;
                while (t < fadeOut)
                {
                    t += Time.unscaledDeltaTime;
                    sleepFadeCanvasGroup.alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / fadeOut));
                    yield return null;
                }
                sleepFadeCanvasGroup.alpha = 0f;
                sleepFadeCanvasGroup.blocksRaycasts = false;
                sleepFadeCanvasGroup.gameObject.SetActive(false);
            }

            if (laidOnBed && foot != null)
            {
                if (hum != null) hum.SetLyingDown(false, 1.1f);
                yield return new WaitForSecondsRealtime(1.15f);
                foot.TeleportTo(preSleepPos, preSleepRot);
                foot.SetMovementEnabled(true);
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
                if (PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot)
                {
                    PlayerActionAnimator.Play(PlayerModeController.Instance, null, PlayerActionKind.WakeStretch);
                }
            }

            _isBusy = false;
            if (_hunger > 40f) SetHunger(Mathf.Max(38f, _hunger - 12f));
            OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.needs.slept", "8 saat uyudun — Dinç hissediyorsun!"));
        }

        public void TryRefuelVehicle(SedanVehicleController vehicle)
        {
            if (_isBusy || vehicle == null || SaveManager.Instance == null)
            {
                return;
            }

            if (vehicle.SpeedKmh > 5.5f)
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.fuel.stop_first", "Yakıt almak için aracı tamamen durdur."));
                return;
            }

            if (vehicle.Fuel >= 0.99f)
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.fuel.already_full", "Depo zaten dolu."));
                return;
            }

            int missingPercent = Mathf.CeilToInt((1f - vehicle.Fuel) * 100f);
            int affordablePercent = Mathf.Min(missingPercent, SaveManager.Instance.Data.money / 2);
            if (affordablePercent <= 0)
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.fuel.no_money", "Yakıt için yeterli paran yok! (%1 = 2 ₺)"));
                return;
            }

            int totalCost = affordablePercent * 2;
            if (!SaveManager.Instance.TrySpend(totalCost))
            {
                OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.shop.no_money", "Yetersiz bakiye!"));
                return;
            }

            float targetFuel = Mathf.Clamp01(vehicle.Fuel + affordablePercent * 0.01f);
            StartCoroutine(RefuelRoutine(vehicle, targetFuel, totalCost));
        }

        private IEnumerator RefuelRoutine(SedanVehicleController vehicle, float targetFuel, int paidCost)
        {
            _isBusy = true;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }

            OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.fuel.filling", "Yakıt dolduruluyor..."));

            if (PlayerModeController.Instance != null &&
                PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot &&
                vehicle != null)
            {
                PlayerActionAnimator.Play(PlayerModeController.Instance, vehicle.transform, PlayerActionKind.RefuelCar);
            }

            float startFuel = vehicle != null ? vehicle.Fuel : 0f;
            const float duration = 3.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (vehicle != null)
                {
                    float f = Mathf.Lerp(startFuel, targetFuel, Mathf.Clamp01(elapsed / duration));
                    vehicle.SetFuel(f);
                }
                yield return null;
            }

            if (vehicle != null)
            {
                vehicle.SetFuel(targetFuel);
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            _isBusy = false;
            string doneFmt = LocalizationService.Tr("ui.fuel.done", "Depo dolduruldu! (-{0} ₺)");
            OnNeedsMessage?.Invoke(string.Format(doneFmt, paidCost));
        }
    }
}
