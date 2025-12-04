using UnityEngine;

public class HideUIManager : MonoBehaviour
{
    public static HideUIManager Instance { get; private set; }

    [SerializeField] private UIManagerSO uiManagerSO;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void HideAllCanvas()
    {
        if (uiManagerSO.mainCanvas != null && uiManagerSO.alexCanvas != null)
        {
            uiManagerSO.mainCanvas.gameObject.SetActive(false);
            uiManagerSO.alexCanvas.gameObject.SetActive(false);
        }
    }

    public void ShowAllCanvas()
    {
        if (uiManagerSO.mainCanvas != null && uiManagerSO.alexCanvas != null)
        {
            uiManagerSO.mainCanvas.gameObject.SetActive(true);
            uiManagerSO.alexCanvas.gameObject.SetActive(true);
        }
    }
}
