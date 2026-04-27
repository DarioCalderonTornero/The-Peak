using UnityEngine;
using UnityEditor;

public class GlowTextureGenerator : MonoBehaviour
{
    [ContextMenu("Generate Glow Texture")]
    void Generate()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / (size / 2f)));
                alpha = Mathf.Pow(alpha, 2f); // suaviza el degradado
                tex.SetPixel(x, y, new Color(1f, 0.85f, 0.1f, alpha));
            }
        }

        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/Sprites/StarGlow.png", bytes);
        AssetDatabase.Refresh();
        Debug.Log("StarGlow.png generado en Assets/Sprites/");
    }
}