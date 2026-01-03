using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera climberTutorialVCam;
    [SerializeField] private CinemachineCamera followClimberTutorialVCam;

    private void Awake()
    {
        CinemachineBlendManager.Instance.SetBlendCut();
    }

    private void MoveCameraToClimber()
    {

    }
}
