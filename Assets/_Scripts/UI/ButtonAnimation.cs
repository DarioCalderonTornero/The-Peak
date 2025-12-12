using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonAnimation : MonoBehaviour
{
    [Header("Pop Settings")]
    [Tooltip("Escala multiplicadora al hacer pop (0.85 = se encoge ligeramente)")]
    [SerializeField] private float popScale = 0.85f;

    [Tooltip("Velocidad del efecto pop (cuanto mayor, más rápido)")]
    [SerializeField] private float popSpeed = 30f;

    private Button button;
    private Vector3 originalScale;
    private Coroutine popCoroutine;

    private void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
        popSpeed = 8f;


        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (popCoroutine != null)
            StopCoroutine(popCoroutine);

        popCoroutine = StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        Vector3 targetScale = originalScale * popScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * popSpeed;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * popSpeed;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
        popCoroutine = null;
    }
}


