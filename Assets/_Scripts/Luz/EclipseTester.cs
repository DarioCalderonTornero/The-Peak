using UnityEngine;

public class EclipseTester : MonoBehaviour
{
    [Header("Panel de Control del Eclipse")]
    [Tooltip("Marca esta casilla durante el Play para iniciar el eclipse. Desmárcala para terminarlo.")]
    public bool activarEclipse = false;

    // Guardamos el estado anterior para saber exactamente en qué frame haces clic
    private bool estadoAnterior = false;

    void Update()
    {
        // El botón solo debe funcionar cuando el juego está corriendo
        if (!Application.isPlaying) return;

        // Comprobamos si has cambiado la casilla en el Inspector
        if (activarEclipse != estadoAnterior)
        {
            if (activarEclipse)
            {
                if (SkyController.Instance != null)
                {
                    SkyController.Instance.IniciarEclipse();
                    Debug.Log("🌑 [EclipseTester] Iniciando Eclipse...");
                }
                else
                {
                    Debug.LogError("No se ha encontrado el SkyController en la escena.");
                }
            }
            else
            {
                if (SkyController.Instance != null)
                {
                    SkyController.Instance.TerminarEclipse();
                    Debug.Log("☀️ [EclipseTester] Terminando Eclipse, volviendo a la normalidad...");
                }
            }

            // Actualizamos la memoria del estado
            estadoAnterior = activarEclipse;
        }
    }
}