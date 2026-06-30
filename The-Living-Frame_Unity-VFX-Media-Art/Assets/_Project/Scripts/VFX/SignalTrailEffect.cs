using UnityEngine;

public sealed class SignalTrailEffect : MonoBehaviour
{
    private ParticleSystem partSystem;
    private Vector3 targetPos;
    private System.Action onComplete;
    private float speed = 5.0f;
    private bool isMoving = false;

    private void Awake()
    {
        partSystem = GetComponent<ParticleSystem>();
        if (partSystem == null)
        {
            partSystem = GetComponentInChildren<ParticleSystem>();
        }
    }

    public void Configure(Color color, Vector3 destination, System.Action completeCallback)
    {
        targetPos = destination;
        onComplete = completeCallback;
        if (partSystem != null)
        {
            var main = partSystem.main;
            main.startColor = color;
            var trails = partSystem.trails;
            if (trails.enabled)
            {
                trails.colorOverTrail = color;
            }
            partSystem.Play();
        }
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving) return;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            isMoving = false;
            transform.position = targetPos;
            onComplete?.Invoke();
            onComplete = null;
            if (partSystem != null)
            {
                partSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 1.5f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
