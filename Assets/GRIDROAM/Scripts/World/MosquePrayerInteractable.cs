using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OurWorld.World
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
            if (PlayerActionAnimator.IsBusyOn(mode)) return;

            if (Time.time - _lastAbdestTime < 8f && _hasAbdest)
            {
                mode.RaiseStatusMessage("Abdestin zaten taze. Camiye girip safa durabilirsin.");
                return;
            }

            _lastAbdestTime = Time.time;
            PlayerActionAnimator.Play(mode, transform, PlayerActionKind.Abdest, onClimax: () =>
            {
                _hasAbdest = true;
                if (PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(15f);
                }
                mode.RaiseStatusMessage("Şadırvanda abdest aldın, gönlün ferahladı! (+15 Enerji)");
            }, startStatus: "Şadırvanda abdest alınıyor (eller, yüz, kollar)...");
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

            // 1. İftitah Tekbiri (Raising hands respectfully beside ears)
            if (humanoid != null) humanoid.SetPrayerPose(5, 0f);
            GameFeelAudio.PlayPrayerChime();
            string tekbirMsg = _hasAbdest
                ? "Allâhu Ekber · İftitah Tekbiri... (İptal: E / Esc)"
                : "Allâhu Ekber · İftitah Tekbiri (İstersen avludaki şadırvandan abdest alabilirsin)";
            mode.RaiseStatusMessage(tekbirMsg);
            yield return new WaitForSeconds(1.25f);

            // 2. Kıyam (Standing upright with right hand folded over left over abdomen)
            if (humanoid != null) humanoid.SetPrayerPose(1, 0f);
            mode.RaiseStatusMessage("Kıyam · Sübhâneke ve Fâtiha okunuyor...");
            yield return new WaitForSeconds(2.3f);

            // 3. Rükû (Crisp 90° flat back bow, both palms grasping knees)
            if (humanoid != null) humanoid.SetPrayerPose(2, 0f);
            mode.RaiseStatusMessage("Rükû · Sübhâne Rabbiye'l-azîm...");
            yield return new WaitForSeconds(2.1f);

            // 4. Kavme (Upright rise from Rükû with arms relaxed cleanly at sides)
            if (humanoid != null) humanoid.SetPrayerPose(6, 0f);
            mode.RaiseStatusMessage("Semiallâhu limen hamideh · Rabbenâ leke'l-hamd...");
            yield return new WaitForSeconds(1.2f);

            // 5. First Secde (7-point prostration: forehead, palms, knees & toes on carpet)
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage("Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.2f);

            // 6. Celse (Calm kneeling sit back on heels between secdes)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            mode.RaiseStatusMessage("Celse · İki secde arası oturuş...");
            yield return new WaitForSeconds(1.1f);

            // 7. Second Secde
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage("İkinci Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.2f);

            // 8. Ka'de / Tahiyyat (Seated prayer, palms on thighs)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            mode.RaiseStatusMessage("Tahiyyat · Ettehiyyâtü lillâhi ve's-salevâtü ve't-tayyibât...");
            yield return new WaitForSeconds(2.0f);

            // 9. Selam to the right, then to the left
            if (humanoid != null) humanoid.SetPrayerPose(4, 64f);
            mode.RaiseStatusMessage("Esselâmü aleyküm ve rahmetullah (Sağa selam)...");
            yield return new WaitForSeconds(1.2f);

            if (humanoid != null) humanoid.SetPrayerPose(4, -64f);
            mode.RaiseStatusMessage("Esselâmü aleyküm ve rahmetullah (Sola selam)...");
            yield return new WaitForSeconds(1.2f);

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
