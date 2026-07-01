using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Animator))]
public class SkinnedMeshParticleSilhouette : MonoBehaviour
{
    public SkinnedMeshRenderer targetRenderer;
    public AnimationClip walkClip;

    private PlayableGraph playableGraph;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (targetRenderer != null)
        {
            ApplyAuroraSilhouetteMaterial(targetRenderer);
        }
    }

    private void Start()
    {
        PlayWalkAnimation();
    }

    private void ApplyAuroraSilhouetteMaterial(SkinnedMeshRenderer smr)
    {
        Shader auroraShader = Shader.Find("TheLivingFrame/AuroraSilhouette");
        if (auroraShader == null)
        {
            Debug.LogWarning("AuroraSilhouette shader not found!");
            return;
        }

        Material auroraMat = new Material(auroraShader);


        float hueA = Random.value;
        float hueB = (hueA + Random.Range(0.15f, 0.35f)) % 1f;
        Color colorA = Color.HSVToRGB(hueA, 0.85f, 1f);
        Color colorB = Color.HSVToRGB(hueB, 0.85f, 1f);

        auroraMat.SetColor("_BaseColor", colorA);
        auroraMat.SetColor("_Color2", colorB);
        auroraMat.SetFloat("_NoiseScale", Random.Range(1.5f, 3.5f));
        auroraMat.SetFloat("_Speed", Random.Range(0.3f, 0.8f));
        auroraMat.SetFloat("_Intensity", Random.Range(1.2f, 2.0f));

        Material[] mats = new Material[smr.sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            mats[i] = auroraMat;
        }
        smr.materials = mats;


        ParticleSystem partSystem = GetComponent<ParticleSystem>();
        if (partSystem != null)
        {
            Destroy(partSystem);
        }
    }

    private void PlayWalkAnimation()
    {
        if (walkClip == null)
        {
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }

        playableGraph = PlayableGraph.Create("SilhouetteWalk");
        var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        var clipPlayable = AnimationClipPlayable.Create(playableGraph, walkClip);

        clipPlayable.SetTime(Random.Range(0f, walkClip.length));

        playableOutput.SetSourcePlayable(clipPlayable);
        playableGraph.Play();
    }

    private void OnDestroy()
    {
        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (walkClip == null)
        {
            string[] guids = AssetDatabase.FindAssets("Standard_Walk t:Model");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip && !asset.name.Contains("__preview__"))
                    {
                        walkClip = (AnimationClip)asset;
                        break;
                    }
                }
            }
        }
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }
#endif
}
