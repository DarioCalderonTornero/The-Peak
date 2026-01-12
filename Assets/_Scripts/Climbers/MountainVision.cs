using UnityEngine;
using System.Collections.Generic;

public class MountainVision : MonoBehaviour
{
    
    public static MountainVision Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
        propBlock = new MaterialPropertyBlock();
    }
    
    [Header("Configuración")]
    public KeyCode visionKey = KeyCode.V;
    private string boolPropertyName = "_IsActive"; 
    private bool isVisionActive = false;
    private List<Renderer> climberRenderers = new List<Renderer>();
    private MaterialPropertyBlock propBlock;

    void Update()
    {
        if (Input.GetKeyDown(visionKey))
        {
            ToggleVision();
        }
    }

    void ToggleVision()
    {
        isVisionActive = !isVisionActive;

        /*
        if (isVisionActive) Debug.Log("[VISIÓN] -> ACTIVADA");
        else Debug.Log("[VISIÓN] -> DESACTIVADA");
        */
        UpdateAllClimbers();
        
    }

    void UpdateAllClimbers()
    {
        float intensity = isVisionActive ? 1.0f : 0.0f;

        for (int i = climberRenderers.Count - 1; i >= 0; i--)
        {
            Renderer r = climberRenderers[i];

            if (r == null)
            {
                climberRenderers.RemoveAt(i);
                continue;
            }

            ApplyVisualEffect(r, intensity);
        }
    }

    void ApplyVisualEffect(Renderer r, float intensity)
    {
        r.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat(boolPropertyName, intensity);
        r.SetPropertyBlock(propBlock, 1);
    }

    public void RegisterClimber(Renderer r)
    {
        if (!climberRenderers.Contains(r))
        {
            climberRenderers.Add(r);

            if (isVisionActive)
            {
                ApplyVisualEffect(r, 1.0f);
            }
        }
    }

    public void UnregisterClimber(Renderer r)
    {
        if (climberRenderers.Contains(r))
        {
            climberRenderers.Remove(r);
        }
    }
}