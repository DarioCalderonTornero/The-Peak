using Unity.Cinemachine;
using UnityEngine;

public class CameraOrbitRightClick : MonoBehaviour
{
    private CinemachineInputAxisController axisController;

    private bool canMoveCamera;

    void Awake()
    {
        axisController = GetComponent<CinemachineInputAxisController>();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    private void Start()
    {
        axisController.enabled = false;
        canMoveCamera = false;
        CardGameManager.Instance.OnInventoryHide += CardGameManager_OnInventoryHide;
    }

    private void CardGameManager_OnInventoryHide(object sender, System.EventArgs e)
    {
        canMoveCamera = true;
    }

    void Update()
    {
        if (!canMoveCamera)
            return;

        axisController.enabled = true;

        bool rightClickHeld = Input.GetMouseButton(1);

        axisController.enabled = rightClickHeld;
    }
}
