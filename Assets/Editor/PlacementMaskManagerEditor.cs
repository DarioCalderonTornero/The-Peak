#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlacementMaskManager))]
public class PlacementMaskManagerEditor : Editor
{
    private int brushRadius = 0;       // 0=1 celda, 1=3x3, etc.
    private bool paintBlocked = true;  // true=bloquea, false=habilita

    // Visual
    private int viewRange = 10;        // cuántas casillas se dibujan alrededor del ratón
    private float cellSize = 1f;       // tu grid es 1 unidad
    private float yDrawOffset = 0.02f; // para que no parpadee

    private bool lockToSegment = false;
    private SegmentGridSettings lockedSegment = null;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Painter", EditorStyles.boldLabel);
        brushRadius = EditorGUILayout.IntSlider("Brush Radius", brushRadius, 0, 8);
        paintBlocked = EditorGUILayout.Toggle("Paint Blocked", paintBlocked);
        viewRange = EditorGUILayout.IntSlider("View Range", viewRange, 4, 25);

        EditorGUILayout.HelpBox(
            "SHIFT + Click/Drag en la SceneView para pintar.\n" +
            "Paint Blocked = true bloquea, false habilita.\n" +
            "Ahora se dibuja una rejilla alrededor del ratón (rojo=bloqueado, blanco=libre).",
            MessageType.Info
        );
    }

    private void OnSceneGUI()
    {
        var mgr = (PlacementMaskManager)target;
        if (mgr == null || mgr.data == null) return;

        Event e = Event.current;

        // Ray desde el ratón
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        int mask = (mgr.paintMask.value == 0) ? ~0 : mgr.paintMask.value;

        RaycastHit[] hits = Physics.RaycastAll(ray, 2000f, mask);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit hit = hits[0];
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null && hits[i].collider.GetComponentInParent<SegmentGridSettings>() != null)
            {
                hit = hits[i];
                break;
            }
        }

        // 🔥 TOGGLE LOCK SEGMENT (SHIFT + P)
        if (e.type == EventType.KeyDown && e.shift && e.keyCode == KeyCode.P)
        {
            lockToSegment = !lockToSegment;

            if (lockToSegment)
            {
                lockedSegment = hit.collider.GetComponentInParent<SegmentGridSettings>();
                Debug.Log("🔒 Segment LOCKED: " + (lockedSegment != null ? lockedSegment.name : "NULL"));
            }
            else
            {
                lockedSegment = null;
                Debug.Log("🔓 Segment UNLOCKED");
            }

            e.Use();
        }

        Vector2Int centerGlobal = new Vector2Int(
            Mathf.RoundToInt(hit.point.x),
            Mathf.RoundToInt(hit.point.z)
        );

        // Overlay
        DrawOverlay(mgr, hit, centerGlobal, mask);

        if (!e.shift) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            SegmentGridSettings settings = lockToSegment
                ? lockedSegment
                : hit.collider.GetComponentInParent<SegmentGridSettings>();

            if (lockToSegment && settings == null)
                return;

            if (settings != null)
            {
                if (settings.TryWorldToCell(hit.point, out int ci, out int cj))
                {
                    Undo.RecordObject(settings, "Paint Segment Grid Mask");

                    for (int dx = -brushRadius; dx <= brushRadius; dx++)
                    {
                        for (int dz = -brushRadius; dz <= brushRadius; dz++)
                        {
                            settings.SetBlocked(ci + dx, cj + dz, paintBlocked);
                        }
                    }

                    EditorUtility.SetDirty(settings);
                    SceneView.RepaintAll();
                }

                e.Use();
                return;
            }

            // GLOBAL fallback
            Undo.RecordObject(mgr.data, "Paint Placement Mask");

            for (int dx = -brushRadius; dx <= brushRadius; dx++)
            {
                for (int dz = -brushRadius; dz <= brushRadius; dz++)
                {
                    Vector2Int c = new Vector2Int(centerGlobal.x + dx, centerGlobal.y + dz);
                    mgr.data.SetBlocked(c, paintBlocked);
                }
            }

            EditorUtility.SetDirty(mgr.data);
            SceneView.RepaintAll();
            e.Use();
        }
    }

    private void DrawOverlay(PlacementMaskManager mgr, RaycastHit centerHit, Vector2Int center, int mask)
    {
        // ¿Estamos sobre un segmento con rejilla local?
        SegmentGridSettings settings = lockToSegment
    ? lockedSegment
    : centerHit.collider.GetComponentInParent<SegmentGridSettings>();

        // ============================
        // MODO SEGMENTO (rejilla local)
        // ============================
        if (settings != null)
        {
            settings.EnsureMask();

            settings.GetPlaneBasis(out var U, out var V, out var N);
            float cs = Mathf.Max(0.01f, settings.cellSize);
            Vector3 origin = settings.OriginWorld;

            int w = Mathf.Max(1, settings.gridDims.x);
            int h = Mathf.Max(1, settings.gridDims.y);

            // Dibuja toda la matriz local (ej: 4x4)
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                    Vector3 rayOrigin = planeCenter + N * 5f;
                    if (!Physics.Raycast(rayOrigin, -N, out RaycastHit cellHit, 30f, mask))
                        continue;

                    bool blocked = settings.IsBlocked(i, j);

                    Handles.color = blocked
                        ? new Color(1f, 0f, 0f, 0.35f)
                        : new Color(1f, 1f, 1f, 0.18f);

                    Vector3 up = cellHit.normal;
                    Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                    if (fwd.sqrMagnitude < 0.0001f)
                        fwd = Vector3.ProjectOnPlane(U, up).normalized;

                    Quaternion rot = Quaternion.LookRotation(fwd, up);

                    using (new Handles.DrawingScope(Matrix4x4.TRS(cellHit.point + up * yDrawOffset, rot, Vector3.one)))
                    {
                        Handles.DrawWireCube(Vector3.zero, new Vector3(0.95f * cs, 0f, 0.95f * cs));
                    }
                }
            }

            // Brush verde en el segmento (local)
            if (settings.TryWorldToCell(centerHit.point, out int ci, out int cj))
            {
                Handles.color = new Color(0f, 1f, 0f, 0.8f);

                for (int dx = -brushRadius; dx <= brushRadius; dx++)
                {
                    for (int dz = -brushRadius; dz <= brushRadius; dz++)
                    {
                        int bi = ci + dx;
                        int bj = cj + dz;
                        if (!settings.InBounds(bi, bj)) continue;

                        Vector3 planeCenter = origin + (bi + 0.5f) * cs * U + (bj + 0.5f) * cs * V;
                        Vector3 rayOrigin = planeCenter + N * 5f;

                        if (!Physics.Raycast(rayOrigin, -N, out RaycastHit cellHit, 30f, mask))
                            continue;

                        Vector3 up = cellHit.normal;
                        Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                        if (fwd.sqrMagnitude < 0.0001f)
                            fwd = Vector3.ProjectOnPlane(U, up).normalized;

                        Quaternion rot = Quaternion.LookRotation(fwd, up);

                        using (new Handles.DrawingScope(Matrix4x4.TRS(cellHit.point + up * (yDrawOffset + 0.01f), rot, Vector3.one)))
                        {
                            Handles.DrawWireCube(Vector3.zero, new Vector3(0.98f * cs, 0f, 0.98f * cs));
                        }
                    }
                }
            }

            return; // ✅ importante: si hay settings, no dibujes la global
        }

        // ======================================
        // FALLBACK GLOBAL (tu sistema por Round)
        // ======================================

        var touchedSegments = new System.Collections.Generic.HashSet<SegmentGridSettings>();
        bool nearAnySegment = false;

        for (int dx = -viewRange; dx <= viewRange; dx++)
        {
            for (int dz = -viewRange; dz <= viewRange; dz++)
            {
                Vector2Int c = new Vector2Int(center.x + dx, center.y + dz);
                if (!mgr.data.InBounds(c)) continue;

                Vector3 probe = new Vector3(c.x * cellSize, centerHit.point.y + 50f, c.y * cellSize);
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit cellHit, 200f, mask))
                    continue;

                SegmentGridSettings segSettings = cellHit.collider.GetComponentInParent<SegmentGridSettings>();

                if (segSettings != null)
                {
                    nearAnySegment = true;   // 🔥 NUEVO
                    continue;
                }

                bool blocked = mgr.data.IsBlocked(c);

                Handles.color = blocked
                    ? new Color(1f, 0f, 0f, 0.35f)
                    : new Color(1f, 1f, 1f, 0.18f);

                Vector3 up = cellHit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(centerHit.transform.forward, up).normalized;

                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(centerHit.transform.right, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);

                using (new Handles.DrawingScope(Matrix4x4.TRS(cellHit.point + up * yDrawOffset, rot, Vector3.one)))
                {
                    Handles.DrawWireCube(Vector3.zero, new Vector3(0.95f * cellSize, 0f, 0.95f * cellSize));
                }
            }
        }

        // 🔥 CLAVE: si hay algún segmento cerca, NO dibujar grid global
        if (nearAnySegment)
        {
            return;
        }

        // ✅ Opcional: dibuja la rejilla LOCAL de los segmentos con settings que han entrado en rango
        /* foreach (var s in touchedSegments)
        {
            DrawSegmentLocalGrid(s, mask);
        }*/

        // Brush verde global
        Handles.color = new Color(0f, 1f, 0f, 0.75f);
        for (int dx = -brushRadius; dx <= brushRadius; dx++)
        {
            for (int dz = -brushRadius; dz <= brushRadius; dz++)
            {
                Vector2Int c = new Vector2Int(center.x + dx, center.y + dz);
                if (!mgr.data.InBounds(c)) continue;

                Vector3 basePos = centerHit.point;

                // movernos en plano tangente al suelo en vez de XZ puro
                Vector3 right = Vector3.ProjectOnPlane(Vector3.right, centerHit.normal).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, centerHit.normal).normalized;

                Vector3 probe = basePos + right * dx * cellSize + forward * dz * cellSize + Vector3.up * 5f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit cellHit, 200f, mask))
                    continue;

                Vector3 up = cellHit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(Vector3.right, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);

                using (new Handles.DrawingScope(Matrix4x4.TRS(cellHit.point + up * (yDrawOffset + 0.01f), rot, Vector3.one)))
                {
                    Handles.DrawWireCube(Vector3.zero, new Vector3(0.98f * cellSize, 0f, 0.98f * cellSize));
                }
            }
        }

        if (lockToSegment && lockedSegment != null)
        {
            Handles.color = Color.cyan;
            Handles.Label(lockedSegment.transform.position, "LOCKED");
        }
    }

    private void DrawSegmentLocalGrid(SegmentGridSettings settings, int mask)
    {
        if (settings == null) return;

        settings.EnsureMask();
        settings.GetPlaneBasis(out var U, out var V, out var N);

        float cs = Mathf.Max(0.01f, settings.cellSize);
        Vector3 origin = settings.OriginWorld;

        int w = Mathf.Max(1, settings.gridDims.x);
        int h = Mathf.Max(1, settings.gridDims.y);

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                Vector3 rayOrigin = planeCenter + N * 5f;
                if (!Physics.Raycast(rayOrigin, -N, out RaycastHit cellHit, 30f, mask))
                    continue;

                bool blocked = settings.IsBlocked(i, j);
                Handles.color = blocked
                    ? new Color(1f, 0f, 0f, 0.35f)
                    : new Color(1f, 1f, 1f, 0.18f);

                Vector3 up = cellHit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(U, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);

                using (new Handles.DrawingScope(Matrix4x4.TRS(cellHit.point + up * yDrawOffset, rot, Vector3.one)))
                {
                    Handles.DrawWireCube(Vector3.zero, new Vector3(0.95f * cs, 0f, 0.95f * cs));
                }
            }
        }
    }
}
#endif