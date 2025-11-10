using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Screen Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject victoryScreenPanel;
    [SerializeField] private GameObject gameOverScreenPanel;

    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private TextMeshProUGUI turnNumberText;
    [SerializeField] private TextMeshProUGUI turnTimerText;
    [SerializeField] private Image turnTimerFillBar;

    [Header("Turn Indicators")]
    [SerializeField] private GameObject playerTurnIndicator;
    [SerializeField] private GameObject climberTurnIndicator;

    [Header("Card UI References")]
    [SerializeField] private CardInventoryUI cardInventoryUI;
    [SerializeField] private CardSlotsUI cardSlotsUI;

    [Header("Notification System")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float notificationDuration = 3f;

    [Header("GameOver/Victory")]
    [SerializeField] private TextMeshProUGUI gameOverReasonText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

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
        InitializeUI();
        SubscribeToManagers();
        SetupButtons();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManagers();
    }

    private void InitializeUI()
    {
        HideAllScreens();
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private void SubscribeToManagers()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart += HandleClimberTurnStart;
            TurnManager.Instance.OnTurnNumberChanged += HandleTurnNumberChanged;
            TurnManager.Instance.OnClimberTurnTick += HandleClimberTurnTick;
        }

        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged += HandlePointsChanged;
        }
    }

    private void UnsubscribeFromManagers()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart -= HandleClimberTurnStart;
            TurnManager.Instance.OnTurnNumberChanged -= HandleTurnNumberChanged;
            TurnManager.Instance.OnClimberTurnTick -= HandleClimberTurnTick;
        }

        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged -= HandlePointsChanged;
        }
    }

    private void SetupButtons()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(() => GameManager.Instance?.RestartGame());
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(() => GameManager.Instance?.ReturnToMainMenu());
        
        if (quitButton != null)
            quitButton.onClick.AddListener(() => GameManager.Instance?.QuitGame());
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                ShowMainMenu();
                break;
            case GameManager.GameState.Playing:
                ShowGameUI();
                break;
            case GameManager.GameState.Paused:
                ShowPauseMenu();
                break;
            case GameManager.GameState.Victory:
                ShowVictoryScreen();
                break;
            case GameManager.GameState.GameOver:
                ShowGameOverScreen();
                break;
        }
    }

    public void ShowMainMenu()
    {
        HideAllScreens();
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }

    public void ShowGameUI()
    {
        HideAllScreens();
        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);
    }

    public void ShowPauseMenu()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
    }

    public void HidePauseMenu()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    public void ShowVictoryScreen()
    {
        if (victoryScreenPanel != null)
            victoryScreenPanel.SetActive(true);
    }

    public void ShowGameOverScreen(string reason = "")
    {
        if (gameOverScreenPanel != null)
        {
            gameOverScreenPanel.SetActive(true);
            if (gameOverReasonText != null && !string.IsNullOrEmpty(reason))
                gameOverReasonText.text = reason;
        }
    }

    private void HideAllScreens()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (victoryScreenPanel != null) victoryScreenPanel.SetActive(false);
        if (gameOverScreenPanel != null) gameOverScreenPanel.SetActive(false);
    }

    private void HandlePlayerTurnStart()
    {
        if (playerTurnIndicator != null)
            playerTurnIndicator.SetActive(true);
        if (climberTurnIndicator != null)
            climberTurnIndicator.SetActive(false);
        
        if (turnTimerText != null)
            turnTimerText.gameObject.SetActive(false);
        if (turnTimerFillBar != null)
            turnTimerFillBar.gameObject.SetActive(false);
    }

    private void HandleClimberTurnStart()
    {
        if (playerTurnIndicator != null)
            playerTurnIndicator.SetActive(false);
        if (climberTurnIndicator != null)
            climberTurnIndicator.SetActive(true);
        
        if (turnTimerText != null)
            turnTimerText.gameObject.SetActive(true);
        if (turnTimerFillBar != null)
            turnTimerFillBar.gameObject.SetActive(true);
    }

    private void HandleTurnNumberChanged(int turnNumber)
    {
        if (turnNumberText != null)
            turnNumberText.text = $"Turno {turnNumber}";
    }

    private void HandleClimberTurnTick(float progress)
    {
        if (TurnManager.Instance != null)
        {
            float timeRemaining = TurnManager.Instance.ClimberTurnTimeRemaining;
            if (turnTimerText != null)
                turnTimerText.text = $"{timeRemaining:F1}s";
            
            if (turnTimerFillBar != null)
                turnTimerFillBar.fillAmount = progress;
        }
    }

    private void HandlePointsChanged(int points)
    {
        if (pointsText != null)
            pointsText.text = $"Puntos: {points}";
    }

    public void ShowCardInventory()
    {
        if (cardInventoryUI != null)
            cardInventoryUI.ShowInventory();
    }

    public void HideCardInventory()
    {
        if (cardInventoryUI != null)
            cardInventoryUI.HideInventory();
    }

    public void ShowCardSlots()
    {
        if (cardSlotsUI != null)
            cardSlotsUI.ShowSlotContainer();
    }

    public void HideCardSlots()
    {
        if (cardSlotsUI != null)
            cardSlotsUI.HideSlotContainer();
    }

    public void ShowNotification(string message)
    {
        if (notificationPanel != null && notificationText != null)
        {
            notificationText.text = message;
            notificationPanel.SetActive(true);
            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), notificationDuration);
        }
    }

    private void HideNotification()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }
}
