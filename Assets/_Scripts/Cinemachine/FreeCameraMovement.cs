using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Rotate")]
    [SerializeField] private float rotateSensitivity = 0.15f; 
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY = false;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;

        pitch = e.x;
        if (pitch > 180f) pitch -= 360f;
    }

    private void Update()
    {
        if (InputManager.Instance == null) return;

        // Solo rota si RMB está presionado
        if (!InputManager.Instance.IsCameraRotationHold())
            return;

        Vector2 delta = InputManager.Instance.GetCameraRotationDelta();

        float dx = delta.x * rotateSensitivity;
        float dy = delta.y * rotateSensitivity * (invertY ? 1f : -1f);

        yaw += dx;
        pitch += dy;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
