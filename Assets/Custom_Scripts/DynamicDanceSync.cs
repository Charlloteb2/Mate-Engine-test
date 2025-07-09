using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Concurrent; // Adicionado para ConcurrentQueue

public class DynamicDanceSync : MonoBehaviour
{
    [Header("Referências")]
    public Animator animator; // Esta referência será atualizada dinamicamente pelo AvatarAnimatorController

    [Header("Config de nomes")]
    public string danceLayerName = ""; // deixe vazio para usar layer 0 (Base Layer)
    public string danceParamName = "DanceIndex"; // Parâmetro que o Animator usa para o índice da dança

    private int danceLayerIndex = 0;
    private Dictionary<int, float> defaultBpmByIndex = new Dictionary<int, float>();
    private float currentBPM = 120f;
    private bool isCurrentlyDancing = false; 
    private int currentDanceIndex = 0; 

    private UdpClient udpClient;
    // Fila para enfileirar ações que precisam ser executadas na thread principal
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    void Awake()
    {
        Debug.Log("[DanceSync] START FOI CHAMADO");

        defaultBpmByIndex = new Dictionary<int, float>()
        {
            { 0, 137 }, { 1, 108 }, { 2, 125 },
            { 3, 106 }, { 4, 141 }, { 5, 100 },
            { 6, 120 }, { 7, 128 }, { 8, 108 },
            { 9, 184 }, { 10, 82 }, { 11, 121 },
            { 12, 185 }
        };

        // Iniciar UDP
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 9955));
            udpClient.BeginReceive(OnUdpData, null);
            Debug.Log("[DanceSync] Esperando BPM via UDP na porta 9955...");
        }
        catch (Exception e)
        {
            Debug.LogError("[DanceSync] Erro ao iniciar UDP: " + e.Message);
        }
    }

    void Update()
    {
        // Processa todas as ações enfileiradas na thread principal
        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    void OnUdpData(IAsyncResult ar)
    {
        try
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 9955);
            byte[] bytes = udpClient.EndReceive(ar, ref ip);
            string bpmStr = Encoding.UTF8.GetString(bytes).Trim();

            if (float.TryParse(bpmStr, out float bpm))
            {
                currentBPM = bpm;
                Debug.Log($"[DanceSync] BPM recebido via UDP: {bpm}");
                // Enfileira a ação de aplicar a velocidade para ser executada na thread principal
                mainThreadActions.Enqueue(() => ApplyDanceSpeed()); 
            }
            else
            {
                Debug.LogWarning("[DanceSync] Dados UDP inválidos: " + bpmStr);
            }

            udpClient.BeginReceive(OnUdpData, null);
        }
        catch (Exception e)
        {
            // O erro original ocorreu aqui, agora ele será evitado
            Debug.LogError("[DanceSync] Erro na recepção UDP: " + e.Message);
            // Certifique-se de que o BeginReceive seja chamado novamente mesmo após um erro,
            // ou a escuta UDP parará.
            udpClient.BeginReceive(OnUdpData, null); 
        }
    }

    /// <summary>
    /// Método público para receber a referência do Animator do avatar atualmente ativo.
    /// Chamado pelo AvatarAnimatorController.
    /// </summary>
    /// <param name="newAnimator">O Animator do avatar atual.</param>
    public void SetTargetAnimator(Animator newAnimator)
    {
        // Esta operação já é esperada na thread principal (chamada pelo AvatarAnimatorController.OnEnable)
        if (newAnimator != null && newAnimator != animator)
        {
            animator = newAnimator;
            Debug.Log("[DanceSync] Animator alvo atualizado para: " + newAnimator.gameObject.name);

            if (!string.IsNullOrEmpty(danceLayerName))
            {
                danceLayerIndex = animator.GetLayerIndex(danceLayerName);
                if (danceLayerIndex < 0)
                {
                    Debug.LogWarning($"[DanceSync] Layer “{danceLayerName}” não encontrada no novo Animator; usando layer 0");
                    danceLayerIndex = 0;
                }
            }
            isCurrentlyDancing = false; 
            // Reaplicar a velocidade caso o Animator tenha mudado enquanto já estava dançando
            ApplyDanceSpeed();
        }
        else if (newAnimator == null)
        {
            Debug.LogWarning("[DanceSync] Tentativa de definir um Animator alvo nulo.");
        }
    }

    /// <summary>
    /// Recebe o estado de dança e o índice do AvatarAnimatorController.
    /// </summary>
    /// <param name="isDancing">True se o avatar estiver dançando.</param>
    /// <param name="danceIndex">O índice da animação de dança.</param>
    public void SetDanceState(bool isDancing, int danceIndex)
    {
        // Esta operação já é esperada na thread principal (chamada pelo AvatarAnimatorController.SetDancing)
        if (animator == null) return;

        isCurrentlyDancing = isDancing;
        currentDanceIndex = danceIndex;
        
        ApplyDanceSpeed();
    }

    private void ApplyDanceSpeed()
    {
        // Esta função agora SEMPRE será chamada na thread principal.
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[DanceSync] Animator ou Controller nulo. Não é possível aplicar velocidade.");
            return;
        }

        if (isCurrentlyDancing)
        {
            if (!defaultBpmByIndex.TryGetValue(currentDanceIndex, out float bpmDefault))
            {
                Debug.LogWarning($"[DanceSync] Index {currentDanceIndex} não encontrado; usando BPM atual ({currentBPM}) como padrão para cálculo da velocidade.");
                bpmDefault = currentBPM > 0.1f ? currentBPM : 120f; // Fallback para 120 se o BPM atual for muito baixo/zero
            }

            // Evitar divisão por zero se bpmDefault for 0 ou muito próximo de 0
            float newSpeed = (bpmDefault > 0.1f) ? (currentBPM / bpmDefault) : 1f; 
            animator.speed = newSpeed;

            Debug.Log($"[DanceSync] Dançando. Index: {currentDanceIndex}, BPM Atual: {currentBPM}, BPM Padrão: {bpmDefault}, Velocidade Aplicada: {newSpeed}");
        }
        else
        {
            // Se não estiver dançando, reseta a velocidade para 1
            if (animator.speed != 1f) 
            {
                animator.speed = 1f;
                Debug.Log("[DanceSync] Não dançando, resetando Animator.speed para 1");
            }
        }
    }

    void OnApplicationQuit()
    {
        udpClient?.Close();
        Debug.Log("[DanceSync] Cliente UDP fechado ao sair da aplicação.");
    }
}