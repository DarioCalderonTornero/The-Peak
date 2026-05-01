using UnityEngine;
using UnityEngine.UI;

public class FixedScrollbarSize : MonoBehaviour
{
    [SerializeField] private Scrollbar scrollbar;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField][Range(0f, 1f)] private float handleSize = 0.1f;

    private void Update()
    {
        if (scrollbar == null || scrollRect == null) return;

        // Forzar el handle pequeño para que recorra toda la barra
        scrollbar.size = handleSize;

        // Remapear el valor real del scroll al rango completo 0-1
        float realValue = 1f - scrollRect.verticalNormalizedPosition;
        scrollbar.value = 1f - realValue;
    }
}