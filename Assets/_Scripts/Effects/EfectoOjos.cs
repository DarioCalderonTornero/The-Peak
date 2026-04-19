using UnityEngine;
using System.Collections;

public class EfectoOjos : MonoBehaviour
{
    [Header("Referencias de los Ojos")]
    public GameObject ojoIzquierdo;
    public GameObject ojoDerecho;

    [Header("Tiempos")]
    public float retrasoCaida = 0.2f;          // Tiempo antes de soltar los ojos
    public float tiempoEnElSuelo = 2f;         // Cuánto tiempo ruedan antes de empezar a desaparecer
    public float duracionDesvanecimiento = 1f; // Cuánto tarda el efecto de desaparecer

    [Header("Física")]
    public float fuerzaRodar = 2f;
    public float fuerzaSeparacion = 1f;        // Fuerza para que salten en direcciones distintas

    private void OnEnable()
    {
        StartCoroutine(SecuenciaCompletaOjos());
    }

    // He creado esta función separada para no repetir código, 
    // se encarga de soltar y empujar un solo ojo.
    private void ActivarYEmpujarOjo(GameObject ojo)
    {
        if (ojo == null) return;

        ojo.SetActive(true);
        ojo.transform.SetParent(null);

        Rigidbody rb = ojo.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Generamos rotación aleatoria
            Vector3 giroAleatorio = new Vector3(
                Random.Range(-fuerzaRodar, fuerzaRodar),
                Random.Range(-fuerzaRodar, fuerzaRodar),
                Random.Range(-fuerzaRodar, fuerzaRodar)
            );

            // Generamos un pequeño empujón aleatorio (hacia arriba y hacia los lados) 
            // para que los dos ojos se separen entre sí al aparecer
            Vector3 empujeAleatorio = new Vector3(
                Random.Range(-fuerzaSeparacion, fuerzaSeparacion),
                Random.Range(0f, fuerzaSeparacion),
                Random.Range(-fuerzaSeparacion, fuerzaSeparacion)
            );

            // Aplicamos ambas fuerzas
            rb.AddForce(empujeAleatorio, ForceMode.Impulse);
            rb.AddTorque(giroAleatorio, ForceMode.Impulse);
        }
    }

    private IEnumerator SecuenciaCompletaOjos()
    {
        // 1. ESPERA INICIAL
        yield return new WaitForSeconds(retrasoCaida);

        // 2. ACTIVAR, SOLTAR Y EMPUJAR AMBOS OJOS INDEPENDIENTEMENTE
        ActivarYEmpujarOjo(ojoIzquierdo);
        ActivarYEmpujarOjo(ojoDerecho);

        // 3. ESPERAR EN EL SUELO
        yield return new WaitForSeconds(tiempoEnElSuelo);

        // 4. DESVANECER POCO A POCO AMBOS A LA VEZ (FADE OUT)
        Renderer rendIzq = ojoIzquierdo != null ? ojoIzquierdo.GetComponent<Renderer>() : null;
        Renderer rendDer = ojoDerecho != null ? ojoDerecho.GetComponent<Renderer>() : null;

        // Obtenemos las instancias de los materiales para modificarlos
        Material matIzq = rendIzq != null ? rendIzq.material : null;
        Material matDer = rendDer != null ? rendDer.material : null;

        float tiempoPasado = 0f;

        while (tiempoPasado < duracionDesvanecimiento)
        {
            tiempoPasado += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, tiempoPasado / duracionDesvanecimiento);

            // Aplicamos la transparencia a ambos ojos en tiempo real
            if (matIzq != null)
                matIzq.color = new Color(matIzq.color.r, matIzq.color.g, matIzq.color.b, alpha);

            if (matDer != null)
                matDer.color = new Color(matDer.color.r, matDer.color.g, matDer.color.b, alpha);

            yield return null;
        }

        // 5. DESTRUIR AL TERMINAR
        if (ojoIzquierdo != null) Destroy(ojoIzquierdo);
        if (ojoDerecho != null) Destroy(ojoDerecho);
    }
}

