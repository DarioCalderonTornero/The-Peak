using UnityEngine;
using System.Collections.Generic;

public class MountainVision : MonoBehaviour
{
    public static MountainVision Instance;

    [Header("Configuración de Material")]
    [SerializeField] private string boolPropertyName = "_IsActive";
    private MaterialPropertyBlock propBlock;

    [Header("Objetos de Visión (Manual)")]
    [SerializeField] private List<GameObject> objectsToToggle = new List<GameObject>();

    private bool isVisionActive = false;
    private Dictionary<Renderer, Color> climberRayProperties = new Dictionary<Renderer, Color>();

    public Material xrayMaterial;

    public Material waterMaterial;
    private int waterCameraPosId;

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

        // Cacheamos el ID de la posición para el agua
        waterCameraPosId = Shader.PropertyToID("_PosicionCamara");
    }

    private void Start()
    {
        InputManager.Instance.OnClimberVision += InputManager_OnClimberVision;

        // Inicializar el estado de los objetos según el estado de la visión al empezar
        UpdateManualObjectsState();
    }

    void Update()
    {
        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            Vector3 camPosition = mainCam.transform.position;

            // 1. Le enviamos la posición al XRayMat
            if (xrayMaterial != null)
            {
                xrayMaterial.SetVector("_MainCameraPosition", camPosition);
            }

            // 2. Le enviamos la posición al material del Agua
            if (waterMaterial != null)
            {
                waterMaterial.SetVector(waterCameraPosId, camPosition);
            }
        }
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
        var keys = new List<Renderer>(climberRayProperties.Keys);

        foreach (Renderer r in keys)
        {
            if (r == null) { climberRayProperties.Remove(r); continue; }
            ApplyVisualEffect(r, intensity, climberRayProperties[r]);
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

    void ApplyVisualEffect(Renderer r, float intensity, Color xRayColor)
    {
        // Se mantiene el índice 1 para el material de efecto
        r.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat(boolPropertyName, intensity);
        propBlock.SetColor("_XRayColor", xRayColor);
        r.SetPropertyBlock(propBlock, 1);
    }

    public void RegisterClimber(Renderer renderer, Color xRayColor)
    {
        if (!climberRayProperties.ContainsKey(renderer))
        {
            climberRayProperties[renderer] = xRayColor;

            if (isVisionActive)
            {
                ApplyVisualEffect(renderer, 1.0f, xRayColor);
            }
        }
    }

    public void UnregisterClimber(Renderer renderer)
    {
        climberRayProperties.Remove(renderer);
    }
}