using System.Collections;
using UnityEngine;

public abstract class BaseDefense : MonoBehaviour
{
    private Vector3 originalScale;
    private Coroutine popRoutine;

    [Header("Pop al hover con pala")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float shovelPopDuration = 0.15f;

    private Renderer[] cachedRenderers;
    private Material[][] originalMaterials;
    private bool materialsStored = false;

    public virtual void Initialize()
    {
        originalScale = transform.localScale;
    }

    public void SetShovelHover(bool active)
    {
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopScale(active ? originalScale * popScale : originalScale));

        if (active)
            ApplyShovelMaterial();
        else
            RestoreOriginalMaterials();
    }

    private void ApplyShovelMaterial()
    {
        var config = Resources.Load<DefenseConfig>("DefenseConfig");
        if (config == null || config.shovelHoverMaterial == null) return;

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[cachedRenderers.Length][];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            originalMaterials[i] = cachedRenderers[i].materials;
            var mats = new Material[cachedRenderers[i].materials.Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = config.shovelHoverMaterial;
            cachedRenderers[i].materials = mats;
        }

        materialsStored = true;
    }

    private void RestoreOriginalMaterials()
    {
        if (!materialsStored || cachedRenderers == null) return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null) continue;
            cachedRenderers[i].materials = originalMaterials[i];
        }

        materialsStored = false;
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