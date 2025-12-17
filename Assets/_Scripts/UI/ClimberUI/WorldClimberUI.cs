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
        gameOverClimberText.gameObject.SetActive(false);    

        InputManager.Instance.OnHideStaminaUI += InputManager_OnHideStaminaUI;
        GameOverManager.Instance.OnGameOver += GameOverManager_OnGameOver;
    }

    private void GameOverManager_OnGameOver(object sender, EventArgs e)
    {
        if (backgroundImage != null && currentStaminaImage != null)
        {
            backgroundImage.gameObject.SetActive(false);
            currentStaminaImage.gameObject.SetActive(false);
        }
        
        gameOverClimberText.gameObject.SetActive(true);
    }

    private void InputManager_OnHideStaminaUI(object sender, System.EventArgs e)
    {
        ToggleStaminaUI();
    }

    private void ToggleStaminaUI()
    {
        if (!isStaminaHide)
        {
            Show();
        }

        else
        {
            Hide();
        }

        isStaminaHide = !isStaminaHide;
    }

    private void Show()
    {
        currentStaminaImage.gameObject.SetActive(true);
        backgroundImage.gameObject.SetActive(true);
    }

    private void Hide()
    {
        currentStaminaImage.gameObject.SetActive(false);
        backgroundImage.gameObject.SetActive(false);
    }


    private void Update()
    {
        float getStaminaNormalized = climberMovement.GetCurrentStamina() / climberMovement.GetMaxStamina();
        currentStaminaImage.fillAmount = getStaminaNormalized;
    }
}
