using UnityEngine;

public class SilhouetteWalker : MonoBehaviour
{
    public float speed = 1.0f;
    public float minX = -4.5f;
    public float maxX = 4.5f;
    public float minZ = -3.5f;
    public float maxZ = 1.0f;

    [Header("Animation")]
    public Animator animator;
    public string walkAnimationState = "Walk";
    public bool useSpeedParameter = true;
    [Tooltip("애니메이션 재생 속도를 조절합니다. (다리 움직임 속도)")]
    public float animationSpeedMultiplier = 1.5f;

    private Vector3 direction;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null && !useSpeedParameter && !string.IsNullOrEmpty(walkAnimationState))
        {
            animator.Play(walkAnimationState);
        }
    }

    public void Initialize(Vector3 dir, float moveSpeed, float xMin, float xMax, float zMin, float zMax)
    {
        direction = dir.normalized;
        speed = moveSpeed;
        minX = xMin;
        maxX = xMax;
        minZ = zMin;
        maxZ = zMax;
        transform.forward = direction;
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);

        Vector3 pos = transform.position;
        bool outOfBounds = false;

        if (direction.x > 0 && pos.x > maxX) { pos.x = minX; outOfBounds = true; }
        else if (direction.x < 0 && pos.x < minX) { pos.x = maxX; outOfBounds = true; }

        if (direction.z > 0 && pos.z > maxZ) { pos.z = minZ; outOfBounds = true; }
        else if (direction.z < 0 && pos.z < minZ) { pos.z = maxZ; outOfBounds = true; }

        if (outOfBounds)
        {
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
        }

        if (animator != null && useSpeedParameter)
        {
            animator.SetFloat("Speed", speed);

            animator.SetFloat("MotionSpeed", animationSpeedMultiplier);
            animator.SetBool("Grounded", true);
        }
    }



    private void OnFootstep(AnimationEvent animationEvent) { }
    private void OnLand(AnimationEvent animationEvent) { }
}
