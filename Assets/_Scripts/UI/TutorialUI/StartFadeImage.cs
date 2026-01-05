using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartFadeImage : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    private void Start()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(1f);

        float duration = 3f;
        float time = 0f;

        Color color = fadeImage.color;

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, time / duration);
            fadeImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(color.r, color.g, color.b, 0f);
        Destroy(fadeImage);
    }

}
