using UnityEngine;

namespace Gridroam.Jobs
{
    public enum JobType
    {
        None = 0,
        PizzaDelivery = 1,
        Taxi = 2,
        StreetCleanup = 3,
        HousePainting = 4
    }

    /// <summary>
    /// Attached to each open-world side-job start point alongside an InteractableTrigger (kind JobBoard)
    /// and a glowing ground ring visual.
    /// </summary>
    public class JobBoardPoint : MonoBehaviour
    {
        [SerializeField] private JobType jobType = JobType.PizzaDelivery;
        [SerializeField] private string jobNameKey = "job.pizza.name";
        [SerializeField] private string locationKey = "job.pizza.loc";
        [SerializeField] private Transform groundRingVisual;

        private Vector3 _baseRingScale = new Vector3(3.2f, 0.06f, 3.2f);

        public JobType Type => jobType;
        public string JobNameKey => jobNameKey;
        public string LocationKey => locationKey;

        public void Configure(JobType type, string nameKey, string locKey, Transform ringVisual)
        {
            jobType = type;
            jobNameKey = nameKey;
            locationKey = locKey;
            groundRingVisual = ringVisual;
            if (groundRingVisual != null)
            {
                _baseRingScale = groundRingVisual.localScale;
            }
        }

        private void Awake()
        {
            if (groundRingVisual != null)
            {
                _baseRingScale = groundRingVisual.localScale;
            }
        }

        private void Update()
        {
            if (groundRingVisual == null) return;
            float pulse = 1f + Mathf.Sin(Time.time * 2.4f) * 0.055f;
            groundRingVisual.localScale = new Vector3(_baseRingScale.x * pulse, _baseRingScale.y, _baseRingScale.z * pulse);
        }
    }
}
