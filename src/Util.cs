using UnityEngine;

namespace TerrainHoe;

public static class Util
{
    public static Color GetColor(Heightmap hmap, TerrainComp tc, int x, int y, int index)
    {
        return hmap.m_doLateUpdate != 1 ? hmap.GetPaintMask(x, y) : tc.m_paintMask[index];
    }

    public static void SetNeighborHeights(TerrainComp tc, int x, int y)
    {
        if (x < 0 || x > tc.m_width || y < 0 || y > tc.m_width) return;
        int index = GetIndex(tc, x, y);
        TerrainComp.m_neighborHeights.Add(tc.m_paintMask[index].g);
    }
    public static int GetIndex(TerrainComp tc, int x, int y) => GetIndex(x, y, tc.m_pitch);
    public static int GetIndex(int x, int y, int pitch) => y * pitch + x;

    extension(TerrainComp comp)
    {
        private bool TryGetNeighbor(Vector3 worldPos, int ix, int iy, float radius, out TerrainComp neighbor)
        {
            neighbor = comp.TryGetNeighbor(worldPos, ix, iy, radius);
            return neighbor != null;
        }

        private bool TryGetNeighborColor(Vector3 worldPos, int ix, int iy, float radius,
            out TerrainColors colors)
        {
            colors = null;
            return comp.TryGetNeighbor(worldPos, ix, iy, radius, out TerrainComp neighbor) &&
                   neighbor.TryGetComponent(out colors);
        }
    }
}