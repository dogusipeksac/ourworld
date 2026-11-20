using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Marks a named point of interest, NPC, vehicle, or delivery bay in the world.
    /// MissionData references anchors by ID so adding new neighborhoods or missions
    /// never requires modifying mission code.
    /// </summary>
    public class WorldAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorId = "anchor_id";
        [SerializeField] private float defaultRadius = 4f;
        [SerializeField] private Vector3 beaconOffset = new Vector3(0f, 2.2f, 0f);

        public string AnchorId => anchorId;
        public float DefaultRadius => defaultRadius;
        public Vector3 BeaconWorldPosition => transform.position + beaconOffset;

        public void Configure(string id, float radius, Vector3 offset)
        {
            anchorId = id;
            defaultRadius = radius;
            beaconOffset = offset;
        }

        private void OnEnable()
        {
            WorldAnchorRegistry.Register(this);
        }

        private void OnDisable()
        {
            WorldAnchorRegistry.Unregister(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.72f, 0.2f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, defaultRadius);
        }
#endif
    }
}
