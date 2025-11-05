using Unity.Cinemachine;
using UnityEngine;

public class CameraOrbitRightClick : MonoBehaviour
{
    private CinemachineInputAxisController axisController;

    void Awake()
    {
        axisController = GetComponent<CinemachineInputAxisController>();
    }

    void Update()
    {
        bool rightClickHeld = Input.GetMouseButton(1);

        axisController.enabled = rightClickHeld;
    }
}
