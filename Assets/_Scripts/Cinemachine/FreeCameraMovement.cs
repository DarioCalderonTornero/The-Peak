using System;
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private float panSpeed = 0.2f;
    [SerializeField] private bool invertPanY = false;
    [SerializeField] private bool panVerticalUsesWorldUp = true;
    [SerializeField] private float panVerticalMultiplier = 1f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 80f;

    private Vector3 zoomOrigin;

    [Header("Rotate")]
    [SerializeField] private float rotateSensitivity = 0.15f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY = false;

    private float yaw;
    private float pitch;

    [Header("View Presets (Transforms)")]
    [SerializeField] private Transform presetFront;
    [SerializeField] private Transform presetRight;
    [SerializeField] private Transform presetBack;
    [SerializeField] private Transform presetLeft;
    [SerializeField] private Transform presetTop;

    [Header("Snap To Preset")]
    [SerializeField] private float snapDuration = 0.35f;

    private bool isSnapping;
    private float snapT;
    private Vector3 snapStartPos, snapTargetPos;
    private Quaternion snapStartRot, snapTargetRot;

    private void Start()
    {
        zoomOrigin = transform.position;

        Vector3 currentEuler = transform.eulerAngles;
        yaw = currentEuler.y;

        pitch = currentEuler.x;
        if (pitch > 180f) pitch -= 360f; 

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnFrontalView += InputManager_OnFrontalView;
            InputManager.Instance.OnRightView += InputManager_OnRightView;
            InputManager.Instance.OnBackView += InputManager_OnBackView;
            InputManager.Instance.OnLeftView += InputManager_OnLeftView;
            InputManager.Instance.OnTopView += InputManager_OnTopView;
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnFrontalView -= InputManager_OnFrontalView;
        InputManager.Instance.OnRightView -= InputManager_OnRightView;
        InputManager.Instance.OnBackView -= InputManager_OnBackView;
        InputManager.Instance.OnLeftView -= InputManager_OnLeftView;
        InputManager.Instance.OnTopView -= InputManager_OnTopView;
    }

    private void InputManager_OnFrontalView(object sender, EventArgs e) => StartSnap(presetFront);
    private void InputManager_OnRightView(object sender, EventArgs e) => StartSnap(presetRight);
    private void InputManager_OnBackView(object sender, EventArgs e) => StartSnap(presetBack);
    private void InputManager_OnLeftView(object sender, EventArgs e) => StartSnap(presetLeft);
    private void InputManager_OnTopView(object sender, EventArgs e) => StartSnap(presetTop);

    private void StartSnap(Transform target)
    {
        if (target == null) return;

        isSnapping = true;
        snapT = 0f;

        snapStartPos = transform.position;
        snapStartRot = transform.rotation;

        snapTargetPos = target.position;
        snapTargetRot = target.rotation;
    }

    private void Update()
    {
        if (InputManager.Instance == null) return;

        if (isSnapping)
        {
            snapT += Time.deltaTime / Mathf.Max(0.0001f, snapDuration);
            float t = Mathf.Clamp01(snapT);

            transform.position = Vector3.Lerp(snapStartPos, snapTargetPos, t);
            transform.rotation = Quaternion.Slerp(snapStartRot, snapTargetRot, t);

            if (t >= 1f)
            {
                isSnapping = false;

                Vector3 euler = transform.eulerAngles;
                yaw = euler.y;

                pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;

                zoomOrigin = transform.position;
            }

            return;
        }

        // --- ROTATE ---
        if (InputManager.Instance.IsCameraRotationHold())
        {
            Vector2 delta = InputManager.Instance.GetCameraRotationDelta();

            float dx = delta.x * rotateSensitivity;
            float dy = delta.y * rotateSensitivity * (invertY ? 1f : -1f);

            yaw += dx;
            pitch += dy;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // --- ZOOM DOLLY ---
        Vector2 zoomDelta = InputManager.Instance.GetCameraZoom();

        if (Mathf.Abs(zoomDelta.y) >= 0.01f)
        {
            Vector3 move = transform.forward * zoomDelta.y * zoomSpeed;
            Vector3 candidatePos = transform.position + move;

            float dist = Vector3.Distance(candidatePos, zoomOrigin);

            if (dist >= minZoom && dist <= maxZoom)
            {
                transform.position = candidatePos;
            }
        }

        // --- PAN ---
        if (InputManager.Instance.IsCameraPanHold())
        {
            Vector2 delta = InputManager.Instance.GetCameraPanMovement();

            Vector3 right = transform.right;
            Vector3 forwardOnGround = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            float x = -delta.x * panSpeed;
            float z = -delta.y * panSpeed;

            float ySign = invertPanY ? 1f : -1f;
            float y = delta.y * panSpeed * ySign * panVerticalMultiplier;

            Vector3 upAxis = panVerticalUsesWorldUp ? Vector3.up : transform.up;

            Vector3 move = (right * x) + (forwardOnGround * z) + (upAxis * y);

            transform.position += move;

            zoomOrigin += move;
        }
    }
}
