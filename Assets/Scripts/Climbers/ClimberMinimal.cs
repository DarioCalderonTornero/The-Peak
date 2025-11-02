using System.Collections.Generic;
using UnityEngine;
using System.Collections;

/// <summary>
/// ClimberMinimal (sin snap-to-ground)
/// -----------------------------------
/// - Planifica SIEMPRE desde su posición actual hasta la cima (ruta óptima local).
/// - Avanza punto a punto por los centros de triángulo calculados por MountainPathfinder.
/// - Sin energía, replan, suavizado ni pegado al suelo.
/// </summary>
public class ClimberMinimal : MonoBehaviour
{
    [Header("Referencias")]
    public MountainPathfinder pathfinder;   // Si está vacío, se auto-busca

    [Header("Movimiento")]
    public float moveSpeed = 3.0f;          // m/s
    public float arriveRadius = 0.25f;      // distancia para considerar alcanzado un waypoint
    public float rotateSpeed = 10f;         // rotación suave mirando la dirección de avance (Y-up)

    [Header("Debug")]
    public bool drawLocalPath = true;
    public Color localPathColor = Color.magenta;

    // Estado interno
    private List<Vector3> _waypoints = new();
    private int _wpIndex = 0;
    private bool _arrived = false;

    private void Start()
    {
        // Arrancamos como rutina para dar tiempo al bootstrap (escaneo)
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        //if (!pathfinder) pathfinder = FindObjectOfType<MountainPathfinder>();
        if (!pathfinder)
        {
            Debug.LogError("[ClimberMinimal] No hay MountainPathfinder en escena.");
            yield break;
        }

        // Espera a que exista 'scanner.triangles' (el bootstrap lo rellena)
        float t = 0f, timeout = 5f;
        while ((pathfinder.scanner == null || pathfinder.scanner.triangles == null || pathfinder.scanner.triangles.Count == 0) && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (pathfinder.scanner == null || pathfinder.scanner.triangles == null || pathfinder.scanner.triangles.Count == 0)
        {
            Debug.LogError("[ClimberMinimal] Timeout esperando al escaneo de la montaña.");
            yield break;
        }

        // Planifica SIEMPRE desde mi posición actual hasta la cima
        _waypoints = pathfinder.PlanCentersFromWorldToSummit(transform.position);

        if (_waypoints == null || _waypoints.Count == 0)
        {
            Debug.LogWarning("[ClimberMinimal] No hay ruta disponible. Quedo en Idle.");
            _arrived = true;
            yield break;
        }

        _wpIndex = 0;
        _arrived = false;
    }

    private void Update()
    {
        if (_arrived || _waypoints == null || _waypoints.Count == 0) return;

        // Objetivo actual
        Vector3 target = _waypoints[_wpIndex];
        Vector3 to = target - transform.position;
        Vector3 toFlat = new Vector3(to.x, 0f, to.z);
        float dist = to.magnitude;

        // ¿Alcanzó waypoint?
        if (dist <= arriveRadius)
        {
            _wpIndex++;
            if (_wpIndex >= _waypoints.Count)
            {
                OnArrivedToGoal();
                return;
            }
            target = _waypoints[_wpIndex];
            to = target - transform.position;
            toFlat = new Vector3(to.x, 0f, to.z);
        }

        // Rotar hacia la dirección de avance (solo yaw)
        if (toFlat.sqrMagnitude > 1e-6f)
        {
            Quaternion look = Quaternion.LookRotation(toFlat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotateSpeed * Time.deltaTime);
        }

        // Avanzar
        if (to.sqrMagnitude > 1e-6f)
        {
            Vector3 dir = to.normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }

    private void OnEnable()
    {
        if (!pathfinder) pathfinder = FindFirstObjectByType<MountainPathfinder>();
        if (pathfinder != null) pathfinder.OnNavTopologyChanged += ReplanNow;
    }
    private void OnDisable()
    {
        if (pathfinder != null) pathfinder.OnNavTopologyChanged -= ReplanNow;
    }

    private void ReplanNow()
    {
        if (_arrived) return;
        // replan desde posición actual
        var newRoute = pathfinder.PlanCentersFromWorldToSummit(transform.position);
        if (newRoute != null && newRoute.Count > 0)
        {
            _waypoints = newRoute;
            // opcional: salta al waypoint más cercano para evitar retrocesos bruscos
            _wpIndex = 0;
        }
        else
        {
            // sin ruta: quedar idle (podrías marcar muerto/atascado si quieres)
            _waypoints.Clear();
            _arrived = true;
            Debug.LogWarning("[ClimberMinimal] Replan fallido, sin ruta.");
        }
    }


    private void OnArrivedToGoal()
    {
        _arrived = true;
        Debug.Log("[ClimberMinimal] ¡Cima alcanzada!");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawLocalPath || _waypoints == null || _waypoints.Count < 2) return;
        Gizmos.color = localPathColor;
        for (int i = 0; i < _waypoints.Count - 1; i++)
            Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
        foreach (var p in _waypoints)
            Gizmos.DrawSphere(p + Vector3.up * 0.05f, 0.06f);
    }
#endif
}
