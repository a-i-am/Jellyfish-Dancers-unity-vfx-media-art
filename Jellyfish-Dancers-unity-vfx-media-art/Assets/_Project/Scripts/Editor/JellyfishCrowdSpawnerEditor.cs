using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JellyfishCrowdSpawner))]
public sealed class JellyfishCrowdSpawnerEditor : Editor
{
    private float testHue = 0.5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the test buttons.", MessageType.Info);
            return;
        }

        GUILayout.Space(15);
        GUILayout.Label("Testing & Simulation", EditorStyles.boldLabel);

        if (GUILayout.Button("Spawn Random Jellyfish (Touch Simulation)", GUILayout.Height(30)))
        {
            var spawner = (JellyfishCrowdSpawner)target;
            spawner.TriggerTestSpawn();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Simulates data received from Mobile Survey after completion.\nh < 0.5: Cyan to Yellow (Calm to Happy)\nh >= 0.5: Yellow to Magenta (Happy to Excited)", MessageType.None);
        testHue = EditorGUILayout.Slider("Emotion Value (h)", testHue, 0f, 1f);

        if (GUILayout.Button("Simulate Survey Data Spawn", GUILayout.Height(30)))
        {
            var spawner = (JellyfishCrowdSpawner)target;
            spawner.SimulateSurveyData(testHue);
        }
    }
}
