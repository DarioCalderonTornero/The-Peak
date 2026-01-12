using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldClimberUI : MonoBehaviour
{
    [SerializeField] private ClimberMovement climberMovement;

    [Header("Stamina")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image currentStaminaImage;

    [Header("GameOverClimberUI")]
    [SerializeField] private TextMeshProUGUI gameOverClimberText;

    private bool isStaminaHide = true;

    private void Start()
    {
        if (gameOverClimberText != null)
            gameOverClimberText.gameObject.SetActive(false);

        if (InputManager.Instance != null)
            InputManager.Instance.OnHideStaminaUI += InputManager_OnHideStaminaUI;

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnGameOver += GameOverManager_OnGameOver;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnHideStaminaUI -= InputManager_OnHideStaminaUI;

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnGameOver -= GameOverManager_OnGameOver;
    }

    private void GameOverManager_OnGameOver(object sender, EventArgs e)
    {
        if (backgroundImage != null) backgroundImage.gameObject.SetActive(false);
        if (currentStaminaImage != null) currentStaminaImage.gameObject.SetActive(false);

        if (gameOverClimberText != null)
            gameOverClimberText.gameObject.SetActive(true);
    }

    private void InputManager_OnHideStaminaUI(object sender, EventArgs e)
    {
        ToggleStaminaUI();
    }

    private void ToggleStaminaUI()
    {
        if (!isStaminaHide) Show();
        else Hide();

        isStaminaHide = !isStaminaHide;
    }

    private void Show()
    {
        if (currentStaminaImage == null || backgroundImage == null) return;
        currentStaminaImage.gameObject.SetActive(true);
        backgroundImage.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (currentStaminaImage == null || backgroundImage == null) return;
        currentStaminaImage.gameObject.SetActive(false);
        backgroundImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (climberMovement == null || currentStaminaImage == null) return;

        float max = climberMovement.GetMaxStamina();
        if (max <= 0f) return;

        float normalized = climberMovement.GetCurrentStamina() / max;
        currentStaminaImage.fillAmount = normalized;
    }
}
