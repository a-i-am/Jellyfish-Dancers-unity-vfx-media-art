using UnityEngine;
using Project.Sensors.Network;

public sealed class StageModeManager : MonoBehaviour
{
    public static StageModeManager Instance { get; private set; }

    [SerializeField] private bool useRemoteSpawnMode = true;
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color remoteAmbientColor = new Color(0.043137f, 0.066667f, 0.117647f, 1f);

    public bool UseRemoteSpawnMode => useRemoteSpawnMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (directionalLight == null)
        {
            directionalLight = FindDirectionalLight();
        }
        if (useRemoteSpawnMode)
        {
            if (directionalLight != null)
            {
                directionalLight.gameObject.SetActive(false);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = remoteAmbientColor;
            if (GetComponent<JellyfishTcpServer>() == null)
            {
                gameObject.AddComponent<JellyfishTcpServer>();
            }
        }
        else
        {
            if (directionalLight != null)
            {
                directionalLight.gameObject.SetActive(true);
            }
        }
    }

    private Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                return l;
            }
        }
        return null;
    }
}
