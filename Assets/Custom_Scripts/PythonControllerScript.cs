using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System.IO;
using System.Collections;

public class PythonToggleHeadless : MonoBehaviour
{
    [HideInInspector] public Toggle runPythonToggle;
    [HideInInspector] public Toggle runSecondToggle;
    [HideInInspector] public Button runButton;

    private Process pythonProcess;
    private Process secondProcess;

    private const string PlayerPrefKey1 = "PythonScriptEnabled";
    private const string PlayerPrefKey2 = "SecondScriptEnabled";
    private const string FirstTimeKey = "HasLaunchedBefore";

    private const string ExecutableName1 = "media_cores_tela";
    private const string ExecutableName2 = "bpm_detector";
    private const string ExecutableName3 = "extra_settings";
    private const string ExecutableNameFirstTime = "tinker2detect";

    void Awake()
    {
        // Toggles invisíveis
        GameObject toggleObj1 = new GameObject("InvisibleToggle1");
        toggleObj1.hideFlags = HideFlags.HideAndDontSave;
        runPythonToggle = toggleObj1.AddComponent<Toggle>();
        runPythonToggle.isOn = PlayerPrefs.GetInt(PlayerPrefKey1, 1) == 1;
        runPythonToggle.onValueChanged.AddListener(OnToggle1Changed);

        GameObject toggleObj2 = new GameObject("InvisibleToggle2");
        toggleObj2.hideFlags = HideFlags.HideAndDontSave;
        runSecondToggle = toggleObj2.AddComponent<Toggle>();
        runSecondToggle.isOn = PlayerPrefs.GetInt(PlayerPrefKey2, 1) == 1;
        runSecondToggle.onValueChanged.AddListener(OnToggle2Changed);

        GameObject buttonObj = new GameObject("InvisibleButton");
        buttonObj.hideFlags = HideFlags.HideAndDontSave;
        runButton = buttonObj.AddComponent<Button>();
        runButton.onClick.AddListener(RunOnceExecutable);
    }

    private void Start()
    {
        // Checar se é a primeira vez que o app roda
        if (!PlayerPrefs.HasKey(FirstTimeKey))
        {
            ShowFirstTimePopup();
            PlayerPrefs.SetInt(FirstTimeKey, 1);
            PlayerPrefs.Save();
        }

        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(1f);

        if (runPythonToggle.isOn)
            StartExecutable(ref pythonProcess, ExecutableName1);

        if (runSecondToggle.isOn)
            StartExecutable(ref secondProcess, ExecutableName2);
    }

    private void OnToggle1Changed(bool isOn)
    {
        PlayerPrefs.SetInt(PlayerPrefKey1, isOn ? 1 : 0);
        PlayerPrefs.Save();

        if (isOn)
        {
            StartExecutable(ref pythonProcess, ExecutableName1);
        }
        else
        {
            StopExecutable(ExecutableName1, ref pythonProcess);

            // Caminho do .bat
            string batPath = Path.Combine(Application.streamingAssetsPath, "clear_detection_lights.bat");

            if (File.Exists(batPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = batPath,
                        UseShellExecute = true, // precisa ser true para .bat
                        CreateNoWindow = false
                    });

                    UnityEngine.Debug.Log("Script .bat executado com sucesso.");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError("Erro ao executar script.bat: " + ex.Message);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("script.bat não encontrado em: " + batPath);
            }
        }
    }


    private void OnToggle2Changed(bool isOn)
    {
        PlayerPrefs.SetInt(PlayerPrefKey2, isOn ? 1 : 0);
        PlayerPrefs.Save();

        if (isOn)
            StartExecutable(ref secondProcess, ExecutableName2);
        else
            StopExecutable(ExecutableName2, ref secondProcess);
    }

    private void StartExecutable(ref Process procRef, string exeName)
    {
        // Já está rodando ou já foi iniciado?
        if (procRef != null && !procRef.HasExited)
        {
            UnityEngine.Debug.Log($"{exeName} já está rodando, não iniciando novamente.");
            return;
        }

        if (IsExecutableRunning(exeName))
        {
            UnityEngine.Debug.Log($"{exeName} já está rodando no sistema.");
            return;
        }

        string exePath = Path.Combine(Application.streamingAssetsPath, exeName + ".exe");
        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("Executável não encontrado: " + exePath);
            return;
        }

        try
        {
            procRef = new Process();
            procRef.StartInfo.FileName = exePath;
            procRef.StartInfo.UseShellExecute = false;
            procRef.StartInfo.CreateNoWindow = true;
            procRef.Start();

            UnityEngine.Debug.Log("Executável iniciado: " + exeName);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("Erro ao iniciar o executável: " + ex.Message);
        }
    }


    private void StopExecutable(string exeName, ref Process procRef)
    {
        try
        {
            var processes = Process.GetProcessesByName(exeName);
            foreach (var proc in processes)
            {
                try { proc.Kill(); proc.Dispose(); }
                catch { UnityEngine.Debug.LogWarning("Falha ao encerrar: " + proc.ProcessName); }
            }
        }
        catch (System.Exception ex) { UnityEngine.Debug.LogError("Erro ao parar o processo: " + ex.Message); }

        procRef?.Dispose();
        procRef = null;
    }

    private bool IsExecutableRunning(string exeName)
    {
        return Process.GetProcessesByName(exeName).Length > 0;
    }

    public void RunOnceExecutable()
    {
        RunExecutableByName(ExecutableName3);
    }

    private void RunExecutableByName(string exeName)
    {
        string exePath = Path.Combine(Application.streamingAssetsPath, exeName + ".exe");
        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("Executável não encontrado: " + exePath);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            UnityEngine.Debug.Log("Executável iniciado (único): " + exeName);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("Erro ao iniciar: " + ex.Message);
        }
    }

    private void ShowFirstTimePopup()
    {
        GameObject canvasGO = new GameObject("PopupCanvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchoredPosition = Vector2.zero;
        panelGO.AddComponent<CanvasRenderer>();
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.10f, 0.10f, 0.10f, 0.90f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform);
        Text text = textGO.AddComponent<Text>();
        text.text = "Starting audio output detection...\nStart a song on YouTube or another player.";
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(380, 120);
        textRect.anchoredPosition = new Vector2(0, 30);

        GameObject buttonGO = new GameObject("OKButton");
        buttonGO.transform.SetParent(panelGO.transform);
        Button button = buttonGO.AddComponent<Button>();
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = Color.black;
        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(100, 40);
        buttonRect.anchoredPosition = new Vector2(0, -50);

        GameObject buttonTextGO = new GameObject("ButtonText");
        buttonTextGO.transform.SetParent(buttonGO.transform);
        Text buttonText = buttonTextGO.AddComponent<Text>();
        buttonText.text = "OK";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.color = Color.white;
        RectTransform btnTextRect = buttonTextGO.GetComponent<RectTransform>();
        btnTextRect.sizeDelta = new Vector2(100, 40);
        btnTextRect.anchoredPosition = Vector2.zero;

        // Quando clicar em OK
        button.onClick.AddListener(() =>
        {
            RunExecutableByName(ExecutableNameFirstTime);
            GameObject.Destroy(canvasGO); // remove popup
        });
    }

    private void OnApplicationQuit()
    {
        pythonProcess?.WaitForExit(2000);
        secondProcess?.WaitForExit(2000);

        StopExecutable(ExecutableName1, ref pythonProcess);
        StopExecutable(ExecutableName2, ref secondProcess);
    }
}
