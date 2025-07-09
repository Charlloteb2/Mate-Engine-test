using UnityEngine;
using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Diagnostics; // This using causes the ambiguity for Debug
using System.Collections;
using System; 

public class AvatarAnimatorController : MonoBehaviour
{
    [Header("Core References")]
    public Animator animator; // Esta referência será o Animator ativo
    public DynamicDanceSync dynamicDanceSync; // Referência para o sistema de dança

    [Header("Audio Detection")]
    public float SOUND_THRESHOLD = 0.02f;
    public List<string> allowedApps = new();

    [Header("Animation Logic")]
    public int totalIdleAnimations = 10;
    public float IDLE_SWITCH_TIME = 12f, IDLE_TRANSITION_TIME = 3f;
    public int DANCE_CLIP_COUNT = 5;
    public bool enableDancing = true;
    public bool BlockDraggingOverride = false;
    public string greetingAnimationStateName = "Intro"; // Você queria isso permanente, o VRMLoader já garante isso!

    // --- Variáveis Internas ---
    private bool isDanceSyncInitialized = false; // NOSSA NOVA FLAG DE CONTROLE!
    private Animator _previousAnimator;
    
    private static readonly int danceIndexParam = Animator.StringToHash("DanceIndex");
    private static readonly int isIdleParam = Animator.StringToHash("isIdle");
    private static readonly int isDraggingParam = Animator.StringToHash("isDragging");
    private static readonly int isDancingParam = Animator.StringToHash("isDancing");
    private static readonly int idleIndexParam = Animator.StringToHash("IdleIndex");

    private MMDevice defaultDevice;
    private MMDeviceEnumerator enumerator;
    private Coroutine soundCheckCoroutine, idleTransitionCoroutine;
    private float lastSoundCheckTime, idleTimer;
    private int idleState;
    private float dragLockTimer;
    private bool mouseHeld;
    public bool isDragging, isDancing, isIdle;
    private int currentDanceIndex;

    void OnEnable()
    {
        // Reseta o estado de inicialização sempre que o objeto é ativado
        isDanceSyncInitialized = false;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // A tentativa inicial de setup ainda pode acontecer aqui, mas sem problemas se falhar.
        // A lógica no Update() garantirá que a conexão seja feita eventualmente.
        SetupAnimatorAndDanceSync(animator);

        Application.runInBackground = true;
        
        try
        {
            enumerator = new MMDeviceEnumerator();
            defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[AvatarAnimatorController] Falha ao inicializar dispositivo de áudio: " + e.Message);
        }
        
        if (soundCheckCoroutine != null) StopCoroutine(soundCheckCoroutine);
        soundCheckCoroutine = StartCoroutine(CheckSoundContinuously());
    }

    // Este método agora é a única fonte da verdade para configurar a conexão.
    public void SetupAnimatorAndDanceSync(Animator newAnimator)
    {
        if (newAnimator == null)
        {
            UnityEngine.Debug.LogWarning("[AvatarAnimatorController] Tentativa de configurar um Animator nulo.");
            animator = null; 
            return;
        }

        animator = newAnimator;
        _previousAnimator = newAnimator;
        
        // Se a referência do DanceSync existir, faz a conexão e marca como inicializado.
        if (dynamicDanceSync != null)
        {
            UnityEngine.Debug.Log($"[AvatarAnimatorController] Conexão estabelecida! Enviando Animator '{animator.gameObject.name}' para DynamicDanceSync.");
            dynamicDanceSync.SetTargetAnimator(animator);
            isDanceSyncInitialized = true; // SUCESSO!
        }
        else
        {
            // Se não, marca que ainda precisa inicializar. O Update() tentará de novo.
            UnityEngine.Debug.LogWarning($"[AvatarAnimatorController] 'dynamicDanceSync' ainda é nulo para o avatar '{animator.gameObject.name}'. Tentando novamente no próximo frame.");
            isDanceSyncInitialized = false;
        }

        // Força a atualização dos estados para o novo animator
        UpdateAllStates();
    }

