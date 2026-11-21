using System.Collections;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
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
            float dx = playerPos.x - doorCenter.x;
            float dz = playerPos.z - doorCenter.z;
            float horizDistSq = dx * dx + dz * dz;

            if (!_isOpen && horizDistSq <= autoOpenDistance * autoOpenDistance)
            {
                _autoOpened = true;
                SetOpenState(true, playerPos);
            }
            else if (_isOpen && _autoOpened && horizDistSq >= autoCloseDistance * autoCloseDistance)
            {
                _autoOpened = false;
                SetOpenState(false, playerPos);
            }
        }

        public void Toggle(Vector3 interactorWorldPos)
        {
            _autoOpened = false;
            _manualCooldown = 2.0f;
            SetOpenState(!_isOpen, interactorWorldPos);
        }

        private void SetOpenState(bool open, Vector3 interactorWorldPos)
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

            _animRoutine = StartCoroutine(AnimateDoorRoutine(targetRot, open));
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

        private IEnumerator AnimateDoorRoutine(Quaternion targetLocalRot, bool opening)
        {
            Quaternion startRot = doorPivot.localRotation;
            float duration = Mathf.Max(0.05f, transitionSeconds);
            float elapsed = 0f;

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
