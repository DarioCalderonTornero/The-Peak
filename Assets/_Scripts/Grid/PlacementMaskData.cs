using UnityEngine;

[CreateAssetMenu(fileName = "PlacementMaskData", menuName = "TowerDefense/Placement Mask")]
public class PlacementMaskData : ScriptableObject
{
    public Vector2Int minCoord = new Vector2Int(-50, -50);
    public Vector2Int maxCoord = new Vector2Int(50, 50);

    [SerializeField] private byte[] blocked; // 0 = allowed, 1 = blocked

    public int Width => maxCoord.x - minCoord.x + 1;
    public int Height => maxCoord.y - minCoord.y + 1;

    private void OnValidate() => EnsureSize();

    public void EnsureSize()
    {
        int w = Mathf.Max(1, Width);
        int h = Mathf.Max(1, Height);
        int len = w * h;

        if (blocked == null || blocked.Length != len)
            blocked = new byte[len];
    }

    public bool InBounds(Vector2Int c)
        => c.x >= minCoord.x && c.x <= maxCoord.x && c.y >= minCoord.y && c.y <= maxCoord.y;

    private int Index(Vector2Int c)
    {
        int x = c.x - minCoord.x;
        int z = c.y - minCoord.y;
        return x + z * Width;
    }

    public bool IsBlocked(Vector2Int c)
    {
        if (!InBounds(c)) return true; // fuera del área => no construible
        EnsureSize();
        return blocked[Index(c)] == 1;
    }

    public void SetBlocked(Vector2Int c, bool value)
    {
        if (!InBounds(c)) return;
        EnsureSize();
        blocked[Index(c)] = (byte)(value ? 1 : 0);
    }
}