using UnityEngine;

public class ClimberDeathPointsManager : MonoBehaviour
{
    public static ClimberDeathPointsManager Instance { get; private set; }

    private const string RECORD_KEY = "CLIMBER_DEATH_RECORD";

    private int climbersDeath;
    private int recordDeaths;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        recordDeaths = PlayerPrefs.GetInt(RECORD_KEY, 0);
    }

    private void Start()
    {
        climbersDeath = 0;
    }

    public void AddClimberDeathPoints()
    {
        climbersDeath++;

        if (climbersDeath > recordDeaths)
        {
            recordDeaths = climbersDeath;
            PlayerPrefs.SetInt(RECORD_KEY, recordDeaths);
            PlayerPrefs.Save();
        }
    }

    public int GetTotalClimberDeathPoints() => climbersDeath;
    public int GetRecordDeaths() => recordDeaths;

    public void ResetRunDeaths()
    {
        climbersDeath = 0;
    }
}
