using System.Collections;
using UnityEngine;

public abstract class BaseDefense : MonoBehaviour
{
    private Vector3 originalScale;
    private Coroutine popRoutine;

    [Header("Pop al hover con pala")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float shovelPopDuration = 0.15f;

    public virtual void Initialize()
    {
        originalScale = transform.localScale;
    }

    public void SetShovelHover(bool active)
    {
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopScale(active ? originalScale * popScale : originalScale));
    }

    private IEnumerator PopScale(Vector3 target)
    {
        Vector3 from = transform.localScale;
        float t = 0f;
        while (t < shovelPopDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / shovelPopDuration), 2f);
            transform.localScale = Vector3.Lerp(from, target, n);
            yield return null;
        }
        transform.localScale = target;
    }
}