using UnityEngine;
using System;
using System.Collections.Generic;

public class LevelExperienceManager : MonoBehaviour
{
    public static LevelExperienceManager Instance { get; private set; }

    public event Action OnExperienceChanged;
    public event EventHandler OnLevelUp;
    public event Action OnCardRewardTriggered;

    [Header("Level Data")]
    [SerializeField] private int baseXPToNextLevel = 200;
    [SerializeField] private int xpIncreasePerLevel = 100;
    [SerializeField] private int levelExperienceToAdd = 100;

    [Header("Niveles con recompensa de carta")]
    [SerializeField] private List<int> rewardLevels = new List<int> { 2, 10 };

    [SerializeField] private AudioClip levelUpAudioclip;

    private int level = 1;
    private int currentXP = 0;
    private int xpToNextLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[LevelExperienceManager] Instancia duplicada destruida en {gameObject.scene.name}.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[LevelExperienceManager] Instancia creada y registrada.");
    }

    private void Start()
    {
        xpToNextLevel = CalculateXPToNextLevel();
    }

    private void OnEnable()
    {
        if (DeathCinematicManager.Instance != null)
            DeathCinematicManager.Instance.OnCinematicFinished += DeathCinematicManager_OnCinematicFinished;
        else
            Debug.LogWarning("[LevelExperienceManager] DeathCinematicManager.Instance es null en OnEnable.");
    }

    private void OnDisable()
    {
        if (DeathCinematicManager.Instance != null)
            DeathCinematicManager.Instance.OnCinematicFinished -= DeathCinematicManager_OnCinematicFinished;
    }

    private void DeathCinematicManager_OnCinematicFinished(object sender, EventArgs e)
    {
        AddExperience(levelExperienceToAdd);
    }

    private void AddExperience(int amount)
    {
        currentXP += amount;
        Debug.Log($"[LevelExperienceManager] +{amount} XP → total {currentXP}/{xpToNextLevel} (nivel {level})");

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

        Debug.Log($"[LevelExperienceManager] ¡Nivel {level}! Suscriptores en OnCardRewardTriggered: {OnCardRewardTriggered?.GetInvocationList().Length ?? 0}");

        Temporal_Sound_Music.Instance.Play2DSound(levelUpAudioclip, 1.0f);

        OnLevelUp?.Invoke(this, EventArgs.Empty);

        if (rewardLevels.Contains(level))
        {
            Debug.Log($"[LevelExperienceManager] Nivel {level} es de recompensa → disparando OnCardRewardTriggered.");
            OnCardRewardTriggered?.Invoke();
        }
    }

    private int CalculateXPToNextLevel()
    {
        return baseXPToNextLevel + (level - 1) * xpIncreasePerLevel;
    }

    // GETTERS
    public float GetExperienceNormalized() => xpToNextLevel > 0 ? (float)currentXP / xpToNextLevel : 0f;
    public int GetLevel() => level;
    public int GetCurrentXp() => currentXP;
    public int GetXpToNextLevel() => xpToNextLevel;
    public int GetXpToLevel() => baseXPToNextLevel;
    public int GetRemainingXpToNextLevel() => xpToNextLevel - currentXP;
}