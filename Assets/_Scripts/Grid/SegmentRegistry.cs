using System.Collections.Generic;

public static class SegmentRegistry
{
    private static Dictionary<int, SegmentGridSettings> segments = new();

    public static void Register(SegmentGridSettings seg)
    {
        segments[seg.GetInstanceID()] = seg;
    }

    public static void Unregister(SegmentGridSettings seg)
    {
        segments.Remove(seg.GetInstanceID());
    }

    public static SegmentGridSettings Get(int id)
    {
        segments.TryGetValue(id, out var seg);
        return seg;
    }
}