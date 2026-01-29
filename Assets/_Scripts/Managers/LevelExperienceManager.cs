using UnityEngine;
using System;

public class LevelExperienceManager : MonoBehaviour
{
    public static LevelExperienceManager Instance { get; private set; }

    public event Action OnExperienceChanged;
    public event EventHandler OnLevelUp;

    [Header("Level Data")]
    [SerializeField] private int baseXPToNextLevel = 200;
    [SerializeField] private int xpIncreasePerLevel = 100;
    [SerializeField] private int levelExperienceToAdd = 100;

    private int level = 1;
    private int currentXP = 0;
    private int xpToNextLevel;

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
        xpToNextLevel = CalculateXPToNextLevel();
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
        level++;
        xpToNextLevel = CalculateXPToNextLevel();

        OnLevelUp?.Invoke(this, EventArgs.Empty);
    }

    private int CalculateXPToNextLevel()
    {
        return baseXPToNextLevel + (level - 1) * xpIncreasePerLevel;
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

    public int GetCurrentXp()
    {
        return currentXP;
    }

    public int GetXpToNextLevel()
    {
        return xpToNextLevel;
    }
}
