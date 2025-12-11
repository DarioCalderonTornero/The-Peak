using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    PlayerInputActions inputActions;

    //Player Inputs
    public event EventHandler OnGamePauseInput;
    public event EventHandler OnResetCameraInput;
    //UI Inputs
    public event EventHandler OnRotateCardInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
        inputActions.UI.Enable();
    }

    private void Start()
    {
        inputActions.Player.GamePause.performed += GamePause_performed;
        inputActions.Player.ResetCamera.performed += ResetCamera_performed;
        inputActions.UI.RotateCard.performed += RotateCard_performed;
    }

    private void RotateCard_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRotateCardInput?.Invoke(this, EventArgs.Empty);
    }

    private void ResetCamera_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnResetCameraInput?.Invoke(this, EventArgs.Empty);  
    }

    private void GamePause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnGamePauseInput?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        inputActions.Player.Disable();
        inputActions.UI.Disable();
    }
}
