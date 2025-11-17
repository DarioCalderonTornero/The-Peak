using UnityEngine;

public class DefensePlacer : MonoBehaviour
{
    public static DefensePlacer Instance { get; private set; }

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
    }

    // Versión antigua (por si algún código la usa todavía)
    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        return PlaceDefense(prefab, position, Vector3.up);
    }

    // NUEVA: con normal de superficie
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Vector3 surfaceNormal)
    {
        // Queremos que el "up" del prefab apunte a la normal del suelo/paret
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

        GameObject instance = Instantiate(prefab, position, rotation);

        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        return instance;
    }

}
