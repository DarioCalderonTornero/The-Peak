using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(DecalProjector))]
public class DecalFader : MonoBehaviour
{
    public float timeBeforeFade = 5f;
    public float fadeDuration = 3f;

    private DecalProjector decalProjector;
    private float timer = 0f;

    void Awake() // Usamos Awake en vez de Start para asegurar que se guarde la referencia
    {
        decalProjector = GetComponent<DecalProjector>();
    }

    // Este método lo llama el Spawner cuando recicla el Decal
    public void ResetFade()
    {
        timer = 0f;
        decalProjector.fadeFactor = 1f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > timeBeforeFade)
        {
            float fadeAmount = 1f - ((timer - timeBeforeFade) / fadeDuration);
            decalProjector.fadeFactor = Mathf.Clamp01(fadeAmount);

            if (decalProjector.fadeFactor <= 0f)
            {
                // ¡IMPORTANTE! Lo apagamos en lugar de destruirlo
                gameObject.SetActive(false);
            }
        }
    }
}