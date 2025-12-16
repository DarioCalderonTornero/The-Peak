using System;
using UnityEngine;
using UnityEngine.UI;

public class ClimberStaminaUI : MonoBehaviour
{
    [SerializeField] private ClimberMovement climberMovement;

    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image currentStaminaImage;

    private bool isStaminaHide = true;

    private void Start()
    {
        InputManager.Instance.OnHideStaminaUI += InputManager_OnHideStaminaUI;
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
