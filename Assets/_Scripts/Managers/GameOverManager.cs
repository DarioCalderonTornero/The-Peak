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

    public void SetGameOverCamer()
    {
        OnGameOver?.Invoke(this, EventArgs.Empty);
        StartCoroutine(GetClimberSound());
        //Climber
        ClimberMovement.Instance.SetExternalSpeedMultiplier(0f);
        //Camera
        gameOverCinemachineCam.Priority = 100;
        //Effects
        Time.timeScale = 1f;
    }

    private IEnumerator GetClimberSound()
    {
        yield return new WaitForSeconds(3);
        Temporal_Sound_Music.Instance.PlaySound(gameOverClimberSound, 1.0f);
    }
}
