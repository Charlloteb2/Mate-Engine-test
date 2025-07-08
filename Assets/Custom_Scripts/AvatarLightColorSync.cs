using System.IO;
using UnityEngine;

public class AvatarLightColorSync : MonoBehaviour
{
    public Light avatarLight;
    public Light ambiLight1;
    public Light ambiLight2;
    public Light ambiLight3;

    private string colorFilePath;
    private string configFilePath;

    [System.Serializable]
    public class LightData
    {
        public float r = 1f;
        public float g = 1f;
        public float b = 1f;
        public float intensity = 1f;
    }

    [System.Serializable]
    public class AllLightsData
    {
        public LightData avatar = new LightData();
        public LightData ambi1 = new LightData();
        public LightData ambi2 = new LightData();
        public LightData ambi3 = new LightData();
    }

    [System.Serializable]
    public class LightConfig
    {
        public string avatarShadow = "Hard"; // Opções: "None", "Hard", "Soft"
    }

    void Start()
    {
        colorFilePath = Path.Combine(Application.streamingAssetsPath, "color_data.json");
        configFilePath = Path.Combine(Application.streamingAssetsPath, "light_config.json");
    }

    void Update()
    {
        if (File.Exists(colorFilePath))
        {
            try
            {
                string json = File.ReadAllText(colorFilePath);
                AllLightsData data = JsonUtility.FromJson<AllLightsData>(json);

                ApplyLight(avatarLight, data.avatar);
                ApplyLight(ambiLight1, data.ambi1);
                ApplyLight(ambiLight2, data.ambi2);
                ApplyLight(ambiLight3, data.ambi3);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Erro lendo JSON de luzes: " + ex.Message);
            }
        }

        if (File.Exists(configFilePath))
        {
            try
            {
                string configJson = File.ReadAllText(configFilePath);
                LightConfig config = JsonUtility.FromJson<LightConfig>(configJson);
                ApplyShadowConfig(avatarLight, config.avatarShadow);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Erro lendo JSON de config: " + ex.Message);
            }
        }
    }

    void ApplyLight(Light light, LightData data)
    {
        if (light != null)
        {
            light.color = new Color(data.r, data.g, data.b);
            light.intensity = data.intensity;
        }
    }

    void ApplyShadowConfig(Light light, string shadowType)
    {
        if (light != null)
        {
            switch (shadowType.ToLower())
            {
                case "none":
                    light.shadows = LightShadows.None;
                    break;
                case "hard":
                    light.shadows = LightShadows.Hard;
                    break;
                case "soft":
                    light.shadows = LightShadows.Soft;
                    break;
                default:
                    Debug.LogWarning("Tipo de sombra inválido: " + shadowType);
                    break;
            }
        }
    }
}
