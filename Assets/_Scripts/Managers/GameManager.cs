using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnPaused;

    public enum GameState
    {
        Initializing,
        Playing,
        GamePause,
        GameOver
    }

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
    }

    private void Start()
    {
        if (autoStartGame)
            StartGame();

        // Nos suscribimos al input de pausa
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

    private void AutoAssignManagers()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (spawnManager == null) spawnManager = FindFirstObjectByType<SpawnManager>();
        if (resourceManager == null) resourceManager = FindFirstObjectByType<PointsManager>();
    }

    public void StartGame()
    {
        if (turnManager == null)
        {
            Debug.LogError("[GameManager] TurnManager no encontrado en escena.");
            return;
        }

        SetState(GameState.Playing);

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

    private void SetState(GameState newState)
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
        gamePaused = true;
        OnGamePaused?.Invoke(this, EventArgs.Empty);
        Time.timeScale = 0f;
        SetState(GameState.GamePause);
    }

    public void UnPauseGame()
    {
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
