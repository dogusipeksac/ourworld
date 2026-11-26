using System.Collections;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Hinged door controller that smoothly toggles open/closed over 0.35 s,
    /// disables its panel collider while open so players never snag in doorways,
    /// and gently auto-opens when the on-foot player walks right up to it.
    /// </summary>
    public class DoorInteractable : MonoBehaviour
    {
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private float openAngleDegrees = 106f;
        [SerializeField] private float transitionSeconds = 0.35f;
        [SerializeField] private float autoOpenDistance = 1.85f;
        [SerializeField] private float autoCloseDistance = 3.80f;

        private Quaternion _closedLocalRotation = Quaternion.identity;
        private Collider _doorCollider;
        private bool _cachedClosed;
        private bool _isOpen;
        private bool _autoOpened;
        private float _manualCooldown;
        private Coroutine _animRoutine;

        public bool IsOpen => _isOpen;

        public void Configure(Transform pivot, Transform panel, float angleDeg = 106f, float durationSec = 0.35f)
        {
            doorPivot = pivot;
            doorPanel = panel;
            openAngleDegrees = angleDeg;
            transitionSeconds = durationSec;
            if (doorPivot != null)
            {
                _closedLocalRotation = doorPivot.localRotation;
                _cachedClosed = true;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (doorPivot == null)
            {
                Transform found = transform.Find("DoorPivot");
                if (found != null) doorPivot = found;
            }

            if (doorPanel == null && doorPivot != null)
            {
                Transform foundPanel = doorPivot.Find("DoorPanel");
                if (foundPanel != null) doorPanel = foundPanel;
            }

            if (_doorCollider == null && doorPanel != null)
            {
                _doorCollider = doorPanel.GetComponent<Collider>();
            }

            if (doorPivot != null && !_cachedClosed)
            {
                _closedLocalRotation = doorPivot.localRotation;
                _cachedClosed = true;
            }
        }

        private void Update()
        {
            if (_manualCooldown > 0f)
            {
                _manualCooldown -= Time.deltaTime;
                return;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                return;
            }

            Vector3 doorCenter = transform.position;
            Vector3 playerPos = mode.ActiveWorldPosition;
            // Ignore players on a different floor above or below multi-story buildings / Atlas AVM
            if (Mathf.Abs(playerPos.y - doorCenter.y) > 2.3f)
            {
                return;
            }
            float dx = playerPos.x - doorCenter.x;
            float dz = playerPos.z - doorCenter.z;
            float horizDistSq = dx * dx + dz * dz;
            float effectiveAutoOpen = Mathf.Min(autoOpenDistance, 1.38f);

            if (!_isOpen && horizDistSq <= effectiveAutoOpen * effectiveAutoOpen)
            {
                _autoOpened = true;
                EnsureInitialized();
                ProceduralHumanoid hum = mode.FootController != null ? mode.FootController.Humanoid : null;
                Transform handle = FindDoorHandle(playerPos);
                float reachTime = 0f;
                if (hum != null && handle != null)
                {
                    Vector3 toHandle = Vector3.ProjectOnPlane(handle.position - playerPos, Vector3.up);
                    if (toHandle.sqrMagnitude <= 1.25f * 1.25f &&
                        (toHandle.sqrMagnitude < 0.01f || Vector3.Dot(hum.transform.forward, toHandle.normalized) > 0.12f))
                    {
                        reachTime = 0.16f;
                        hum.PlayDoorReach(handle, Mathf.Max(0.05f, transitionSeconds) + reachTime, reachTime);
                    }
                }
                SetOpenState(true, playerPos, reachTime);
            }
            else if (_isOpen && _autoOpened && horizDistSq >= autoCloseDistance * autoCloseDistance)
            {
                _autoOpened = false;
                SetOpenState(false, playerPos);
            }
        }

        private Transform FindDoorHandle(Vector3 interactorWorldPos)
        {
            if (doorPivot == null) return null;
            bool outerSide = Vector3.Dot(doorPivot.right, interactorWorldPos - doorPivot.position) >= 0f;
            string primary = outerSide ? "HandleOuter" : "HandleInner";
            string secondary = outerSide ? "HandleInner" : "HandleOuter";
            Transform handle = doorPivot.Find(primary);
            if (handle == null && doorPanel != null) handle = doorPanel.Find(primary);
            if (handle == null) handle = doorPivot.Find(secondary);
            if (handle == null && doorPanel != null) handle = doorPanel.Find(secondary);
            return handle;
        }

        public void Toggle(Vector3 interactorWorldPos, ProceduralHumanoid interactor = null)
        {
            _autoOpened = false;
            _manualCooldown = 2.0f;
            EnsureInitialized();
            Transform handle = FindDoorHandle(interactorWorldPos);
            bool canReach = interactor != null && handle != null &&
                Vector3.ProjectOnPlane(handle.position - interactorWorldPos, Vector3.up).sqrMagnitude < 1.18f * 1.18f;
            float reachTime = canReach ? 0.20f : 0f;
            if (canReach) interactor.PlayDoorReach(handle, Mathf.Max(0.05f, transitionSeconds) + reachTime, reachTime);
            SetOpenState(!_isOpen, interactorWorldPos, reachTime);
        }

        private void SetOpenState(bool open, Vector3 interactorWorldPos, float reachTime = 0f)
        {
            EnsureInitialized();
            if (doorPivot == null) return;

            Quaternion targetRot;
            if (open)
            {
                _isOpen = true;
                SetDoorColliderEnabled(false);

                Quaternion rotPos = _closedLocalRotation * Quaternion.Euler(0f, openAngleDegrees, 0f);
                Quaternion rotNeg = _closedLocalRotation * Quaternion.Euler(0f, -openAngleDegrees, 0f);

                Vector3 sampleLocal = doorPanel != null ? doorPanel.localPosition : new Vector3(0f, 1.0f, 0.5f);
                Vector3 worldPosA = EvaluateSampleWorldPosition(rotPos, sampleLocal);
                Vector3 worldPosB = EvaluateSampleWorldPosition(rotNeg, sampleLocal);

                float distSqA = (worldPosA - interactorWorldPos).sqrMagnitude;
                float distSqB = (worldPosB - interactorWorldPos).sqrMagnitude;
                targetRot = distSqA >= distSqB ? rotPos : rotNeg;
            }
            else
            {
                _isOpen = false;
                targetRot = _closedLocalRotation;
            }

            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
            }

            OurWorld.Core.GameFeelAudio.PlayDoorLatch();
            _animRoutine = StartCoroutine(AnimateDoorRoutine(targetRot, open, reachTime));
        }

        private void SetDoorColliderEnabled(bool enabledState)
        {
            if (_doorCollider != null)
            {
                _doorCollider.enabled = enabledState;
            }
        }

        private Vector3 EvaluateSampleWorldPosition(Quaternion candidatePivotLocalRot, Vector3 panelLocalPos)
        {
            Vector3 inParentSpace = doorPivot.localPosition + (candidatePivotLocalRot * panelLocalPos);
            return doorPivot.parent != null
                ? doorPivot.parent.TransformPoint(inParentSpace)
                : inParentSpace;
        }

        private IEnumerator AnimateDoorRoutine(Quaternion targetLocalRot, bool opening, float reachTime)
        {
            Quaternion startRot = doorPivot.localRotation;
            float duration = Mathf.Max(0.05f, transitionSeconds);
            float elapsed = -reachTime;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                doorPivot.localRotation = Quaternion.Slerp(startRot, targetLocalRot, t);
                yield return null;
            }

            doorPivot.localRotation = targetLocalRot;
            if (!opening)
            {
                SetDoorColliderEnabled(true);
            }
            _animRoutine = null;
        }
    }
}
