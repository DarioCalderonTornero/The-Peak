using System.Collections;
using UnityEngine;

public class PlastonFadeIn : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f;

    private Material plastonMat;

    private void Start()
    {
        plastonMat = GetComponent<Renderer>().material;
        plastonMat.SetFloat("_Cantidad", 0f);
        StartCoroutine(FadePlastonIn());
    }

    private IEnumerator FadePlastonIn()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            plastonMat.SetFloat("_Cantidad", t);
            yield return null;
        }
        plastonMat.SetFloat("_Cantidad", 1f);

        Destroy(gameObject);
    }
}
