using System.Collections;
using UnityEngine;

public class HandleSnowObstacle : BaseObstacle
{
    [Header("Counter Settings")]
    [SerializeField] private float shrinkDuration = 0.4f;
    [SerializeField] private AudioClip counterAudioClip;
    [SerializeField] private GameObject counterVFX;
    [SerializeField] private float snowCounterDelay = 1f;

    private bool counterTriggered = false;

    private void Awake()
    {
        obstacleType = ObstacleType.Snow;
    }

    protected override void OnHandleBy(ClimberLoadout loadout)
    {
        if (counterTriggered) return;
        counterTriggered = true;

        StartCoroutine(CounterRoutine());
    }

    private IEnumerator CounterRoutine()
    {
        yield return new WaitForSeconds(snowCounterDelay);

        if (counterAudioClip != null)
            Temporal_Sound_Music.Instance.Play3DSound(counterAudioClip, transform.position, 1f, 15f, 30f);

        if (counterVFX != null)
            Instantiate(counterVFX, transform.position, Quaternion.identity);

        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        Destroy(gameObject);
    }
}