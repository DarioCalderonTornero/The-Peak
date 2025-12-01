using System;
using TMPro;
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
    [SerializeField] private bool logTurnChanges = true;

    [Header("UI References")]
    [SerializeField] private Button nextTurnButton;
    [SerializeField] private Button accelarateTimeButton;
    [SerializeField] private TextMeshProUGUI accelerateTimeText;

    public event Action<TurnState, TurnState> OnTurnStateChanged;
    public event Action OnPlayerTurnStart;
    public event Action OnPlayerTurnEnd;
    public event Action OnClimberTurnStart;
    public event Action OnClimberTurnEnd;

    private SpawnManager spawnManager;
    private DefensePlacementManager defenseManager;

    // ----- Time scale -----
    // Ciclo: X1 -> X2 -> X4 -> X1
    [SerializeField] private int[] timeMultipliers = { 1, 2, 4 };
    private int currentTimeMultiplierIndex = 0;

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

        if (accelarateTimeButton != null)
        {
            accelarateTimeButton.onClick.AddListener(AccelerateTime);
        }

        SetTimeScaleIndex(0);

        if (accelarateTimeButton != null)
        {
            accelarateTimeButton.gameObject.SetActive(false);
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
        currentTurnNumber = 0;
        StartPlayerTurn();
        if (logTurnChanges)
            Debug.Log("[TurnManager] Game started, beginning Player Turn");
    }

    public void ResetGame()
    {
        currentTurnNumber = 0;
        CurrentTurnState = TurnState.Idle;
        ResetTime();
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

        // Turno del jugador: botón "end turn" activo
        if (nextTurnButton != null)
        {
            nextTurnButton.interactable = true;
        }

        // Reset Time al entrar en turno de jugador
        ResetTime();

        // En turno del jugador no se puede acelerar el tiempo
        if (accelarateTimeButton != null)
        {
            accelarateTimeButton.gameObject.SetActive(false);
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

        // En turno de escaladores sí se puede acelerar el tiempo
        if (accelarateTimeButton != null)
        {
            accelarateTimeButton.gameObject.SetActive(true);
        }

        // Dejamos el turno de escaladores empezando en X1
        SetTimeScaleIndex(0);

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

        // Al terminar turno escalador, volvemos a X1 por seguridad
        ResetTime();

        OnClimberTurnEnd?.Invoke();
        StartPlayerTurn();
    }

    private void AccelerateTime()
    {
        // Solo tiene sentido acelerar en turno de escaladores
        if (CurrentTurnState != TurnState.ClimberTurn)
            return;

        // Avanzar al siguiente índice en el array [1,2,4]
        currentTimeMultiplierIndex++;
        if (currentTimeMultiplierIndex >= timeMultipliers.Length)
            currentTimeMultiplierIndex = 0;

        SetTimeScaleIndex(currentTimeMultiplierIndex);
    }

    private void SetTimeScaleIndex(int index)
    {
        if (timeMultipliers == null || timeMultipliers.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, timeMultipliers.Length - 1);
        currentTimeMultiplierIndex = index;

        int mult = timeMultipliers[currentTimeMultiplierIndex];
        Time.timeScale = mult;

        if (accelerateTimeText != null)
        {
            accelerateTimeText.text = $"X{mult}";
        }
    }

    private void ResetTime()
    {
        SetTimeScaleIndex(0);
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
