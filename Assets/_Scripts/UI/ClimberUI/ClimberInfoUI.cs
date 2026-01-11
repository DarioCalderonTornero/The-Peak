using System;
using UnityEngine;

public class ClimberInfoUI : MonoBehaviour
{
    private bool isShowClimberInfo = false;

    private void Start()
    {
        gameObject.SetActive(false);

        InputManager.Instance.OnShowClimberInfo += InputManager_OnShowClimberInfo;
    }

    private void InputManager_OnShowClimberInfo(object sender, System.EventArgs e)
    {
        ToggleShowClimberInfo();
    }

    private void ToggleShowClimberInfo()
    {
        if (!isShowClimberInfo)
        {
            ShowClimberInfo();
        }

        else if (isShowClimberInfo)
        {
            HideClimberInfo();
        }

        isShowClimberInfo = !isShowClimberInfo;
    }

    private void ShowClimberInfo()
    {
        gameObject.SetActive(true);
    }
    private void HideClimberInfo()
    {
        gameObject.SetActive(false);
    }

}
