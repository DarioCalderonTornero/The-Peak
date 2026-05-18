using UnityEngine;

[ExecuteAlways]
public class SkyController : MonoBehaviour
{
    public Material skyMaterial;

    void Update()
    {
        // La dirección hacia la que "mira" la luz es el centro del sol
        Vector3 dir = -transform.forward;
        skyMaterial.SetVector("_SunDirection", dir);

        // Opcional: Cambiar la exposición del cielo según la altura del sol
        float sunHeight = Mathf.Max(0, dir.y);
        skyMaterial.SetFloat("_SunHeight", sunHeight);
    }
}