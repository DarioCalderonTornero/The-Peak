using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Initializing,
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;

    public GameState CurrentState
    {
        get => currentState;
        private set
        {
            if (currentState == value) return;
            GameState previousState = currentState;
            currentState = value;
            OnStateChanged?.Invoke(previousState, currentState);
            if (logStateChanges)
                Debug.Log($"[GameManager] State: {previousState} → {currentState}");
        }
    }

    public event Action<GameState, GameState> OnStateChanged;

    [Header("Manager References")]
    public TurnManager turnManager;
    public SpawnManager spawnManager;
    public DefensePlacementManager defenseManager;
    public PointsManager resourceManager;

    [Header("Core Systems")]
    public MountainPathfinder pathfinder;
    public MeshSlopeScannerSimple scanner;

    [Header("UI References")]
    public CardInventoryUI cardInventoryUI;
    public CardSlotsUI cardSlotsUI;
    public TurnsUI turnsUI;

    [Header("Configuration")]
    [SerializeField] private bool logStateChanges = true;
    [SerializeField] private bool autoStartGame = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeGame();
    }

    private void InitializeGame()
    {
        if (logStateChanges)
            Debug.Log("[GameManager] Initializing game systems...");
        AutoAssignManagers();
        InitializeCoreLevel();
        CurrentState = GameState.MainMenu;
    }

    private void Start()
    {
        if (autoStartGame)
            StartGame();
    }

    public void StartGame()
    {
        if (CurrentState == GameState.Playing)
        {
            Debug.LogWarning("[GameManager] Game already started!");
            return;
        }
        CurrentState = GameState.Playing;
        if (turnManager != null)
            turnManager.StartGame();
        if (resourceManager != null)
            resourceManager.AddPoints(0);
        if (logStateChanges)
            Debug.Log("[GameManager] Game started!");
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Paused;
        Time.timeScale = 0f;
        Debug.Log("[GameManager] Game paused");
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;
        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        Debug.Log("[GameManager] Game resumed");
    }

    public void Victory()
    {
        if (CurrentState == GameState.Victory || CurrentState == GameState.GameOver)
            return;
        CurrentState = GameState.Victory;
        Time.timeScale = 0f;
        Debug.Log("[GameManager] VICTORY!");
    }

    public void GameOver(string reason = "")
    {
        if (CurrentState == GameState.Victory || CurrentState == GameState.GameOver)
            return;
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        Debug.Log($"[GameManager] GAME OVER: {reason}");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        if (turnManager != null)
            turnManager.ResetGame();
        if (defenseManager != null)
            defenseManager.ResetAll();
        if (spawnManager != null)
            spawnManager.ResetSpawner();
        CurrentState = GameState.Playing;
        if (logStateChanges)
            Debug.Log("[GameManager] Game restarted!");
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        CurrentState = GameState.MainMenu;
    }

    public void QuitGame()
    {
        if (logStateChanges)
            Debug.Log("[GameManager] Quitting game...");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private void AutoAssignManagers()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<DefensePlacementManager>();
        if (resourceManager == null)
            resourceManager = FindFirstObjectByType<PointsManager>();
        if (spawnManager == null)
            spawnManager = FindFirstObjectByType<SpawnManager>();
        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<MountainPathfinder>();
        if (scanner == null)
            scanner = FindFirstObjectByType<MeshSlopeScannerSimple>();
        if (cardInventoryUI == null)
            cardInventoryUI = FindFirstObjectByType<CardInventoryUI>();
        if (cardSlotsUI == null)
            cardSlotsUI = FindFirstObjectByType<CardSlotsUI>();
        if (turnsUI == null)
            turnsUI = FindFirstObjectByType<TurnsUI>();
    }

    private void InitializeCoreLevel()
    {
        GameObject mountain = GameObject.FindWithTag("Mountain");
        if (mountain == null)
        {
            Debug.LogWarning("[GameManager] No se encontró objeto con tag 'Mountain'");
            return;
        }
        MeshFilter mf = mountain.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("[GameManager] Mountain no tiene MeshFilter válido");
            return;
        }
        if (scanner != null)
        {
            scanner.ScanMesh(mf);
            if (logStateChanges)
                Debug.Log($"[GameManager] Mountain scanned. Triangles: {scanner.triangles?.Count}");
        }
        if (pathfinder != null)
        {
            if (pathfinder.scanner == null) pathfinder.scanner = scanner;
            if (pathfinder.mountainMeshFilter == null) pathfinder.mountainMeshFilter = mf;
            bool pathOk = pathfinder.BuildGraphAndSolveAuto();
            if (pathOk && logStateChanges)
                Debug.Log("[GameManager] Pathfinding initialized successfully");
        }
    }

    public bool IsGameActive()
    {
        return CurrentState == GameState.Playing || CurrentState == GameState.Paused;
    }

    public bool IsGameEnded()
    {
        return CurrentState == GameState.GameOver || CurrentState == GameState.Victory;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
