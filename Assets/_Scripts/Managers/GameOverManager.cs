using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using Unity.VisualScripting.Antlr3.Runtime;
using System;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    public event EventHandler OnGameOver;

    [SerializeField] private CinemachineCamera gameOverCinemachineCam;
    [SerializeField] private AudioClip gameOverClimberSound;

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
        gameOverCinemachineCam.Priority = 0;
    }

    public void SetGameOverCamera()
    {
        // Guardar ranking antes de cualquier otra cosa
        if (RankingManager.Instance != null && RankingManager.Instance.RankingEnabled)
        {
            RankingManager.Instance.SaveCurrentSession(
                TurnManager.Instance != null ? TurnManager.Instance.CurrentTurnNumber : 0,
                ClimberDeathPointsManager.Instance != null ? ClimberDeathPointsManager.Instance.GetTotalClimberDeathPoints() : 0
            );
        }

        OnGameOver?.Invoke(this, EventArgs.Empty);
        StartCoroutine(GetClimberSound());
        ClimberMovement.Instance.SetExternalSpeedMultiplier(0f);
        gameOverCinemachineCam.Priority = 100;
        Time.timeScale = 1f;
    }

    private IEnumerator GetClimberSound()
    {
        yield return new WaitForSeconds(3);
        Temporal_Sound_Music.Instance.PlaySound(gameOverClimberSound, 1.0f);
    }
}
