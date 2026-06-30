using UnityEngine;
using UnityEngine.VFX;
using Project.Input;
using Project.VFX.TVUniverse;

namespace Project.VFX
{
    public class MusicTrigger : MonoBehaviour
    {
        public static MusicTrigger Instance { get; private set; }
        public static bool IsPlaying { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private VisualEffect noteVfx;
        [SerializeField] private Sprite[] noteSprites;
        [SerializeField] private float beatsPerMinute = 96f;
        [SerializeField] private int noteBurstCount = 6;
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private SensorToTVUniverseVfxBinder universeBinder;
        [SerializeField] private float playAttractorThreshold = 5f;
        [SerializeField] private float stopAttractorThreshold = 2.5f;

        private InputAggregator inputAggregator;
        private AttractorStage attractorStage;
        private int currentNoteIndex;
        private bool isPlayingMusic;
        private float lastMusicTime;
        private float nextNoteTime;

        private void Awake() => Instance = this;

        private void Start()
        {
            inputAggregator = FindFirstObjectByType<InputAggregator>();
            attractorStage = FindFirstObjectByType<AttractorStage>();

            if (musicSource == null)
                musicSource = GetComponent<AudioSource>();

            if (universeBinder == null)
                universeBinder = SensorToTVUniverseVfxBinder.Instance ?? FindFirstObjectByType<SensorToTVUniverseVfxBinder>();

            if (musicSource != null)
            {
                if (musicClip != null)
                    musicSource.clip = musicClip;

                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }
        }

        private void Update()
        {
            if (musicSource == null)
                return;

            bool inRange = ShouldPlayMusic();

            if (inRange && !isPlayingMusic)
            {
                ResumeMusic();
            }
            else if (!inRange && isPlayingMusic)
            {
                PauseMusic();
            }

            if (isPlayingMusic)
            {
                UpdateNoteSpawning();
            }

        }

        private bool ShouldPlayMusic()
        {
            float threshold = isPlayingMusic ? stopAttractorThreshold : playAttractorThreshold;
            if (inputAggregator == null || attractorStage == null)
                return false;

            InputState state = inputAggregator.CurrentState;
            float attractor = state.WebcamMotionScore * 1000f;
            if (universeBinder != null && universeBinder.isActiveAndEnabled)
                attractor = Mathf.Max(attractor, universeBinder.FinalAttractorStrength);

            if (attractor > 0f)
                return attractor >= threshold;

            float attractorDist = Vector3.Distance(state.AttractorPos, attractorStage.GetBoundsCenter());
            return attractorDist < attractorStage.GravityRadius;
        }

        private void ResumeMusic()
        {
            if (musicSource == null) return;

            if (musicSource.clip == null)
            {
                Debug.LogError("Music clip not assigned");
                return;
            }

            if (musicSource.time >= musicSource.clip.length)
                musicSource.time = 0f;

            musicSource.Play();
            isPlayingMusic = true;
            IsPlaying = true;
        }

        private void PauseMusic()
        {
            if (musicSource != null)
            {
                musicSource.Pause();
            }
            isPlayingMusic = false;
            IsPlaying = false;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            IsPlaying = false;
        }

        private void UpdateNoteSpawning()
        {
            if (musicSource == null || !musicSource.isPlaying)
                return;

            float currentTime = musicSource.time;
            if (currentTime < lastMusicTime)
            {
                currentNoteIndex = 0;
                nextNoteTime = 0f;
            }

            lastMusicTime = currentTime;
            float step = Mathf.Max(0.05f, 60f / Mathf.Max(1f, beatsPerMinute) * 0.5f);
            int emitted = 0;
            while (currentTime >= nextNoteTime && emitted < 4)
            {
                SpawnNoteParticle(currentNoteIndex);
                currentNoteIndex++;
                nextNoteTime += step;
                emitted++;
            }

            if (currentTime >= nextNoteTime)
                nextNoteTime = currentTime + step;
        }

        private void SpawnNoteParticle(int noteIndex)
        {
            InputState state = inputAggregator.CurrentState;
            int noteType = GetTempoNoteType(noteIndex);

            SpawnNoteSprites(noteType, state.AttractorPos);
            if (noteVfx == null) return;

            if (noteVfx.HasVector3("SpawnPos"))
                noteVfx.SetVector3("SpawnPos", state.AttractorPos);
            if (noteVfx.HasInt("NoteType"))
                noteVfx.SetInt("NoteType", noteType);
            noteVfx.SendEvent("OnNoteSpawn");
        }

        private int GetTempoNoteType(int noteIndex)
        {
            int step = noteIndex % 8;
            if (step == 0) return 0;
            if (step == 4) return 2;
            return step % 2 == 0 ? 1 : 3;
        }

        private void SpawnNoteSprites(int noteType, Vector3 fallbackPosition)
        {
            if (noteSprites == null || noteSprites.Length == 0)
                return;

            Transform parent = null;
            var agents = JellyfishAgent.ActiveAgents;
            for (int i = 0; i < agents.Count; i++)
            {
                JellyfishAgent agent = agents[Random.Range(0, agents.Count)];
                if (agent != null && agent.isActiveAndEnabled)
                {
                    parent = agent.transform;
                    break;
                }
            }

            int count = Mathf.Clamp(noteBurstCount, 1, 32);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Tempo_Note");
                Transform t = go.transform;
                if (parent != null)
                {
                    t.SetParent(parent, false);
                    t.localPosition = Random.insideUnitSphere * 0.9f + Vector3.up * Random.Range(0.2f, 1.1f);
                }
                else
                {
                    t.position = fallbackPosition + Random.insideUnitSphere * 0.9f;
                }

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = noteSprites[Mathf.Clamp(noteType, 0, noteSprites.Length - 1)];
                renderer.color = new Color(0.8f, 0.95f, 1f, 0.85f);
                renderer.sortingOrder = 12;

                float size = Random.Range(0.08f, 0.18f);
                t.localScale = Vector3.one * size;
                go.AddComponent<MusicNoteParticle>().Initialize(Random.Range(1.2f, 2.4f));
            }
        }

        public void SetMusicClip(AudioClip clip)
        {
            musicClip = clip;
            if (musicSource != null)
                musicSource.clip = clip;
        }

    }

    internal sealed class MusicNoteParticle : MonoBehaviour
    {
        private float lifetime;
        private float age;
        private Vector3 drift;
        private SpriteRenderer spriteRenderer;

        public void Initialize(float seconds)
        {
            lifetime = Mathf.Max(0.1f, seconds);
            drift = Random.onUnitSphere * Random.Range(0.15f, 0.55f) + Vector3.up * Random.Range(0.2f, 0.8f);
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            float t = age / lifetime;
            transform.localPosition += drift * Time.deltaTime;
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.SmoothStep(0.85f, 0f, t);
                spriteRenderer.color = color;
            }

            if (age >= lifetime)
                Destroy(gameObject);
        }
    }
}
