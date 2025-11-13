using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Initializing,
        Playing,
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AutoAssignManagers();

        SetState(GameState.Initializing);
    }

    private void Start()
    {
        if (autoStartGame)
            StartGame();
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
    }
}
