using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [Header("Escenas de juego que se cargarán al inicio")]
    [SerializeField] private string[] scenesToLoadAdditive;

    private void Start()
    {
        // Cargar todas las escenas de contenido de forma additive
        foreach (var sceneName in scenesToLoadAdditive)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
        }
    }
}
