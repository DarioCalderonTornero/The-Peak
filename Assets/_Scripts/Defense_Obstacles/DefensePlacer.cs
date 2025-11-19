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

    // Versión antigua (por si algún código la usa todavía):
    // ahora instancia con rotación identidad
    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        return PlaceDefense(prefab, position, Quaternion.identity);
    }

    // Versión con normal de superficie (se sigue usando si quieres)
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Vector3 surfaceNormal)
    {
        // Queremos que el "up" del prefab apunte a la normal del suelo/paret
        Quaternion baseRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        return PlaceDefense(prefab, position, baseRotation);
    }

    // 🔹 NUEVA: versión central que recibe una rotación explícita
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = Instantiate(prefab, position, rotation);

        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        return instance;
    }
}
