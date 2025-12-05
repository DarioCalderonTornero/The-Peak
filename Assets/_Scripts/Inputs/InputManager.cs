using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    PlayerInputActions inputActions;

    public event EventHandler OnGamePauseInput;
    public event EventHandler OnResetCameraInput;

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
    }

    private void Start()
    {
        inputActions.Player.GamePause.performed += GamePause_performed;
        inputActions.Player.ResetCamera.performed += ResetCamera_performed;
    }

    private void ResetCamera_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        Debug.Log("Space");
        OnResetCameraInput?.Invoke(this, EventArgs.Empty);  
    }

    private void GamePause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnGamePauseInput?.Invoke(this, EventArgs.Empty);
    }
}
