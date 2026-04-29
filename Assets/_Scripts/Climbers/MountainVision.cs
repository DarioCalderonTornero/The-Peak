using UnityEngine;
using System.Collections.Generic;

public class MountainVision : MonoBehaviour
{
    public static MountainVision Instance;

    [Header("Configuración de Material")]
    [SerializeField] private string boolPropertyName = "_IsActive";
    private MaterialPropertyBlock propBlock;

    [Header("Objetos de Visión (Manual)")]
    [Tooltip("Arrastra aquí los GameObjects que quieras activar/desactivar con la visión")]
    [SerializeField] private List<GameObject> objectsToToggle = new List<GameObject>();

    private bool isVisionActive = false;
    private List<Renderer> climberRenderers = new List<Renderer>();

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

    private void Start()
    {
        InputManager.Instance.OnClimberVision += InputManager_OnClimberVision;

        // Inicializar el estado de los objetos según el estado de la visión al empezar
        UpdateManualObjectsState();
    }

    private void InputManager_OnClimberVision(object sender, System.EventArgs e)
    {
        ToggleVision();
    }

    void ToggleVision()
    {
        isVisionActive = !isVisionActive;

        UpdateAllClimbers();
        UpdateManualObjectsState(); // Activa/Desactiva los objetos de la lista
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

    // Recorre la lista manual y cambia el estado de cada objeto
    void UpdateManualObjectsState()
    {
        foreach (GameObject obj in objectsToToggle)
        {
            if (obj != null)
            {
                obj.SetActive(isVisionActive);
            }
        }
    }

    void ApplyVisualEffect(Renderer r, float intensity)
    {
        // Se mantiene el índice 1 para el material de efecto
        r.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat(boolPropertyName, intensity);
        r.SetPropertyBlock(propBlock, 1);
    }

    public void RegisterClimber(Renderer r)
    {
        if (!climberRenderers.Contains(r))
        {
            climberRenderers.Add(r);
            if (isVisionActive) ApplyVisualEffect(r, 1.0f);
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