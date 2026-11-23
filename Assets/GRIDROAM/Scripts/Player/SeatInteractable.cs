using UnityEngine;

namespace OurWorld.Player
{
    /// <summary>
    /// Represents a single sittable slot on a sofa, armchair, dining chair, or outdoor bench.
    /// SitPoint.position sits on the cushion/seat surface and SitPoint.forward points in the facing direction.
    /// </summary>
    public class SeatInteractable : MonoBehaviour
    {
        [SerializeField] private Transform sitPoint;
        [SerializeField] private InteractableTrigger interactableTrigger;
        [SerializeField] private bool isOccupied;

        public Transform SitPoint => sitPoint != null ? sitPoint : transform;
        public bool IsOccupied => isOccupied;

        public void Configure(Transform point, InteractableTrigger trigger = null, bool occupied = false)
        {
            sitPoint = point != null ? point : transform;
            interactableTrigger = trigger != null ? trigger : GetComponent<InteractableTrigger>();
            SetOccupied(occupied);
        }

        private void Awake()
        {
            if (sitPoint == null) sitPoint = transform;
            if (interactableTrigger == null) interactableTrigger = GetComponent<InteractableTrigger>();
            if (interactableTrigger != null)
            {
                interactableTrigger.enabled = !isOccupied;
            }
        }

        public void SetOccupied(bool occupied)
        {
            isOccupied = occupied;
            if (interactableTrigger == null)
            {
                interactableTrigger = GetComponent<InteractableTrigger>();
            }
            if (interactableTrigger != null)
            {
                interactableTrigger.enabled = !occupied;
            }
        }
    }
}
