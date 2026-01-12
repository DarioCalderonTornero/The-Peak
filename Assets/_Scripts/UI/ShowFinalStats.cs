using TMPro;
using UnityEngine;

public class ShowFinalStats : MonoBehaviour
{
    public static ShowFinalStats Instance { get; private set; }
    [SerializeField] private TextMeshProUGUI totalClimberDeathText;
    [SerializeField] private TextMeshProUGUI recordClimberDeathText;

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

    private void Start()
    {
        Hide();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        int current = ClimberDeathPointsManager.Instance.GetTotalClimberDeathPoints();
        int record = ClimberDeathPointsManager.Instance.GetRecordDeaths();

        totalClimberDeathText.text = "CLIMBER DEATHS: " + current;
        recordClimberDeathText.text = "RECORD: " + record;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
