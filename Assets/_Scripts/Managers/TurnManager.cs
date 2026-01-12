using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private PlayerTurnsUI playerTurnsUI;

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

    public int recordTurnNumber;

    private string RECORD_KEY = "RECORD_TURN_NUMBER";

    [Header("Configuration")]
    [SerializeField] private bool logTurnChanges = true;

    [Header("UI References")]
    [SerializeField] private Button nextTurnButton;
    [SerializeField] private Button accelarateTimeButtonX1;
    [SerializeField] private Button accelarateTimeButtonX2;
    [SerializeField] private Button accelarateTimeButtonX4;
    [SerializeField] private TextMeshProUGUI accelerateTimeText;

    public event Action<TurnState, TurnState> OnTurnStateChanged;
    public event Action OnPlayerTurnStart;
    public event Action OnPlayerTurnEnd;
    public event Action OnClimberTurnStart;
    public event Action OnClimberTurnEnd;

    private SpawnManager spawnManager;
    private DefensePlacementManager defenseManager;


    private bool pointsAddedThisTurn = false;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        spawnManager = SpawnManager.Instance;
        defenseManager = DefensePlacementManager.Instance;

        //recordTurnNumber = 0;
        recordTurnNumber = PlayerPrefs.GetInt(RECORD_KEY, 0);

        if (nextTurnButton != null)
        {
            nextTurnButton.onClick.AddListener(EndPlayerTurn);
        }

        if (accelarateTimeButtonX1 != null)
        {
            accelarateTimeButtonX1.onClick.AddListener(() =>
            {
                Time.timeScale = 1.0f;
            });
        }

        if (accelarateTimeButtonX2 != null)
        {
            accelarateTimeButtonX2.onClick.AddListener(() =>
            {
                Time.timeScale = 2.0f;
            });
        }

        if (accelarateTimeButtonX4 != null)
        {
            accelarateTimeButtonX4.onClick.AddListener(() =>
            {
                Time.timeScale = 4.0f;
            });
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
                //CurrentTurnState = TurnState.GameOver;
                break;
        }
    }

    public void StartGame()
    {
        //currentTurnNumber = 0;
        StartPlayerTurn();
        if (logTurnChanges)
            Debug.Log("[TurnManager] Game started, beginning Player Turn");
    }

    public void ResetGame()
    {
        currentTurnNumber = 0;
        CurrentTurnState = TurnState.Idle;

        if (logTurnChanges)
            Debug.Log("[TurnManager] Turn system reset");
    }

    private void StartPlayerTurn()
    {
        pointsAddedThisTurn = false; 
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

        if (logTurnChanges)
        {
            Debug.Log("[TurnManager] Climber turn started.");
        }
    }

    private void EndClimberTurn()
    {
        if (CurrentTurnState != TurnState.ClimberTurn)
            return;

        if (!pointsAddedThisTurn)
        {
            PointsManager.Instance.AddPoints(5);
            pointsAddedThisTurn = true;
        }

        if (currentTurnNumber > recordTurnNumber)
        {
            recordTurnNumber = currentTurnNumber;
            PlayerPrefs.SetInt(RECORD_KEY, recordTurnNumber);
            PlayerPrefs.Save();
        }

        // Actualizamos el contador de turnos en el UI
        if (playerTurnsUI != null)
        {
            playerTurnsUI.currentTurns.text = (currentTurnNumber).ToString(); 
            ShowFinalStats.Instance.totalRounds.text = "TOTAL ROUNDS: " + (currentTurnNumber).ToString();  
            ShowFinalStats.Instance.recordTotalRounds.text = "MAX ROUNDS: " + recordTurnNumber.ToString();
        }

        OnClimberTurnEnd?.Invoke();
        StartPlayerTurn();
    }

    private void Update()
    {
        if (CurrentTurnState == TurnState.ClimberTurn)
        {
            UpdateClimberTurn();
        }
    }

    private void UpdateClimberTurn()
    {
        // 1) Spawnear escaladores mientras no se llegue al máximo
        if (spawnManager != null && !spawnManager.isMaxCount)
        {
            spawnManager.SpawnClimbers();
        }

        // 2) Mirar TODOS los escaladores vivos en escena
        ClimberMovement[] climbers = FindObjectsOfType<ClimberMovement>();

        bool hasClimbers = false;
        bool allDone = true;

        foreach (var climber in climbers)
        {
            if (climber == null)
                continue;

            hasClimbers = true;

            // Si alguno no ha terminado su turno, el turno completo sigue
            if (!climber.IsDoneThisTurn)
            {
                allDone = false;
                break;
            }
        }

        // 3) Si NO hay escaladores vivos ahora mismo...
        if (!hasClimbers)
        {
            // ...y el spawner TODAVÍA puede crear más, dejamos el turno abierto
            if (spawnManager != null && !spawnManager.isMaxCount)
            {
                return;
            }

            // ...y el spawner YA NO puede crear más → fin de turno de escaladores
            if (logTurnChanges)
                Debug.Log("[TurnManager] Climber turn ended: no climbers alive and spawner cannot create more.");

            EndClimberTurn();
            return;
        }

        // 4) Si hay escaladores y TODOS han terminado su turno → fin de turno
        if (allDone)
        {
            if (logTurnChanges)
                Debug.Log("[TurnManager] Climber turn ended: all climbers are done (at camp, out of stamina, or destroyed).");
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
}
