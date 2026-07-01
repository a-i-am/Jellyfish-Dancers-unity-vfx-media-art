using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class MultiDisplayBootstrap : MonoBehaviour
{
    private const int PrimaryDisplayIndex = 0;
    private const int TvDisplayIndex = 1;

    [Header("TV Universe")]
    [SerializeField]
    private string tvSceneName = "TVUniverseMock";

    public int DetectedDisplayCount =>
        Display.displays.Length;

    public bool TvDisplayActivated { get; private set; }
    public bool TvSceneLoaded { get; private set; }

    private IEnumerator Start()
    {
        Debug.Log(
            $"[MultiDisplay] 감지된 Display 수: " +
            $"{Display.displays.Length}",
            this
        );

#if UNITY_EDITOR
        Debug.Log(
            "[MultiDisplay] Editor에서는 실제 외부 Display " +
            "활성화를 건너뜁니다. Windows Player에서 검증하세요.",
            this
        );

        yield break;
#else
        if (Display.displays.Length <= TvDisplayIndex)
        {
            Debug.LogWarning(
                "[MultiDisplay] Display 2가 없습니다. " +
                "Magic Mirror만 유지하고 TV 씬은 로드하지 않습니다.",
                this
            );

            yield break;
        }

        Display.displays[TvDisplayIndex].Activate();
        TvDisplayActivated = true;

        Debug.Log(
            "[MultiDisplay] Display 2 활성화를 요청했습니다.",
            this
        );

        yield return null;

        Scene tvScene = SceneManager.GetSceneByName(tvSceneName);

        if (!tvScene.isLoaded)
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    tvSceneName,
                    LoadSceneMode.Additive
                );

            if (operation == null)
            {
                Debug.LogError(
                    $"[MultiDisplay] '{tvSceneName}' 씬을 " +
                    "로드하지 못했습니다. Build Profiles를 확인하세요.",
                    this
                );

                yield break;
            }

            yield return operation;
            tvScene = SceneManager.GetSceneByName(tvSceneName);
        }

        TvSceneLoaded = tvScene.isLoaded;

        ValidateTvScene(tvScene);
#endif
    }

    private void ValidateTvScene(Scene tvScene)
    {
        if (!tvScene.IsValid() || !tvScene.isLoaded)
        {
            Debug.LogError(
                "[MultiDisplay] TV Universe 씬이 유효하지 않습니다.",
                this
            );

            return;
        }

        int tvLayer = LayerMask.NameToLayer("TVUniverse");

        if (tvLayer < 0)
        {
            Debug.LogError(
                "[MultiDisplay] TVUniverse Layer가 없습니다.",
                this
            );

            return;
        }

        int expectedMask = 1 << tvLayer;
        int cameraCount = 0;
        bool valid = true;

        foreach (GameObject root in tvScene.GetRootGameObjects())
        {
            Camera[] cameras =
                root.GetComponentsInChildren<Camera>(true);

            foreach (Camera camera in cameras)
            {
                cameraCount++;

                if (camera.targetDisplay != TvDisplayIndex)
                {
                    valid = false;

                    Debug.LogError(
                        $"[MultiDisplay] {camera.name}: " +
                        "Target Display가 Display 2가 아닙니다.",
                        camera
                    );
                }

                if (camera.cullingMask != expectedMask)
                {
                    valid = false;

                    Debug.LogError(
                        $"[MultiDisplay] {camera.name}: " +
                        "Culling Mask가 TVUniverse 전용이 아닙니다.",
                        camera
                    );
                }
            }
        }

        if (cameraCount == 0)
        {
            Debug.LogError(
                "[MultiDisplay] TV Universe 카메라가 없습니다.",
                this
            );

            return;
        }

        if (valid)
        {
            Debug.Log(
                "[MultiDisplay] TV 씬과 카메라 라우팅을 확인했습니다. " +
                "실제 출력은 HDMI 환경에서 검증해야 합니다.",
                this
            );
        }
    }
}
