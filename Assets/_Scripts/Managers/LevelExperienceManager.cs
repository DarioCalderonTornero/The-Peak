using UnityEngine;
using System;

public class LevelExperienceManager : MonoBehaviour
{
    public static LevelExperienceManager Instance { get; private set; }

    public event Action OnExperienceChanged;
    public event EventHandler OnLevelUp;

    private int level = 1;
    private int currentXP = 0;
    private int xpToNextLevel = 5;

    [SerializeField] private int levelExperienceToAdd = 1;

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
        Temporal_Sound_Music.Instance.OnClimberDeath += Temporal_Sound_Music_OnClimberDeath;
    }

    private void Temporal_Sound_Music_OnClimberDeath(object sender, EventArgs e)
    {
        AddExperience(levelExperienceToAdd);
    }

    private void AddExperience(int amount)
    {
        currentXP += amount;

        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }

        OnExperienceChanged?.Invoke();
    }

    private void LevelUp()
    {
        OnLevelUp?.Invoke(this, EventArgs.Empty);
        level++;
        xpToNextLevel = 5 + level; 
    }

    // GETTERS
    public float GetExperienceNormalized()
    {
        return (float)currentXP / xpToNextLevel;
    }

    public int GetLevel()
    {
        return level;
    }
}
