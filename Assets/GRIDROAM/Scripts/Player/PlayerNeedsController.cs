using System;
using System.Collections;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Vehicle;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Player
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
        [SerializeField] private float sprintExtraDrainPerSecond = 0.032f;
        [SerializeField] private CanvasGroup sleepFadeCanvasGroup;

        private float _energy = 80f;
        private bool _lastTiredState;
        private bool _isBusy;

        public float Energy => _energy;
        public bool IsTired => _energy < 20f;
        public bool IsBusy => _isBusy;

        public event Action<float, bool> OnEnergyChanged;
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
        }

        private void Start()
        {
            if (SaveManager.Instance != null)
            {
                _energy = Mathf.Clamp(SaveManager.Instance.Data.energy, 0f, 100f);
                SaveManager.Instance.OnBeforeSave += HandleBeforeSave;
                SaveManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            }

            _lastTiredState = IsTired;
            OnEnergyChanged?.Invoke(_energy, _lastTiredState);
        }

        private void OnDestroy()
        {
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
            }
        }

        private void HandleSaveLoaded(SaveData data)
        {
            if (data != null)
            {
                SetEnergy(data.energy);
            }
        }

        private void Update()
        {
            if (_isBusy) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float realSecPerHour = DayNightCycle.Instance != null ? DayNightCycle.Instance.RealSecondsPerGameHour : 60f;
            float drain = dt / Mathf.Max(5f, realSecPerHour); // 1 energy per in-game hour

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null && InputHub.Instance != null)
            {
                if (InputHub.Instance.SprintHeld && mode.FootController.CurrentSpeed > 4.2f)
                {
                    drain += sprintExtraDrainPerSecond * dt;
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

            RestoreEnergy(8f);
            OnNeedsMessage?.Invoke(LocalizationService.Tr("ui.vending.bought", "Soğuk içecek aldın! (+8 Enerji)"));
        }

        public void TrySleepInBed()
        {
            if (_isBusy) return;
            StartCoroutine(SleepRoutine());
        }

        private IEnumerator SleepRoutine()
        {
            _isBusy = true;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
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
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }

            yield return new WaitForSecondsRealtime(0.3f);

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

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            _isBusy = false;
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
