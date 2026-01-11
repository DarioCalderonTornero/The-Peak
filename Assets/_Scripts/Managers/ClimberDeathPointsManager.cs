using UnityEngine;

public class ClimberDeathPointsManager : MonoBehaviour
{
    public static ClimberDeathPointsManager Instance { get; private set; }

    private int climbersDeath;

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
        climbersDeath = 0;
    }

    public void AddClimberDeathPoints()
    {
        climbersDeath++;
    }

    public int GetTotalClimberDeathPoints()
    {
        return climbersDeath; 
    }
}
