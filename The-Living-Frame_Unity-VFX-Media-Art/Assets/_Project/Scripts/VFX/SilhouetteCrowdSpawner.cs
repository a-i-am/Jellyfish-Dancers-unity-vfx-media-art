using UnityEngine;
using Project.VFX.TVUniverse;

public class SilhouetteCrowdSpawner : MonoBehaviour
{
    public GameObject characterPrefab;
    public RuntimeAnimatorController animatorController;
    public Material gradientMaterial;
    public AnimationClip walkClip;
    public int crowdSize = 7;

    public float minX = -4.5f;
    public float maxX = 4.5f;
    public float minZ = -3.5f;
    public float maxZ = 1.0f;

    public float minSpeed = 0.5f;
    public float maxSpeed = 1.3f;
    public float crowdGravityStrength = 0.35f;
    public float crowdGravityRadius = 2.0f;

    private void Start()
    {
        if (characterPrefab == null)
        {
            return;
        }

        for (int i = 0; i < crowdSize; i++)
        {
            SpawnCharacter();
        }
    }

    private void SpawnCharacter()
    {
        Vector3 spawnPos = new Vector3(
            Random.Range(minX, maxX),
            0f,
            Random.Range(minZ, maxZ)
        );

        GameObject go = Instantiate(characterPrefab, spawnPos, Quaternion.identity, transform);
        go.name = "Crowd_Character_" + Random.Range(1000, 9999);

        float dirX = Random.value > 0.5f ? 1f : -1f;
        float dirZ = Random.Range(-0.3f, 0.3f);
        Vector3 direction = new Vector3(dirX, 0f, dirZ).normalized;
        go.transform.forward = direction;

        float speed = Mathf.Lerp(minSpeed, maxSpeed, 0.45f);
        var walker = go.AddComponent<SilhouetteWalker>();
        walker.Initialize(direction, speed, minX, maxX, minZ, maxZ);
        walker.animationSpeedMultiplier = 0.8f;

        var gravitySource = go.GetComponent<CrowdGravitySource>();
        if (gravitySource == null)
        {
            gravitySource = go.AddComponent<CrowdGravitySource>();
        }
        gravitySource.Configure(crowdGravityStrength, crowdGravityRadius);

        var animator = go.GetComponent<Animator>();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && gradientMaterial != null)
        {
            Material[] newMats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = gradientMaterial;
            }
            smr.sharedMaterials = newMats;
        }
    }
}
