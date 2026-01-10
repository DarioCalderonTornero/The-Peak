using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PlayerInputActions inputActions;

    // Player Inputs
    public event EventHandler OnGamePauseInput;

    /// <summary>
    /// TAP (click corto): mostrar ruta / seleccionar.
    /// </summary>
    public event EventHandler OnClimberClickRoute;

    /// <summary>
    /// HOLD (click mantenido): inspección (cámara + stats).
    /// </summary>
    public event EventHandler OnClickCameraClimber;

    // UI Inputs
    public event EventHandler OnRotateCardInput;
    public event EventHandler OnHideStaminaUI;

    // Camera Inputs
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

        inputActions = new PlayerInputActions();

        // Habilitamos todos los maps como antes
        inputActions.Player.Enable();
        inputActions.UI.Enable();
        inputActions.Camera.Enable();
    }

    private void Start()
    {
        // Player
        inputActions.Player.GamePause.performed += GamePause_performed;

        // IMPORTANTE:
        // Reemplaza "ClickClimber" por el nombre exacto de tu NUEVA acción única
        // que tiene en el binding las interacciones Tap + Hold.
        inputActions.Player.ClickClimberRoute.performed += ClickClimber_performed;

        // UI
        inputActions.UI.RotateCard.performed += RotateCard_performed;
        inputActions.UI.HideClimberStaminaUI.performed += HideClimberStaminaUI_performed;

        // Camera (presets y cámara libre siguen igual)
        inputActions.Camera.FrontalView.performed += FrontalView_performed;
        inputActions.Camera.RightView.performed += RightView_performed;
        inputActions.Camera.BackView.performed += BackView_performed;
        inputActions.Camera.LeftView.performed += LeftView_performed;
        inputActions.Camera.TopView.performed += TopView_performed;
    }

    // ----------------- NUEVO: TAP vs HOLD en una sola action -----------------

    private void ClickClimber_performed(InputAction.CallbackContext ctx)
    {
        // HOLD -> inspección
        if (ctx.interaction is HoldInteraction)
        {
            OnClickCameraClimber?.Invoke(this, EventArgs.Empty);
            return;
        }

        // TAP (por defecto) -> ruta
        if (ctx.interaction is TapInteraction || ctx.interaction == null)
        {
            OnClimberClickRoute?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Fallback seguro (por si añades más interacciones en el futuro)
        OnClimberClickRoute?.Invoke(this, EventArgs.Empty);
    }

    // ----------------- CAMERA GETTERS (igual que antes) -----------------

    public Vector2 GetCameraPanMovement() => inputActions.Camera.CameraPanMove.ReadValue<Vector2>();
    public Vector2 GetCameraRotationDelta() => inputActions.Camera.CameraRotateDelta.ReadValue<Vector2>();
    public Vector2 GetCameraZoom() => inputActions.Camera.CameraZoom.ReadValue<Vector2>();

    public bool IsCameraPanHold() => inputActions.Camera.CameraPanHold.IsPressed();
    public bool IsCameraRotationHold() => inputActions.Camera.CameraRotateHold.IsPressed();
    public bool isCameraPanSpeedMultiplierHold() => inputActions.Camera.CameraPanSpeedMultiplier.IsPressed();

    // ----------------- PRESSETS CAMERA (igual que antes) -----------------

    private void TopView_performed(InputAction.CallbackContext obj) => OnTopView?.Invoke(this, EventArgs.Empty);
    private void LeftView_performed(InputAction.CallbackContext obj) => OnLeftView?.Invoke(this, EventArgs.Empty);
    private void BackView_performed(InputAction.CallbackContext obj) => OnBackView?.Invoke(this, EventArgs.Empty);
    private void RightView_performed(InputAction.CallbackContext obj) => OnRightView?.Invoke(this, EventArgs.Empty);
    private void FrontalView_performed(InputAction.CallbackContext obj) => OnFrontalView?.Invoke(this, EventArgs.Empty);

    // ----------------- UI -----------------

    private void HideClimberStaminaUI_performed(InputAction.CallbackContext obj) => OnHideStaminaUI?.Invoke(this, EventArgs.Empty);
    private void RotateCard_performed(InputAction.CallbackContext obj) => OnRotateCardInput?.Invoke(this, EventArgs.Empty);

    // ----------------- PLAYER -----------------

    private void GamePause_performed(InputAction.CallbackContext obj) => OnGamePauseInput?.Invoke(this, EventArgs.Empty);

    private void OnDestroy()
    {
        if (inputActions == null) return;
        inputActions.Player.Disable();
        inputActions.UI.Disable();
        inputActions.Camera.Disable();
    }
}
