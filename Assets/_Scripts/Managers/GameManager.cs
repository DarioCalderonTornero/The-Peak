// GameManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnPaused;
    public event EventHandler OnTutorial;

    public enum GameState
    {
        Initializing,
        Tutorial,
        Playing,
        GamePause,
        CinematicDeath,
        GameOver
    }

    [Serializable]
    public struct DeathInfo
    {
        public ClimberMovement climber;
        public Vector3 position;
        public DeathCause cause;
    }

    public event Action<DeathInfo> OnClimberDead;

    [Header("Death Effects Configurations")]
    [SerializeField] private List<DeathEffectConfigSO> deathEffectConfigs;

    private Dictionary<DeathCause, DeathEffectConfigSO> deathEffectDictionary;


    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState;

    public event Action<GameState, GameState> OnStateChanged;

    [Header("Required Managers")]
    public TurnManager turnManager;
    public SpawnManager spawnManager;
    public PointsManager resourceManager;

    [Header("Config")]
    public bool autoStartGame = true;
    public bool logStateChanges = true;

    [Header("UI")]
    [SerializeField] private GamePauseUI gamePauseUI;

    private bool gamePaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        AutoAssignManagers();
        SetState(GameState.Initializing);
        InitializeDeathEffectDictionary();
    }

    private void Start()
    {
        autoStartGame = false;

        if (autoStartGame)
            StartGame();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnGamePauseInput += HandleGamePauseInput;
        }
        else
        {
            Debug.LogWarning("[GameManager] InputManager.Instance es null en Start.");
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnGamePauseInput -= HandleGamePauseInput;
        }
    }

    private void InitializeDeathEffectDictionary()
    {
        deathEffectDictionary = new Dictionary<DeathCause, DeathEffectConfigSO>();

        foreach (var config in deathEffectConfigs)
        {
            deathEffectDictionary[config.deathCause] = config;
        }
    }

    public void NotifyClimberDied(DeathInfo info)
    {
        DeathEffectConfigSO deathEffectConfig = GetDeathEffectConfig(info.cause);
        ApplyDeathEffects(info.climber, deathEffectConfig);
        OnClimberDead?.Invoke(info);
    }

    private DeathEffectConfigSO GetDeathEffectConfig(DeathCause cause)
    {
        if (deathEffectDictionary.ContainsKey(cause))
        {
            return deathEffectDictionary[cause];
        }

        Debug.LogWarning($"No se encontró configuración para la causa de muerte: {cause}");
        return null;
    }

    private void ApplyDeathEffects(ClimberMovement climber, DeathEffectConfigSO config)
    {
        if (config == null)
        {
            Debug.LogWarning("[GameManager] Config de efectos de muerte es null, no se aplicarán efectos visuales o sonoros.");
            return;
        }

        Debug.Log("Funciona");

        if (config.deathAudioClip != null)
        {
            //AudioSource.PlayClipAtPoint(config.deathAudioClip, climber.transform.position);
        }

        if (climber.GetComponent<Animator>() && config.deathAnimationClip != null)
        {
            //climber.GetComponent<Animator>().Play(config.deathAnimationClip.name);
        }
    }

    private void AutoAssignManagers()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (spawnManager == null) spawnManager = FindFirstObjectByType<SpawnManager>();
        if (resourceManager == null) resourceManager = FindFirstObjectByType<PointsManager>();
    }

    public void StartTutorial()
    {
        SetState(GameState.Tutorial);

        if (currentState == GameState.Tutorial)
        {
            OnTutorial?.Invoke(this, EventArgs.Empty);
        }
    }

    public void StartGame()
    {
        if (turnManager == null)
        {
            Debug.LogError("[GameManager] TurnManager no encontrado en escena.");
            return;
        }

        SetState(GameState.Playing);

        UIManager.Instance.ShowMultiple(UICanvasType.Dario, UICanvasType.Alex);

        turnManager.StartGame();

        if (logStateChanges)
            Debug.Log("[GameManager] Game started!");
    }

    public void GameOver(string reason = "")
    {
        if (currentState == GameState.GameOver)
            return;

        SetState(GameState.GameOver);

        Debug.Log($"[GameManager] GAME OVER: {reason}");

        Time.timeScale = 0f;
    }

    public void SetState(GameState newState)
    {
        if (currentState == newState)
            return;

        var previous = currentState;
        currentState = newState;

        OnStateChanged?.Invoke(previous, currentState);

        if (logStateChanges)
            Debug.Log($"[GameManager] State: {previous} -> {currentState}");
    }

    public void PauseGame()
    {
        UIManager.Instance.ShowOnly(UICanvasType.Pause);
        gamePaused = true;
        OnGamePaused?.Invoke(this, EventArgs.Empty);
        Time.timeScale = 0f;
        SetState(GameState.GamePause);
    }

    public void UnPauseGame()
    {
        UIManager.Instance.ShowMultiple(UICanvasType.Dario, UICanvasType.Alex);
        gamePaused = false;
        Time.timeScale = 1.0f;
        OnGameUnPaused?.Invoke(this, EventArgs.Empty);
        SetState(GameState.Playing);
    }

    private void HandleGamePauseInput(object sender, EventArgs e)
    {
        if (gamePauseUI == null)
        {
            Debug.LogWarning("[GameManager] GamePauseUI no asignado en el inspector.");
            return;
        }

        if (!gamePauseUI.gameObject.activeSelf)
        {
            gamePauseUI.gameObject.SetActive(true);
        }

        gamePauseUI.TogglePauseMenu();
    }
}