    void Update()
    {
        // ----- NOSSA NOVA LÓGICA DE VERIFICAÇÃO -----
        // Se a conexão ainda não foi feita E a referência do DanceSync já foi preenchida pelo VRMLoader...
        if (!isDanceSyncInitialized && dynamicDanceSync != null)
        {
            // ... então agora é a hora certa de fazer a configuração!
            SetupAnimatorAndDanceSync(this.animator);
        }
        // ---------------------------------------------

        if (animator == null) return;
        
        // Esta verificação extra garante que se o animator for trocado manualmente, a conexão seja refeita.
        if (animator != _previousAnimator)
        {
            SetupAnimatorAndDanceSync(animator);
        }

        // Lógica de Dragging
        if (BlockDraggingOverride || MenuActions.IsMovementBlocked() || (typeof(TutorialMenu) != null && TutorialMenu.IsActive))
        {
            if (isDragging) SetDragging(false);
            if (isDancing) SetDancing(false); 
            return;
        }
        if (Input.GetMouseButtonDown(0))
        {
            SetDragging(true);
            mouseHeld = true;
            dragLockTimer = 0.30f;
            if(isDancing) SetDancing(false);
        }
        if (Input.GetMouseButtonUp(0)) mouseHeld = false;
        if (dragLockTimer > 0f)
        {
            dragLockTimer -= Time.deltaTime;
            animator.SetBool(isDraggingParam, true);
        }
        else if (!mouseHeld && isDragging) SetDragging(false);

        // Lógica de Idle
        idleTimer += Time.deltaTime;
        if (idleTimer > IDLE_SWITCH_TIME)
        {
            idleTimer = 0f;
            int next = (idleState + 1) % totalIdleAnimations;
            if (next == 0) animator.SetFloat(idleIndexParam, 0);
            else
            {
                if (idleTransitionCoroutine != null) StopCoroutine(idleTransitionCoroutine);
                idleTransitionCoroutine = StartCoroutine(SmoothIdleTransition(next));
            }
            idleState = next;
        }
        UpdateIdleStatus();
    }
    
    // --- O resto do script permanece muito similar, com pequenas limpezas ---

    IEnumerator CheckSoundContinuously()
    {
        var wait = new WaitForSeconds(2f);
        while (true)
        {
            CheckForSound();
            yield return wait;
        }
    }

    void CheckForSound()
    {
        if (animator == null || MenuActions.IsMovementBlocked() || !enableDancing || defaultDevice == null || isDragging)
        {
            if (isDancing) SetDancing(false);
            return;
        }
        
        bool soundIsPlaying = IsValidAppPlaying();
        if (soundIsPlaying && !isDancing)
        {
            StartDancing();
        }
        else if (!soundIsPlaying && isDancing)
        {
            SetDancing(false);
        }
    }

    void StartDancing()
    {
        if (animator == null) return;
        currentDanceIndex = UnityEngine.Random.Range(0, DANCE_CLIP_COUNT);
        animator.SetFloat(danceIndexParam, currentDanceIndex);
        SetDancing(true);
    }

    void SetDancing(bool value)
    {
        if (animator == null) return;
        isDancing = value;
        animator.SetBool(isDancingParam, value);
        if (dynamicDanceSync != null)
        {
            dynamicDanceSync.SetDanceState(isDancing, currentDanceIndex);
        }
    }
    
    void SetDragging(bool value)
    {
        if (animator == null) return;
        isDragging = value;
        animator.SetBool(isDraggingParam, value);
    }

    void UpdateAllStates()
    {
        if(animator == null) return;
        SetDancing(isDancing);
        SetDragging(isDragging);
        UpdateIdleStatus();
    }

    void UpdateIdleStatus()
    {
        if (animator == null) return;
        bool inIdle = animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
        if (isIdle != inIdle)
        {
            isIdle = inIdle;
            animator.SetBool(isIdleParam, isIdle);
        }
    }

    bool IsValidAppPlaying()
    {
        // A lógica de verificação de som permanece a mesma
        if (Time.time - lastSoundCheckTime < 2f) return isDancing;
        lastSoundCheckTime = Time.time;
        try
        {
            var sessions = defaultDevice.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive && s.AudioMeterInformation.MasterPeakValue > SOUND_THRESHOLD)
                {
                    uint pid = s.GetProcessID;
                    if (pid == 0) continue;
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        string pname = p.ProcessName;
                        if (string.IsNullOrEmpty(pname)) continue;
                        foreach (string app in allowedApps)
                        {
                            if (pname.Equals(app, StringComparison.OrdinalIgnoreCase)) return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // Silenciosamente ignora erros aqui para não poluir o console
        }
        return false;
    }
    
    IEnumerator SmoothIdleTransition(int newIdle)
    {
        if (animator == null) yield break;
        float elapsed = 0f, start = animator.GetFloat(idleIndexParam);
        while (elapsed < IDLE_TRANSITION_TIME)
        {
            elapsed += Time.deltaTime;
            animator.SetFloat(idleIndexParam, Mathf.Lerp(start, newIdle, elapsed / IDLE_TRANSITION_TIME));
            yield return null;
        }
        animator.SetFloat(idleIndexParam, newIdle);
    }

    void OnDisable() => CleanupAudioResources();
    void OnDestroy() => CleanupAudioResources();
    void OnApplicationQuit() => CleanupAudioResources();

    void CleanupAudioResources()
    {
        if (soundCheckCoroutine != null) { StopCoroutine(soundCheckCoroutine); soundCheckCoroutine = null; }
        if (idleTransitionCoroutine != null) { StopCoroutine(idleTransitionCoroutine); idleTransitionCoroutine = null; }
        defaultDevice?.Dispose(); defaultDevice = null;
        enumerator?.Dispose(); enumerator = null;
    }
}