using System;

[Serializable]
public struct CellKey : IEquatable<CellKey>
{
    public int segmentId; // 0 => grid global mundo. !=0 => segmento (GetInstanceID)
    public int x;         // worldX o i
    public int y;         // worldZ o j

    public CellKey(int segmentId, int x, int y)
    {
        this.segmentId = segmentId;
        this.x = x;
        this.y = y;
    }

    public bool Equals(CellKey other) => segmentId == other.segmentId && x == other.x && y == other.y;
    public override bool Equals(object obj) => obj is CellKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(segmentId, x, y);
}