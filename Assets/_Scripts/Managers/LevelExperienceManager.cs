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
        GameManager.Instance.OnClimberDead += GameManager_OnClimberDead;
    }

    private void GameManager_OnClimberDead(GameManager.DeathInfo obj)
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
        if (xpToNextLevel <= 0) return 0f;
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

    public int GetXpToLevel()
    {
        return baseXPToNextLevel;
    }

    public int GetRemainingXpToNextLevel()
    {
        return xpToNextLevel - currentXP;
    }

}
