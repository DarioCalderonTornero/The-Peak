using UnityEngine;
using UnityEngine.VFX;

public class LodoVFXController : MonoBehaviour
{
    private VisualEffect vfx;

    private void Start()
    {
        vfx = GetComponent<VisualEffect>();
    }

    public void SetBubbles(bool active)
    {
        vfx.SetFloat("CantidadBurbujas", active ? 1f: 0f);
        vfx.SetBool("Desactivado", !active);
    }

    
}
