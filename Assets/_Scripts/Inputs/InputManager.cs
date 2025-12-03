using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    PlayerInputActions inputActions;

    public event EventHandler OnGamePause;

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
    }

    private void GamePause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        Debug.Log("P");
        OnGamePause?.Invoke(this, EventArgs.Empty);
    }
}
