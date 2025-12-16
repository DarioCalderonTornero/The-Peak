using Unity.Cinemachine;
using UnityEngine;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera gameOverCinemachineCam;

    private void Awake()
    {
        gameOverCinemachineCam.Priority = 0;
    }

    private void Start()
    {
        if (ClimberMovement.Instance != null)
        {
            ClimberMovement.Instance.OnReachedGoal += ClimberMovement_OnReachedGoal;
        }
        else
        {
            StartCoroutine(WaitForClimberMovement());
        }
    }

    private IEnumerator WaitForClimberMovement()
    {
        yield return new WaitUntil(() => ClimberMovement.Instance != null);
        ClimberMovement.Instance.OnReachedGoal += ClimberMovement_OnReachedGoal;
    }

    private void ClimberMovement_OnReachedGoal(object sender, System.EventArgs e)
    {
        ClimberMovement.Instance.SetExternalSpeedMultiplier(0f);
        gameOverCinemachineCam.Priority = 100;
    }
}
