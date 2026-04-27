using UnityEngine;
using UnityEngine.UI;

public class CardSelectionTarget : MonoBehaviour
{
    [Tooltip("Image del FrontCard que cambia de color al seleccionar.")]
    [SerializeField] private Image frontImage;

    [Tooltip("Image del BackCard que debe tener siempre el mismo color que el front.")]
    [SerializeField] private Image backImage;

    public Image FrontImage => frontImage;
    public Image BackImage => backImage;

    private void Awake()
    {
        // Auto-detect de emergencia si no se asignan en el Inspector
        if (frontImage == null || backImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                string parentName = img.transform.parent != null
                    ? img.transform.parent.name.ToLower()
                    : "";

                if (frontImage == null && parentName.Contains("front"))
                    frontImage = img;
                else if (backImage == null && parentName.Contains("back"))
                    backImage = img;
            }
        }
    }

    /// <summary>
    /// Aplica el color a ambas caras a la vez.
    /// </summary>
    public void SetSelectionColor(Color color)
    {
        if (frontImage != null) frontImage.color = color;
        if (backImage != null) backImage.color = color;
    }
}
