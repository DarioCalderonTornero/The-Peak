using UnityEngine;

public class ActivateOnEffectStart : MonoBehaviour
{
    public GameObject targetObject;
    public ParticleSystem effectSystem;

    private bool wasPlaying = false;

    void Start()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    void Update()
    {
        if (effectSystem == null) return;

        if (effectSystem.isPlaying)
        {
            if (!wasPlaying)
            {
                ActivateGameObject();
                wasPlaying = true;
            }
        }
        else
        {
            wasPlaying = false;
        }
    }

    private void ActivateGameObject()
    {
        if (targetObject != null && !targetObject.activeSelf)
        {
            targetObject.SetActive(true);
            Invoke(nameof(DeactivateGameObject), 1f);
        }
    }

    private void DeactivateGameObject()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }
}