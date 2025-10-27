using UnityEngine;

/// <summary>
/// GameBootstrap
/// --------------
/// Al iniciar la escena:
/// 1) Busca el objeto con tag "Mountain" y su MeshFilter.
/// 2) Ejecuta el escaneo en MeshSlopeScannerSimple.
/// 3) Pide a MountainPathfinder que construya el grafo y calcule la ruta base (base cima).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Referencias")]
    public MeshSlopeScannerSimple scanner;
    public MountainPathfinder pathfinder;

    [Header("Opciones")]
    public bool logInfo = true;

    private void Start()
    {

        // 1) Localiza la montaña por tag
        GameObject mountain = GameObject.FindWithTag("Mountain");
        if (mountain == null)
        {
            Debug.LogError("[GameBootstrap] No se encontró ningún objeto con tag 'Mountain'.");
            return;
        }

        MeshFilter mf = mountain.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("[GameBootstrap] La 'Mountain' no tiene MeshFilter o no tiene mesh asignado.");
            return;
        }

        // 2) Ejecuta escaneo (equivalente a pulsar E, pero por código)
        scanner.ScanMesh(mf);
        if (logInfo) Debug.Log($"[GameBootstrap] Escaneo completado. Triángulos: {scanner.triangles?.Count}");

        // 3) Asegura que el pathfinder tenga las referencias y resuelva la ruta
        if (pathfinder.scanner == null) pathfinder.scanner = scanner;
        if (pathfinder.mountainMeshFilter == null) pathfinder.mountainMeshFilter = mf;

        bool ok = pathfinder.BuildGraphAndSolveAuto();
        if (ok && logInfo) Debug.Log("[GameBootstrap] Ruta base calculada con éxito (base→cima).");
    }
}
