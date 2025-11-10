using System;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState
    {
        Idle,
        PlayerTurn,
        ClimberTurn,
        GameOver
    }

    [Header("Turn State")]
    [SerializeField] private TurnState currentTurnState = TurnState.Idle;
    
    public TurnState CurrentTurnState 
    { 
        get => currentTurnState;
        private set
        {
            if (currentTurnState == value) return;
            TurnState previousState = currentTurnState;
            currentTurnState = value;
            OnTurnStateChanged?.Invoke(previousState, currentTurnState);
            if (logTurnChanges)
                Debug.Log($"[TurnManager] Turn State: {previousState} -> {currentTurnState}");
        }
    }

    [Header("Turn Counter")]
    [SerializeField] private int currentTurnNumber = 0;
    public int CurrentTurnNumber => currentTurnNumber;
    public event Action<int> OnTurnNumberChanged;

    [Header("Configuration")]
    [SerializeField] private float climberTurnDuration = 10f;
    [SerializeField] private bool logTurnChanges = true;
    
    [Header("UI References")]
    [SerializeField] private Button nextTurnButton;

    private float climberTurnTimer = 0f;
    public float ClimberTurnTimeRemaining => Mathf.Max(0f, climberTurnDuration - climberTurnTimer);
    public float ClimberTurnProgress => Mathf.Clamp01(climberTurnTimer / climberTurnDuration);

    public event Action<TurnState, TurnState> OnTurnStateChanged;
    public event Action OnPlayerTurnStart;
    public event Action OnPlayerTurnEnd;
    public event Action OnClimberTurnStart;
    public event Action OnClimberTurnEnd;
    public event Action<float> OnClimberTurnTick;

    private SpawnManager spawnManager;
    private DefensePlacementManager defenseManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        spawnManager = SpawnManager.Instance;
        defenseManager = DefensePlacementManager.Instance;
        
        if (nextTurnButton != null)
        {
            nextTurnButton.onClick.AddListener(EndPlayerTurn);
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }



    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.Playing:
                StartGame();
                break;
            case GameManager.GameState.GameOver:
            case GameManager.GameState.Victory:
                CurrentTurnState = TurnState.GameOver;
                break;
        }
    }

    public void StartGame()
    {
        currentTurnNumber = 0;
        climberTurnTimer = 0f;
        StartPlayerTurn();
        if (logTurnChanges)
            Debug.Log("[TurnManager] Game started, beginning Player Turn");
    }

    public void ResetGame()
    {
        currentTurnNumber = 0;
        climberTurnTimer = 0f;
        CurrentTurnState = TurnState.Idle;
        if (logTurnChanges)
            Debug.Log("[TurnManager] Turn system reset");
    }

    private void StartPlayerTurn()
    {
        CurrentTurnState = TurnState.PlayerTurn;
        currentTurnNumber++;
        OnTurnNumberChanged?.Invoke(currentTurnNumber);
        
        if (CardSlotsUI.Instance != null)
        {
            CardSlotsUI.Instance.ShowSlotContainer();
        }
        
        OnPlayerTurnStart?.Invoke();
        
        if (nextTurnButton != null)
        {
            nextTurnButton.interactable = true;
        }
    }

    public void EndPlayerTurn()
    {
        if (CurrentTurnState != TurnState.PlayerTurn)
        {
            Debug.LogWarning("[TurnManager] Cannot end player turn - not in player turn state");
            return;
        }
        OnPlayerTurnEnd?.Invoke();
        StartClimberTurn();
    }

    private void StartClimberTurn()
    {
        CurrentTurnState = TurnState.ClimberTurn;
        climberTurnTimer = 0f;
        
        if (CardSlotsUI.Instance != null)
        {
            CardSlotsUI.Instance.HideSlotContainer();
        }
        
        if (nextTurnButton != null)
        {
            nextTurnButton.interactable = false;
        }
        
        if (spawnManager != null)
        {
            spawnManager.ResetSpawner();
        }
        
        if (defenseManager != null)
        {
            defenseManager.AdvanceTurn();
        }
        
        OnClimberTurnStart?.Invoke();
    }

    private void EndClimberTurn()
    {
        if (CurrentTurnState != TurnState.ClimberTurn)
            return;
        OnClimberTurnEnd?.Invoke();
        StartPlayerTurn();
    }

    private void Update()
    {
        //Debug.Log("Is Game Active");
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive() && CurrentTurnState != TurnState.ClimberTurn)
            return;
        //Debug.Log("Updating TurnManager");
        if (CurrentTurnState == TurnState.ClimberTurn)
        {
            //Debug.Log("Updating ClimberTurn");
            UpdateClimberTurn();
        }
    }

    private void UpdateClimberTurn()
    {
        // Contar tiempo desde el inicio del turno
        climberTurnTimer += Time.deltaTime;
        OnClimberTurnTick?.Invoke(ClimberTurnProgress);

        // Hacer que los escaladores actúen durante este tiempo
        if (spawnManager != null && !spawnManager.isMaxCount)
        {
            spawnManager.SpawnClimbers();
        }

        // Si han pasado 10 segundos (o el valor configurado), volver al jugador
        if (climberTurnTimer >= climberTurnDuration)
        {
            Debug.Log($"[TurnManager] Climber turn ended automatically after {climberTurnTimer:F1}s");
            EndClimberTurn();
        }
    }

        public bool IsPlayerTurn()
    {
        return CurrentTurnState == TurnState.PlayerTurn;
    }

    public bool IsClimberTurn()
    {
        return CurrentTurnState == TurnState.ClimberTurn;
    }

    [ContextMenu("Force Player Turn")]
    public void ForcePlayerTurn()
    {
        StartPlayerTurn();
    }

    [ContextMenu("Force Climber Turn")]
    public void ForceClimberTurn()
    {
        StartClimberTurn();
    }

    public void SetClimberTurnDuration(float duration)
    {
        climberTurnDuration = Mathf.Max(1f, duration);
        if (logTurnChanges)
            Debug.Log($"[TurnManager] Climber turn duration set to {climberTurnDuration}s");
    }

}
