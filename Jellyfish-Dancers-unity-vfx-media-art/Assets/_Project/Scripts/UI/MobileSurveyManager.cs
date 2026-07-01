using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class MobileSurveyManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject setupPanel;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private GameObject statusPanel;

    [Header("Setup UI")]
    [SerializeField] private InputField ipInputField;
    [SerializeField] private Button startButton;

    [Header("Question UI")]
    [SerializeField] private Text questionTitleText;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject optionButtonPrefab;

    [Header("Results UI")]
    [SerializeField] private Transform resultsContainer;
    [SerializeField] private Button sendButton;
    [SerializeField] private GameObject resultRowPrefab;

    [Header("Status UI")]
    [SerializeField] private Text statusText;
    [SerializeField] private Button restartButton;

    private string targetIp = "127.0.0.1";
    private int currentStage = 1;

    private List<ThoughtEssence> currentPool = new();
    private HashSet<int> selectedIndices = new();
    private List<ThoughtEssence> selectedEmotions = new();
    private List<float> finalIntensities = new();

    private void Awake()
    {
        ShowPanel(setupPanel);
        startButton.onClick.AddListener(OnStartClicked);
        nextButton.onClick.AddListener(OnNextClicked);
        sendButton.onClick.AddListener(OnSendClicked);
        restartButton.onClick.AddListener(OnRestartClicked);
    }

    private void OnStartClicked()
    {
        if (!string.IsNullOrEmpty(ipInputField.text))
        {
            targetIp = ipInputField.text.Trim();
        }

        currentPool.Clear();
        int count = ThoughtEssenceCatalog.Count;
        for (int i = 0; i < count; i++)
        {
            currentPool.Add(ThoughtEssenceCatalog.Get(i));
        }

        currentStage = 1;
        selectedIndices.Clear();
        ShowQuestionStage();
    }

    private void ShowQuestionStage()
    {
        ShowPanel(questionPanel);
        ClearContainer(optionsContainer);
        selectedIndices.Clear();

        if (currentStage == 1)
        {
            questionTitleText.text = "1단계: 현재 당신이 느끼고 있는 감정들을 모두 선택해 주세요. (최소 6개)";
        }
        else if (currentStage == 2)
        {
            questionTitleText.text = "2단계: 선택하신 감정 중 특히 강하게 느껴지는 것들을 골라주세요. (최소 3개)";
        }
        else if (currentStage == 3)
        {
            questionTitleText.text = "3단계: 최종적으로 남길 가장 핵심적인 감정 3가지를 선택해 주세요. (정확히 3개)";
        }

        for (int i = 0; i < currentPool.Count; i++)
        {
            int index = i;
            GameObject go = Instantiate(optionButtonPrefab, optionsContainer, false);
            go.SetActive(true);
            Text txt = go.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = currentPool[index].Label;
            }

            Image img = go.GetComponent<Image>();
            Button btn = go.GetComponent<Button>();
            if (btn != null && img != null)
            {
                Color defaultColor = img.color;
                btn.onClick.AddListener(() =>
                {
                    if (selectedIndices.Contains(index))
                    {
                        selectedIndices.Remove(index);
                        img.color = defaultColor;
                    }
                    else
                    {
                        if (currentStage == 3 && selectedIndices.Count >= 3)
                        {
                            return;
                        }
                        selectedIndices.Add(index);
                        img.color = new Color(0.3f, 0.75f, 1f, 0.7f);
                    }
                    ValidateNextButton();
                });
            }
        }
        ValidateNextButton();
    }

    private void ValidateNextButton()
    {
        if (currentStage == 1)
        {
            nextButton.interactable = selectedIndices.Count >= 6;
        }
        else if (currentStage == 2)
        {
            nextButton.interactable = selectedIndices.Count >= 3 && selectedIndices.Count < currentPool.Count;
        }
        else if (currentStage == 3)
        {
            nextButton.interactable = selectedIndices.Count == 3;
        }
    }

    private void OnNextClicked()
    {
        List<ThoughtEssence> nextPool = new();
        foreach (int idx in selectedIndices)
        {
            nextPool.Add(currentPool[idx]);
        }

        currentPool = nextPool;

        if (currentStage < 3)
        {
            currentStage++;
            ShowQuestionStage();
        }
        else
        {
            ShowResults();
        }
    }

    private void ShowResults()
    {
        ShowPanel(resultsPanel);
        ClearContainer(resultsContainer);
        selectedEmotions = new List<ThoughtEssence>(currentPool);
        finalIntensities.Clear();

        for (int i = 0; i < selectedEmotions.Count; i++)
        {
            int index = i;
            finalIntensities.Add(0.8f);

            GameObject go = Instantiate(resultRowPrefab, resultsContainer, false);
            go.SetActive(true);
            LayoutElement layoutElement = go.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = go.AddComponent<LayoutElement>();
            }
            layoutElement.preferredHeight = 80f;
            Text labelText = go.transform.Find("LabelText")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = selectedEmotions[index].Label;
            }

            Slider slider = go.transform.Find("IntensitySlider")?.GetComponent<Slider>();
            if (slider != null)
            {
                slider.value = 0.8f;
                slider.onValueChanged.AddListener((val) =>
                {
                    finalIntensities[index] = val;
                });
            }
        }
    }

    private void OnSendClicked()
    {
        ShowPanel(statusPanel);
        statusText.text = "전송 중...";
        restartButton.gameObject.SetActive(false);
        StartCoroutine(SendRequestsCoroutine());
    }

    private IEnumerator SendRequestsCoroutine()
    {
        bool success = true;
        string errorMsg = "";

        for (int i = 0; i < selectedEmotions.Count; i++)
        {
            string label = selectedEmotions[i].Label;
            string category = selectedEmotions[i].Category.ToString();
            float intensity = finalIntensities[i];

            string json = "{\"jsonrpc\":\"2.0\",\"method\":\"spawn\",\"params\":{\"label\":\"" + label + "\",\"category\":\"" + category + "\",\"intensity\":" + intensity.ToString("F2") + "}}";

            bool singleSuccess = false;
            yield return StartCoroutine(SendTcpMessage(json, (res, err) =>
            {
                singleSuccess = res;
                errorMsg = err;
            }));

            if (!singleSuccess)
            {
                success = false;
                break;
            }

            yield return new WaitForSeconds(0.4f);
        }

        if (success)
        {
            statusText.text = "성공적으로 전송되었습니다!";
        }
        else
        {
            statusText.text = "전송 실패: " + errorMsg;
        }
        restartButton.gameObject.SetActive(true);
    }

    private IEnumerator SendTcpMessage(string payload, Action<bool, string> callback)
    {
        bool done = false;
        bool success = false;
        string err = "";

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(targetIp, 9100, null, null);
                    var successConnect = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3.0));
                    if (!successConnect)
                    {
                        throw new Exception("Timeout connecting to " + targetIp);
                    }
                    client.EndConnect(result);

                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(payload + "\n");
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                }
                success = true;
            }
            catch (Exception e)
            {
                err = e.Message;
                success = false;
            }
            finally
            {
                System.Threading.Volatile.Write(ref done, true);
            }
        });

        while (!System.Threading.Volatile.Read(ref done))
        {
            yield return null;
        }

        callback(success, err);
    }

    private void OnRestartClicked()
    {
        ShowPanel(setupPanel);
    }

    private void ShowPanel(GameObject target)
    {
        setupPanel.SetActive(setupPanel == target);
        questionPanel.SetActive(questionPanel == target);
        resultsPanel.SetActive(resultsPanel == target);
        statusPanel.SetActive(statusPanel == target);
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(container.GetChild(i).gameObject);
        }
    }
}
