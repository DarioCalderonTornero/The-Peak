using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralMountain))]
public class ProceduralMountainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ProceduralMountain mountain = (ProceduralMountain)target;
        if (GUILayout.Button("Regenerar Montaña"))
        {
            mountain.GenerateMountain();
        }
    }
}
