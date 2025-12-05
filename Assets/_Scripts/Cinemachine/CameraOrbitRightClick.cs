using Unity.Cinemachine;
using UnityEngine;
using System.Collections;

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

    private IEnumerator Start()
    {
        axisController.enabled = false;
        canMoveCamera = false;

        yield return new WaitUntil(() => CardGameManager.Instance != null);
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

    private void OnDestroy()
    {
        if (CardGameManager.Instance != null)
        {
            CardGameManager.Instance.OnInventoryHide -= CardGameManager_OnInventoryHide;
        }
    }
        
}
