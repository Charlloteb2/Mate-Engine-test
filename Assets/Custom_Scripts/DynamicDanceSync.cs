using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

public class DynamicDanceSync : MonoBehaviour
{
    [Header("Referências")]
    public Animator animator;

    [Header("Config de nomes (evita hard‑code)")]
    public string danceLayerName = "Dance";
    public string danceStateName = "PET_DANCING";
    public string danceParamName = "DanceIndex";

    [Header("Arquivos JSON (StreamingAssets)")]
    public string bpmConfigFile   = "bpm_config.json";
    public string defaultsFile    = "dance_defaults.json";

    private int danceLayerIndex = -1;
    private Dictionary<int, float> defaultBpmByIndex = new Dictionary<int, float>();
    private string bpmJsonPath;
    private string tableJsonPath;
    private string pauseFlagPath;
    private bool wasInDance = false;
    private string lastPauseValue = null;

    private class BpmConfig   { public float currentBPM; }
    private class DefaultLine { public int index; public float bpm; }

    void Awake()
    {
        danceLayerIndex = animator.GetLayerIndex(danceLayerName);
        if (danceLayerIndex < 0)
        {
            Debug.LogWarning($"Layer “{danceLayerName}” não encontrada; usando layer 0");
            danceLayerIndex = 0;
        }

        bpmJsonPath = Path.Combine(Application.streamingAssetsPath, bpmConfigFile);
        tableJsonPath = Path.Combine(Application.streamingAssetsPath, defaultsFile);
        pauseFlagPath = Path.Combine(Application.streamingAssetsPath, "pause_dance_flag.json");

        // Carrega tabela padrão de BPMs
        if (File.Exists(tableJsonPath))
        {
            var json = File.ReadAllText(tableJsonPath);
            var lines = JsonConvert.DeserializeObject<List<DefaultLine>>(json);
            foreach (var l in lines)
                defaultBpmByIndex[l.index] = l.bpm;
        }
        else
        {
            Debug.LogError($"Arquivo {defaultsFile} não encontrado em StreamingAssets");
        }
    }

    void Update()
    {
        var stateInfo = animator.GetCurrentAnimatorStateInfo(danceLayerIndex);
        var clips = animator.GetCurrentAnimatorClipInfo(danceLayerIndex);

        bool inDance = clips.Length > 0 && clips[0].clip.name.StartsWith("PET_DANCING");

        if (inDance)
        {
            if (!File.Exists(bpmJsonPath)) return;

            var cfg = JsonConvert.DeserializeObject<BpmConfig>(File.ReadAllText(bpmJsonPath));
            int danceIndex = Mathf.RoundToInt(animator.GetFloat(danceParamName));

            if (!defaultBpmByIndex.TryGetValue(danceIndex, out float bpmDefault))
                bpmDefault = cfg.currentBPM;

            animator.speed = cfg.currentBPM / bpmDefault;
            Debug.Log($"dancing at {cfg.currentBPM}");
            wasInDance = true;
            WritePauseFlag("false");
        }
        else if (wasInDance)
        {
            animator.speed = 1f;
            wasInDance = false;
            WritePauseFlag("true");
            Debug.Log("[DanceSync] Saiu da dança, resetando Animator.speed para 1");
        }
    }

    void WritePauseFlag(string value)
    {
        if (value != lastPauseValue)
        {
            bool boolValue = value == "true";
            File.WriteAllText(pauseFlagPath, JsonConvert.SerializeObject(boolValue));
            lastPauseValue = value;
        }
    }

}
