using UnityEngine;
using System.Collections;

public class HandleRockObstacle : BaseObstacle
{
    [SerializeField] private GameObject breakRockPrefab;
    [SerializeField] private float delayAfterSpawn = 2f;

    protected override void OnHandleBy(ClimberLoadout loadout)
    {
        if (obstacleType != ObstacleType.Rock)
            return;

        StartCoroutine(BreakRockAfterDelay());
    }

    private IEnumerator BreakRockAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterSpawn);

        if (breakRockPrefab != null)
        {
            GameObject breakRock = Instantiate(
                breakRockPrefab,
                transform.position,
                transform.rotation
            );
            breakRock.transform.localScale = transform.localScale;
        }

        Destroy(gameObject);
    }
}