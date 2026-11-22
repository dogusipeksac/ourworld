using System.Collections;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridroam.World
{
    public enum MosqueInteractionKind
    {
        SadirvanAbdest,
        PrayerSaf
    }

    /// <summary>
    /// Interactive mosque experience for Hz. Osman Camii (Sultanbeyli · Hamidiye):
    /// - SadirvanAbdest: take ablution at the octagonal marble courtyard fountain (+15 Energy).
    /// - PrayerSaf: align on the emerald saf carpet facing the Mihrab (Kıble) and perform an
    ///   animated Salah (Namaz) sequence (Kıyam -> Rükû -> Secde -> Oturuş -> Secde -> Tahiyyat & Selam),
    ///   restoring full Energy (100%) on completion. Pressing E or Esc cancels cleanly at any time.
    /// </summary>
    public class MosquePrayerInteractable : MonoBehaviour
    {
        [SerializeField] private MosqueInteractionKind interactionKind = MosqueInteractionKind.PrayerSaf;
        [SerializeField] private Vector3 prayerStandLocalOffset = Vector3.zero;
        [SerializeField] private float prayerFacingYawOffset;

        private static MosquePrayerInteractable _activePrayer;
        private static bool _hasAbdest;

        private Coroutine _prayerRoutine;
        private PlayerModeController _prayingMode;
        private float _lastAbdestTime = -99f;

        public static bool IsPlayerPraying => _activePrayer != null;
        public static bool HasAbdest => _hasAbdest;
        public MosqueInteractionKind Kind => interactionKind;

        public void Configure(MosqueInteractionKind kind, Vector3 standLocalOffset = default, float facingYawOffset = 0f)
        {
            interactionKind = kind;
            prayerStandLocalOffset = standLocalOffset;
            prayerFacingYawOffset = facingYawOffset;
        }

        public static void CancelActivePrayerIfAny()
        {
            if (_activePrayer != null)
            {
                _activePrayer.CancelPrayer(showMessage: true);
            }
        }

        public void Interact(PlayerModeController mode)
        {
            if (mode == null) return;

            if (interactionKind == MosqueInteractionKind.SadirvanAbdest)
            {
                PerformAbdest(mode);
                return;
            }

            if (_activePrayer != null)
            {
                _activePrayer.CancelPrayer(showMessage: true);
                return;
            }

            StartPrayer(mode);
        }

        private void PerformAbdest(PlayerModeController mode)
        {
            if (Time.time - _lastAbdestTime < 8f && _hasAbdest)
            {
                mode.RaiseStatusMessage("Abdestin zaten taze. Camiye girip safa durabilirsin.");
                return;
            }

            _lastAbdestTime = Time.time;
            _hasAbdest = true;
            GameFeelAudio.PlayWaterSplash();
            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.1f, new Color(0.42f, 0.84f, 1.0f), count: 10, spread: 0.55f);
            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(15f);
            }
            mode.RaiseStatusMessage("Şadırvanda abdest aldın, gönlün ferahladı! (+15 Enerji)");
        }

        private void StartPrayer(PlayerModeController mode)
        {
            PlayerFootController foot = mode.FootController;
            if (foot == null) return;

            _activePrayer = this;
            _prayingMode = mode;
            _prayerRoutine = StartCoroutine(PrayerSequenceRoutine(mode, foot));
        }

        private void Update()
        {
            if (_activePrayer != this) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPrayer(showMessage: true);
            }
        }

        private void OnDisable()
        {
            if (_activePrayer == this)
            {
                CancelPrayer(showMessage: false);
            }
        }

        private void CancelPrayer(bool showMessage)
        {
            if (_prayerRoutine != null)
            {
                StopCoroutine(_prayerRoutine);
                _prayerRoutine = null;
            }

            if (_prayingMode != null)
            {
                PlayerFootController foot = _prayingMode.FootController;
                if (foot != null)
                {
                    if (foot.Humanoid != null)
                    {
                        foot.Humanoid.SetPrayerPose(0, 0f);
                    }
                    foot.SetMovementEnabled(true);
                }

                if (showMessage)
                {
                    _prayingMode.RaiseStatusMessage("Namazdan kalktın.");
                }
            }

            _prayingMode = null;
            if (_activePrayer == this)
            {
                _activePrayer = null;
            }
        }

        private IEnumerator PrayerSequenceRoutine(PlayerModeController mode, PlayerFootController foot)
        {
            ProceduralHumanoid humanoid = foot.Humanoid;

            // Align cleanly onto the emerald saf carpet facing the Mihrab (Kıble)
            Vector3 standWorld = transform.TransformPoint(prayerStandLocalOffset);
            Quaternion kibleRot = transform.rotation * Quaternion.Euler(0f, prayerFacingYawOffset, 0f);
            foot.TeleportTo(standWorld, kibleRot);
            foot.SetMovementEnabled(false);

            if (humanoid != null)
            {
                humanoid.SetLookAtPoint(false, Vector3.zero);
                humanoid.SetLocomotion(0f, true, 0f);
            }

            // 1. Kıyam (Standing with hands folded)
            if (humanoid != null) humanoid.SetPrayerPose(1, 0f);
            GameFeelAudio.PlayPrayerChime();
            string kiyamMsg = _hasAbdest
                ? "Allâhu Ekber · Kıyam (Ayakta kıraat)... (İptal: E / Esc)"
                : "Allâhu Ekber · Kıyam (İstersen avludaki şadırvandan abdest de alabilirsin)";
            mode.RaiseStatusMessage(kiyamMsg);
            yield return new WaitForSeconds(2.4f);

            // 2. Rükû (Bowing with hands on knees)
            if (humanoid != null) humanoid.SetPrayerPose(2, 0f);
            mode.RaiseStatusMessage("Rükû · Sübhâne Rabbiye'l-azîm...");
            yield return new WaitForSeconds(2.0f);

            // 3. Kavme (Brief upright rise from Rükû)
            if (humanoid != null) humanoid.SetPrayerPose(1, 0f);
            mode.RaiseStatusMessage("Semiallâhu limen hamideh · Rabbenâ leke'l-hamd...");
            yield return new WaitForSeconds(1.1f);

            // 4. First Secde (Prostration on carpet)
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage("Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.1f);

            // 5. Celse (Brief calm sit between secdes)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            yield return new WaitForSeconds(1.0f);

            // 6. Second Secde
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage("İkinci Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.0f);

            // 7. Ka'de / Tahiyyat (Seated prayer)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            mode.RaiseStatusMessage("Tahiyyat · Ettehiyyâtü lillâhi ve's-salevâtü ve't-tayyibât...");
            yield return new WaitForSeconds(1.8f);

            // 8. Selam to the right, then to the left
            if (humanoid != null) humanoid.SetPrayerPose(4, 54f);
            mode.RaiseStatusMessage("Esselâmü aleyküm ve rahmetullah (Sağa selam)...");
            yield return new WaitForSeconds(1.1f);

            if (humanoid != null) humanoid.SetPrayerPose(4, -54f);
            mode.RaiseStatusMessage("Esselâmü aleyküm ve rahmetullah (Sola selam)...");
            yield return new WaitForSeconds(1.1f);

            // Complete cleanly & restore full Energy
            if (humanoid != null) humanoid.SetPrayerPose(0, 0f);
            foot.SetMovementEnabled(true);
            GameFeelAudio.PlayMilestoneFanfare();
            GameFeelAudio.SpawnBurst(foot.transform.position + Vector3.up * 1.35f, new Color(0.95f, 0.82f, 0.34f), count: 14, spread: 0.65f);

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(100f);
            }

            mode.RaiseStatusMessage("Allah kabul etsin! Gönlün huzurla doldu. (%100 Enerji)");

            _prayerRoutine = null;
            _prayingMode = null;
            if (_activePrayer == this)
            {
                _activePrayer = null;
            }
        }
    }
}
