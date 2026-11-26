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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _activePrayer = null;
            _hasAbdest = false;
        }

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
            bool en = LocalizationService.IsEnglish;

            if (Time.time - _lastAbdestTime < 8f && _hasAbdest)
            {
                mode.RaiseStatusMessage(en
                    ? "Your ablution is already fresh. You may enter the mosque to pray."
                    : "Abdestin zaten taze. Camiye girip safa durabilirsin.");
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
                mode.RaiseStatusMessage(en
                    ? "Took ablution at the courtyard fountain, feeling refreshed! (+15 Energy)"
                    : "Şadırvanda abdest aldın, gönlün ferahladı! (+15 Enerji)");
            }, startStatus: en ? "Taking ablution at the fountain..." : "Şadırvanda abdest alınıyor (eller, yüz, kollar)...");
        }

        private void StartPrayer(PlayerModeController mode)
        {
            PlayerFootController foot = mode.FootController;
            if (foot == null) return;

            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();
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
                    _prayingMode.RaiseStatusMessage(LocalizationService.IsEnglish ? "Stepped out of prayer." : "Namazdan kalktın.");
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

            // Smoothly step onto the emerald saf carpet and turn toward the Mihrab (Kıble) instead of popping
            Vector3 standWorld = transform.TransformPoint(prayerStandLocalOffset);
            Quaternion kibleRot = transform.rotation * Quaternion.Euler(0f, prayerFacingYawOffset, 0f);
            foot.SetMovementEnabled(false);
            if (humanoid != null) humanoid.SetLookAtPoint(false, Vector3.zero);

            Vector3 startDelta = standWorld - foot.transform.position;
            startDelta.y = 0f;
            CharacterController cc = foot.GetComponent<CharacterController>();
            if (startDelta.magnitude > 0.14f && startDelta.magnitude <= 4.5f)
            {
                float walkElapsed = 0f;
                const float maxWalkTime = 2.0f;
                while (walkElapsed < maxWalkTime)
                {
                    float dt = Time.deltaTime;
                    walkElapsed += dt;
                    Vector3 toSpot = standWorld - foot.transform.position;
                    toSpot.y = 0f;
                    float dist = toSpot.magnitude;
                    if (dist <= 0.12f) break;
                    Vector3 dir = toSpot / dist;
                    float spd = Mathf.Lerp(0.85f, 1.85f, Mathf.Clamp01(dist / 0.9f));
                    Vector3 step = dir * Mathf.Min(dist, spd * dt);
                    if (cc != null && cc.enabled) cc.Move(step + Vector3.down * (2f * dt));
                    else foot.transform.position += step;
                    foot.transform.rotation = Quaternion.Slerp(foot.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-9f * dt));
                    if (humanoid != null) humanoid.SetLocomotion(spd, true, 0f, dir);
                    yield return null;
                }
            }

            if (humanoid != null) humanoid.SetLocomotion(0f, true, 0f);
            float turnElapsed = 0f;
            while (turnElapsed < 0.45f && Quaternion.Angle(foot.transform.rotation, kibleRot) > 2.5f)
            {
                float dt = Time.deltaTime;
                turnElapsed += dt;
                foot.transform.rotation = Quaternion.Slerp(foot.transform.rotation, kibleRot, 1f - Mathf.Exp(-10f * dt));
                yield return null;
            }
            foot.TeleportTo(standWorld, kibleRot);

            if (humanoid != null)
            {
                humanoid.SetLookAtPoint(false, Vector3.zero);
                humanoid.SetLocomotion(0f, true, 0f);
            }

            bool en = LocalizationService.IsEnglish;

            // 1. İftitah Tekbiri (Raising hands respectfully beside ears)
            if (humanoid != null) humanoid.SetPrayerPose(5, 0f);
            GameFeelAudio.PlayPrayerChime();
            string tekbirMsg = _hasAbdest
                ? (en ? "Allahu Akbar · Opening Takbir... (Cancel: E / Esc)" : "Allâhu Ekber · İftitah Tekbiri... (İptal: E / Esc)")
                : (en ? "Allahu Akbar · Opening Takbir (You can also take ablution at the courtyard fountain)" : "Allâhu Ekber · İftitah Tekbiri (İstersen avludaki şadırvandan abdest alabilirsin)");
            mode.RaiseStatusMessage(tekbirMsg);
            yield return new WaitForSeconds(1.25f);

            // 2. Kıyam (Standing upright with right hand folded over left over abdomen)
            if (humanoid != null) humanoid.SetPrayerPose(1, 0f);
            mode.RaiseStatusMessage(en ? "Qiyam · Reciting Al-Fatiha..." : "Kıyam · Sübhâneke ve Fâtiha okunuyor...");
            yield return new WaitForSeconds(2.3f);

            // 3. Rükû (Crisp 90° flat back bow, both palms grasping knees)
            if (humanoid != null) humanoid.SetPrayerPose(2, 0f);
            mode.RaiseStatusMessage(en ? "Ruku · Bowing in reverence..." : "Rükû · Sübhâne Rabbiye'l-azîm...");
            yield return new WaitForSeconds(2.1f);

            // 4. Kavme (Upright rise from Rükû with arms relaxed cleanly at sides)
            if (humanoid != null) humanoid.SetPrayerPose(6, 0f);
            mode.RaiseStatusMessage(en ? "Qawmah · Rising upright..." : "Semiallâhu limen hamideh · Rabbenâ leke'l-hamd...");
            yield return new WaitForSeconds(1.2f);

            // 4b. Smooth knee-first descent before prostration (prevents stiff dive straight from standing to carpet)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            yield return new WaitForSeconds(0.42f);

            // 5. First Secde (7-point prostration: forehead, palms, knees & toes on carpet)
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage(en ? "Sujud · Prostration..." : "Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.2f);

            // 6. Celse (Calm kneeling sit back on heels between secdes)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            mode.RaiseStatusMessage(en ? "Jalsa · Brief pause seated..." : "Celse · İki secde arası oturuş...");
            yield return new WaitForSeconds(1.1f);

            // 7. Second Secde
            if (humanoid != null) humanoid.SetPrayerPose(3, 0f);
            mode.RaiseStatusMessage(en ? "Second Sujud · Prostration..." : "İkinci Secde · Sübhâne Rabbiye'l-a'lâ...");
            yield return new WaitForSeconds(2.2f);

            // 8. Ka'de / Tahiyyat (Seated prayer, palms on thighs)
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            mode.RaiseStatusMessage(en ? "Tashahhud · Seated supplication..." : "Tahiyyat · Ettehiyyâtü lillâhi ve's-salevâtü ve't-tayyibât...");
            yield return new WaitForSeconds(2.0f);

            // 9. Selam to the right, then to the left
            if (humanoid != null) humanoid.SetPrayerPose(4, 64f);
            mode.RaiseStatusMessage(en ? "Assalamu Alaikum wa Rahmatullah (Right)..." : "Esselâmü aleyküm ve rahmetullah (Sağa selam)...");
            yield return new WaitForSeconds(1.2f);

            if (humanoid != null) humanoid.SetPrayerPose(4, -64f);
            mode.RaiseStatusMessage(en ? "Assalamu Alaikum wa Rahmatullah (Left)..." : "Esselâmü aleyküm ve rahmetullah (Sola selam)...");
            yield return new WaitForSeconds(1.2f);

            // Return head forward and stand up calmly before giving movement back
            if (humanoid != null) humanoid.SetPrayerPose(4, 0f);
            yield return new WaitForSeconds(0.28f);
            if (humanoid != null) humanoid.SetPrayerPose(6, 0f);
            yield return new WaitForSeconds(0.48f);

            // Complete cleanly & restore full Energy
            if (humanoid != null) humanoid.SetPrayerPose(0, 0f);
            foot.SetMovementEnabled(true);
            GameFeelAudio.PlayMilestoneFanfare();
            GameFeelAudio.SpawnBurst(foot.transform.position + Vector3.up * 1.35f, new Color(0.95f, 0.82f, 0.34f), count: 14, spread: 0.65f);

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(100f);
            }

            mode.RaiseStatusMessage(en ? "May your prayer be accepted! Filled with peace. (100% Energy)" : "Allah kabul etsin! Gönlün huzurla doldu. (%100 Enerji)");

            _prayerRoutine = null;
            _prayingMode = null;
            if (_activePrayer == this)
            {
                _activePrayer = null;
            }
        }
    }
}
