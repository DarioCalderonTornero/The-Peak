using Unity.Cinemachine;
using UnityEngine;

public class CinemachineBlendManager : MonoBehaviour
{
    public static CinemachineBlendManager Instance { get; private set; }

    [SerializeField] private CinemachineBrain cinemachineBrain;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (cinemachineBrain == null)
            cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
    }

    public void SetBlendEaseInOut(float blendDuration)
    {
        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendDuration);
    }

    public void SetBlendCut()
    {
        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    }
}
