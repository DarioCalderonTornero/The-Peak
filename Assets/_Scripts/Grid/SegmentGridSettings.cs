using UnityEngine;

public class SegmentGridSettings : MonoBehaviour
{
    public Transform gridAxis;
    public Transform gridOrigin;

    public Vector2Int gridDims = new Vector2Int(4, 4);
    public float cellSize = 1f;

    [SerializeField] private byte[] blocked; // 0=libre, 1=bloqueado

    public Vector3 OriginWorld => gridOrigin != null ? gridOrigin.position : transform.position;

    private void OnValidate() => EnsureMask();

    public void EnsureMask()
    {
        int w = Mathf.Max(1, gridDims.x);
        int h = Mathf.Max(1, gridDims.y);
        int len = w * h;
        if (blocked == null || blocked.Length != len)
            blocked = new byte[len];
    }

    private int Index(int i, int j) => i + j * Mathf.Max(1, gridDims.x);

    public bool InBounds(int i, int j)
        => i >= 0 && j >= 0 && i < gridDims.x && j < gridDims.y;

    public bool IsBlocked(int i, int j)
    {
        EnsureMask();
        if (!InBounds(i, j)) return true;
        return blocked[Index(i, j)] == 1;
    }

    public void SetBlocked(int i, int j, bool value)
    {
        EnsureMask();
        if (!InBounds(i, j)) return;
        blocked[Index(i, j)] = (byte)(value ? 1 : 0);
    }

    // Base fija del segmento (NO depende de la normal del triángulo => no “tiembla” al pintar)
    public void GetPlaneBasis(out Vector3 U, out Vector3 V, out Vector3 N)
    {
        Transform t = gridAxis != null ? gridAxis : transform;
        U = t.right.normalized;
        V = t.forward.normalized;
        N = t.up.normalized;
    }

    public bool TryWorldToCell(Vector3 worldPoint, out int i, out int j)
    {
        GetPlaneBasis(out var U, out var V, out _);
        float cs = Mathf.Max(0.0001f, cellSize);

        Vector3 rel = worldPoint - OriginWorld;
        float u = Vector3.Dot(rel, U) / cs;
        float v = Vector3.Dot(rel, V) / cs;

        i = Mathf.FloorToInt(u);
        j = Mathf.FloorToInt(v);
        return InBounds(i, j);
    }
}