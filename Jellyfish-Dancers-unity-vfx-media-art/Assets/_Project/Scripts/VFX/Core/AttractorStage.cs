using UnityEngine;

namespace Project.VFX
{
    public class AttractorStage : MonoBehaviour
    {
        [Header("Aquarium Bounds")]
        [SerializeField] private Vector2 xBounds = new Vector2(-4.2f, 4.2f);
        [SerializeField] private Vector2 yBounds = new Vector2(0.35f, 3.2f);
        [SerializeField] private Vector2 zBounds = new Vector2(-3.35f, 0.85f);

        [Header("Interaction")]
        [SerializeField] private float gravityRadius = 1.8f;
        [SerializeField] private float gravityStrength = 0.28f;

        public static AttractorStage Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsInBounds(Vector3 worldPos)
        {
            return worldPos.x >= xBounds.x && worldPos.x <= xBounds.y &&
                   worldPos.y >= yBounds.x && worldPos.y <= yBounds.y &&
                   worldPos.z >= zBounds.x && worldPos.z <= zBounds.y;
        }

        public Vector2 XBounds => xBounds;
        public Vector2 YBounds => yBounds;
        public Vector2 ZBounds => zBounds;
        public float GravityRadius => gravityRadius;
        public float GravityStrength => gravityStrength;

        public Vector3 GetBoundsCenter()
        {
            return new Vector3(
                (xBounds.x + xBounds.y) * 0.5f,
                (yBounds.x + yBounds.y) * 0.5f,
                (zBounds.x + zBounds.y) * 0.5f);
        }

        public Vector3 GetBoundsSize()
        {
            return new Vector3(
                xBounds.y - xBounds.x,
                yBounds.y - yBounds.x,
                zBounds.y - zBounds.x);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = GetBoundsCenter();
            Vector3 size = GetBoundsSize();
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, gravityRadius);
        }
#endif
    }
}
