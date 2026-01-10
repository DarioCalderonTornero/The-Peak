using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    PlayerInputActions inputActions;

    //Player Inputs
    public event EventHandler OnGamePauseInput;

    //UI Inputs
    public event EventHandler OnRotateCardInput;
    public event EventHandler OnHideStaminaUI;

    //Camera Inputs
    public event EventHandler OnFrontalView;
    public event EventHandler OnRightView;
    public event EventHandler OnBackView;
    public event EventHandler OnLeftView;
    public event EventHandler OnTopView;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        //Enable Input Actions
        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
        inputActions.UI.Enable();
        inputActions.Camera.Enable();
    }

    private void Start()
    {
        //Player Actions performed
        inputActions.Player.GamePause.performed += GamePause_performed;

        //UI Actions performed
        inputActions.UI.RotateCard.performed += RotateCard_performed;
        inputActions.UI.HideClimberStaminaUI.performed += HideClimberStaminaUI_performed;

        //Camera Actions performed
        inputActions.Camera.FrontalView.performed += FrontalView_performed;
        inputActions.Camera.RightView.performed += RightView_performed;
        inputActions.Camera.BackView.performed += BackView_performed;
        inputActions.Camera.LeftView.performed += LeftView_performed;
        inputActions.Camera.TopView.performed += TopView_performed;
    }

   

    //---CAMERA GETTERS---
    public Vector2 GetCameraPanMovement()
    {
        return inputActions.Camera.CameraPanMove.ReadValue<Vector2>();
    }

    public Vector2 GetCameraRotationDelta()
    {
        return inputActions.Camera.CameraRotateDelta.ReadValue<Vector2>();
    }

    public Vector2 GetCameraZoom()
    {
        return inputActions.Camera.CameraZoom.ReadValue<Vector2>();
    }

    public bool IsCameraPanHold()
    {
        return inputActions.Camera.CameraPanHold.IsPressed();
    }

    public bool IsCameraRotationHold()
    {
        return inputActions.Camera.CameraRotateHold.IsPressed();
    }

    public bool isCameraPanSpeedMultiplierHold()
    {
        return inputActions.Camera.CameraPanSpeedMultiplier.IsPressed();
    }

    //Get Camera Faces
    private void TopView_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnTopView?.Invoke(this, EventArgs.Empty);
    }

    private void LeftView_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnLeftView?.Invoke(this, EventArgs.Empty);
    }

    private void BackView_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnBackView?.Invoke(this, EventArgs.Empty);
    }

    private void RightView_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRightView?.Invoke(this, EventArgs.Empty);
    }

    private void FrontalView_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnFrontalView?.Invoke(this, EventArgs.Empty);
    }

    private void HideClimberStaminaUI_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnHideStaminaUI?.Invoke(this, EventArgs.Empty);
    }

    private void RotateCard_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRotateCardInput?.Invoke(this, EventArgs.Empty);
    }

    private void GamePause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnGamePauseInput?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        inputActions.Player.Disable();
        inputActions.UI.Disable();
        inputActions.Camera.Disable();
    }
}
