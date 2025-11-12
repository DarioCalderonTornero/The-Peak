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

    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);

        // Inicializar defensa si tiene lógica
        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        return instance; // <<< DEVOLVEMOS LA INSTANCIA
    }

}
