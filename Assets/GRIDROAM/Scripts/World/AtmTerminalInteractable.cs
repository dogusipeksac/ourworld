using Gridroam.Core;
using Gridroam.Player;
using Gridroam.Vehicle;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Interactive Sultanbeyli neighborhood ATM kiosk:
    /// - Claims persistent neighborhood income (+75 ₺ every 90 s of play time, saved in SaveData).
    /// - Paid services require explicit two-step confirmation (press E to preview vehicle & cost,
    ///   press E again within 6 s to confirm) so a player never spends money accidentally:
    ///   (1) Sokak Kedilerine Mama Bağışı (25 ₺ -> +35 Enerji)
    ///   (2) En Yakın / Son Kullanılan Araç Bakım & Yakıt Çeki (45 ₺ -> %100 tamir & depo)
    /// </summary>
    public class AtmTerminalInteractable : MonoBehaviour
    {
        private const float IncomeCooldownSeconds = 90f;
        private const float ConfirmWindowSeconds = 6f;

        [SerializeField] private string bankName = "Sultanbeyli Mahalle ATM";

        private int _pendingService = -1; // 0 = cat food, 1 = vehicle repair+fuel
        private float _pendingConfirmExpiresAt;
        private SedanVehicleController _pendingTargetCar;
        private int _nextServicePreview;

        public void Configure(string displayName)
        {
            bankName = displayName;
        }

        public void Use(PlayerModeController mode)
        {
            if (mode == null || SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
            if (PlayerActionAnimator.IsBusyOn(mode)) return;

            SaveData data = SaveManager.Instance.Data;
            float elapsedSinceClaim = data.totalPlayTimeSeconds - data.lastAtmClaimPlayTimeSeconds;

            // 1. Persistent periodic neighborhood income (+75 ₺ every 90 s of saved play time)
            if (data.lastAtmClaimPlayTimeSeconds < 0f || elapsedSinceClaim >= IncomeCooldownSeconds)
            {
                _pendingService = -1;
                data.lastAtmClaimPlayTimeSeconds = data.totalPlayTimeSeconds;
                PlayerActionAnimator.Play(mode, transform, PlayerActionKind.AtmKeypad, onClimax: () =>
                {
                    if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
                    SaveManager.Instance.AddMoney(75);
                    GameFeelAudio.PlayCoinChime();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.4f, new Color(0.28f, 0.92f, 0.56f), count: 12, spread: 0.5f);
                    mode.RaiseStatusMessage($"{bankName}: Günlük mahalle geliri çekildi! (+75 ₺ · Bakiye: {SaveManager.Instance.Data.money} ₺)");
                }, startStatus: $"{bankName}: Şifre giriliyor, nakit hazırlanıyor...");
                return;
            }

            // 2. If user is confirming a previously previewed paid service within the confirmation window
            if (_pendingService >= 0 && Time.time <= _pendingConfirmExpiresAt)
            {
                int confirmed = _pendingService;
                SedanVehicleController targetCar = _pendingTargetCar;
                _pendingService = -1;
                _pendingTargetCar = null;
                PlayerActionAnimator.Play(mode, transform, PlayerActionKind.AtmKeypad, onClimax: () =>
                {
                    if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
                    ExecutePaidService(mode, SaveManager.Instance.Data, confirmed, targetCar);
                }, startStatus: $"{bankName}: İşlem onaylanıyor...");
                return;
            }

            // 3. First press during cooldown: preview service, exact cost, and exact target vehicle (no money deducted yet!)
            int remain = Mathf.CeilToInt(IncomeCooldownSeconds - elapsedSinceClaim);
            _pendingService = _nextServicePreview;
            _nextServicePreview = (_nextServicePreview + 1) % 2;
            _pendingConfirmExpiresAt = Time.time + ConfirmWindowSeconds;
            GameFeelAudio.PlayUiClick();

            if (_pendingService == 0)
            {
                mode.RaiseStatusMessage($"{bankName} (Gelir: {remain} sn) · Kedilere Mama Bağışı (25 ₺ · +35 Enerji) — Onay için tekrar E bas.");
            }
            else
            {
                _pendingTargetCar = ResolveServiceVehicle(mode);
                string carLabel = _pendingTargetCar != null ? _pendingTargetCar.gameObject.name : "Araç bulunamadı";
                mode.RaiseStatusMessage($"{bankName} (Gelir: {remain} sn) · '{carLabel}' Tamir+Yakıt (45 ₺) — Onay için tekrar E bas.");
            }
        }

        private void ExecutePaidService(PlayerModeController mode, SaveData data, int serviceIdx, SedanVehicleController targetCar)
        {
            if (serviceIdx == 0)
            {
                if (!SaveManager.Instance.TrySpend(25))
                {
                    mode.RaiseStatusMessage($"{bankName}: Yetersiz bakiye (25 ₺ gerekli · Bakiye: {data.money} ₺).");
                    return;
                }
                if (PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(35f);
                }
                GameFeelAudio.PlayCatMeow();
                GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.35f, new Color(1.0f, 0.78f, 0.30f), count: 12, spread: 0.5f);
                mode.RaiseStatusMessage($"{bankName}: Selman Sokağı kedilerine mama bağışı onaylandı! (-25 ₺ · +35 Enerji)");
                return;
            }

            if (targetCar == null)
            {
                targetCar = ResolveServiceVehicle(mode);
            }
            if (targetCar == null)
            {
                mode.RaiseStatusMessage($"{bankName}: Bakım uygulanacak uygun araç bulunamadı (ücret alınmadı).");
                return;
            }
            if (!SaveManager.Instance.TrySpend(45))
            {
                mode.RaiseStatusMessage($"{bankName}: Yetersiz bakiye (45 ₺ gerekli · Bakiye: {data.money} ₺).");
                return;
            }

            targetCar.SetFuel(1f);
            VehicleDamageSystem dmg = targetCar.GetComponent<VehicleDamageSystem>();
            if (dmg != null) dmg.RepairFull();
            GameFeelAudio.PlayMilestoneFanfare();
            GameFeelAudio.SpawnBurst(targetCar.transform.position + Vector3.up * 1.2f, new Color(0.32f, 0.88f, 1.0f), count: 12, spread: 0.7f);
            mode.RaiseStatusMessage($"{bankName}: '{targetCar.gameObject.name}' tamamen onarıldı & yakıt %100! (-45 ₺)");
        }

        private SedanVehicleController ResolveServiceVehicle(PlayerModeController mode)
        {
            if (mode.CurrentVehicle != null) return mode.CurrentVehicle;

            SedanVehicleController[] allCars = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Exclude);
            SedanVehicleController nearest = null;
            float bestDistSq = 28f * 28f;
            Vector3 p = transform.position;
            for (int i = 0; i < allCars.Length; i++)
            {
                SedanVehicleController c = allCars[i];
                if (c == null) continue;
                float dSq = (c.transform.position - p).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = c;
                }
            }

            return nearest != null ? nearest : mode.PlayerSedan;
        }
    }
}
