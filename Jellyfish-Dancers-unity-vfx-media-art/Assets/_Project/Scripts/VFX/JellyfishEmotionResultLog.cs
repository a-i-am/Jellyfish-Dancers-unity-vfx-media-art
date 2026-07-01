using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum JellyfishInteractionOutcome
{
    Repelled,
    Accepted
}

public sealed class JellyfishEmotionResultLog : MonoBehaviour
{
    private struct Entry
    {
        public string Label;
        public ThoughtEssenceCategory Category;
        public JellyfishInteractionOutcome Outcome;
        public float Time;
    }

    public static JellyfishEmotionResultLog Instance { get; private set; }

    [SerializeField] private bool showRuntimePanel = true;
    [SerializeField] private KeyCode togglePanelKey = KeyCode.F3;
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [SerializeField] private int maxRecentItems = 7;

    private readonly List<Entry> entries = new List<Entry>(64);
#if UNITY_EDITOR
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle mutedStyle;
#endif
    private int repelledCount;
    private int acceptedCount;

    public int RepelledCount => repelledCount;
    public int AcceptedCount => acceptedCount;
    public int TotalCount => entries.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(togglePanelKey))
        {
            showRuntimePanel = !showRuntimePanel;
        }

        if (Input.GetKeyDown(resetKey))
        {
            Clear();
        }
    }

    public void Register(ThoughtEssence essence, JellyfishInteractionOutcome outcome)
    {
        if (essence == null)
        {
            return;
        }

        Entry entry = new Entry
        {
            Label = essence.Label,
            Category = essence.Category,
            Outcome = outcome,
            Time = Time.time
        };

        entries.Add(entry);
        if (outcome == JellyfishInteractionOutcome.Accepted)
        {
            acceptedCount++;
        }
        else
        {
            repelledCount++;
        }
    }

    public void Clear()
    {
        entries.Clear();
        repelledCount = 0;
        acceptedCount = 0;
    }

    public string BuildPlainTextSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("감정 상호작용 결과");
        builder.AppendLine("품은 감정: " + acceptedCount);
        builder.AppendLine("쳐낸 감정: " + repelledCount);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            builder.Append(entry.Outcome == JellyfishInteractionOutcome.Accepted ? "품음: " : "쳐냄: ");
            builder.Append(entry.Label);
            builder.Append(" / ");
            builder.AppendLine(entry.Category.ToString());
        }

        return builder.ToString();
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showRuntimePanel)
        {
            return;
        }

        EnsureStyles();

        float width = Mathf.Clamp(Screen.width * 0.28f, 260f, 360f);
        float height = 188f;
        Rect panel = new Rect(Screen.width - width - 12f, 12f, width, height);

        GUI.color = new Color(0f, 0f, 0f, 0.46f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        float x = panel.x + 12f;
        float y = panel.y + 8f;
        GUI.Label(new Rect(x, y, width - 24f, 24f), "Emotion Result", titleStyle);
        y += 26f;
        GUI.Label(new Rect(x, y, width - 24f, 20f), "Accepted " + acceptedCount + "  /  Repelled " + repelledCount, bodyStyle);
        y += 22f;

        if (entries.Count == 0)
        {
            GUI.Label(new Rect(x, y, width - 24f, 20f), "Move hand near jellyfish to collect results.", mutedStyle);
            return;
        }

        int start = Mathf.Max(0, entries.Count - maxRecentItems);
        for (int i = start; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            string prefix = entry.Outcome == JellyfishInteractionOutcome.Accepted ? "품음" : "쳐냄";
            GUI.Label(new Rect(x, y, width - 24f, 18f), prefix + "  " + entry.Label, bodyStyle);
            y += 18f;
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        };
        bodyStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);

        mutedStyle = new GUIStyle(bodyStyle);
        mutedStyle.normal.textColor = new Color(0.72f, 0.78f, 0.86f, 1f);
    }
#endif
}
