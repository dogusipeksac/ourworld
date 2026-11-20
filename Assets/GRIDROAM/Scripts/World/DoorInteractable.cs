using System.Collections;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Hinged door controller that smoothly toggles open/closed over 0.4 s (0 <-> 95 degrees),
    /// swinging away from whichever side the player is standing on.
    /// </summary>
    public class DoorInteractable : MonoBehaviour
    {
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private float openAngleDegrees = 95f;
        [SerializeField] private float transitionSeconds = 0.4f;

        private Quaternion _closedLocalRotation = Quaternion.identity;
        private bool _cachedClosed;
        private bool _isOpen;
        private Coroutine _animRoutine;

        public bool IsOpen => _isOpen;

        public void Configure(Transform pivot, Transform panel, float angleDeg = 95f, float durationSec = 0.4f)
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

            if (doorPivot != null && !_cachedClosed)
            {
                _closedLocalRotation = doorPivot.localRotation;
                _cachedClosed = true;
            }
        }

        public void Toggle(Vector3 interactorWorldPos)
        {
            EnsureInitialized();
            if (doorPivot == null) return;

            Quaternion targetRot;
            if (!_isOpen)
            {
                _isOpen = true;
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

            _animRoutine = StartCoroutine(AnimateDoorRoutine(targetRot));
        }

        private Vector3 EvaluateSampleWorldPosition(Quaternion candidatePivotLocalRot, Vector3 panelLocalPos)
        {
            Vector3 inParentSpace = doorPivot.localPosition + (candidatePivotLocalRot * panelLocalPos);
            return doorPivot.parent != null
                ? doorPivot.parent.TransformPoint(inParentSpace)
                : inParentSpace;
        }

        private IEnumerator AnimateDoorRoutine(Quaternion targetLocalRot)
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
            _animRoutine = null;
        }
    }
}
