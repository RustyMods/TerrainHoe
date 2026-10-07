using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainHoe;

public class TerrainColors : MonoBehaviour
{
    private static readonly List<TerrainColors> instances = [];
    public int m_width;
    public bool[] m_modifiedTerrain;
    public Color32[] m_terrainMask;
    public bool[] m_forceGrass;
    
    public bool m_initialized;
    
    public TerrainComp m_terrainComp;
    public ZNetView m_nview;
    
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_terrainComp = GetComponent<TerrainComp>();
        instances.Add(this);
    }

    public void OnDestroy()
    {
        instances.Remove(this);
    }

    public void Initialize()
    {
        m_width = m_terrainComp.m_hmap.m_width;
        int num = m_width + 1;
        m_modifiedTerrain = new bool[num * num];
        m_terrainMask = new Color32[num * num];
        m_forceGrass = new bool[num * num];
        m_initialized = true;
    }

    public void Save()
    {
        if (!m_initialized || !m_nview.IsValid() || !m_nview.IsOwner()) return;

        ZPackage pkg = new ZPackage();
        pkg.Write(m_modifiedTerrain.Length);
        for (int i = 0; i < m_modifiedTerrain.Length; ++i)
        {
            bool modified = m_modifiedTerrain[i];
            pkg.Write(modified);
            bool forceGrass = m_forceGrass[i];
            pkg.Write(forceGrass);
            if (modified)
            {
                Color32 color = m_terrainMask[i];
                pkg.Write(color.r);
                pkg.Write(color.g);
                pkg.Write(color.b);
                pkg.Write(color.a);
            }
        }

        byte[] bytes = Utils.Compress(pkg.GetArray());
        m_nview.GetZDO().Set(PaintVars.TerrainColors, bytes);
    }

    public void Load()
    {
        byte[] bytes = m_nview.GetZDO().GetByteArray(PaintVars.TerrainColors);
        if (bytes == null) return;
        byte[] decompressed = Utils.Decompress(bytes);
        ZPackage pkg = new ZPackage(decompressed);
        int length = pkg.ReadInt();
        for (int i = 0; i < length; ++i)
        {
            bool modified = pkg.ReadBool();
            bool forceGrass = pkg.ReadBool();
            m_modifiedTerrain[i] = modified;
            m_forceGrass[i] = forceGrass;
            if (modified)
            {
                byte r = pkg.ReadByte();
                byte g = pkg.ReadByte();
                byte b = pkg.ReadByte();
                byte a = pkg.ReadByte();
                m_terrainMask[i] = new Color32(r, g, b, a);
            }
            else
            {
                m_terrainMask[i] = new Color32();
            }
        }
    }

    public void SetBiomeColor(int index, Color32 color, bool forceGrass = false)
    {
        if (!m_initialized) return;
        m_modifiedTerrain[index] = true;
        m_terrainMask[index] = color;
        m_forceGrass[index] = forceGrass;
    }

    public void ResetTerrain(int index)
    {
        if (!m_initialized) return;
        m_modifiedTerrain[index] = false;
        m_terrainMask[index] = new Color32();
        m_forceGrass[index] = false;
    }

    public void ApplyToHeightmap(Heightmap hm)
    {
        if (!m_initialized) return;
        
        List<Color32> colors = new List<Color32>();
        hm.m_renderMesh.GetColors(colors);
            
        int pitch = hm.m_width + 1;
        for (int x = 0; x < pitch; ++x)
        {
            for (int y = 0; y < pitch; ++y)
            {
                int index = x * pitch + y;
                bool modified = m_modifiedTerrain[index];
                if (modified)
                {
                    Color32 color = m_terrainMask[index];
                    colors[index] = color;
                }
            }
        }
            
        hm.m_renderMesh.SetColors(colors);
    }

    public Color32 GetCurrentBiomeColor(int index, int x, int y)
    {
        bool modified = m_modifiedTerrain[index];
        if (modified) return m_terrainMask[index];
        return m_terrainComp.m_hmap.GetBiomeColor(x, y);
    }

    public Heightmap.Biome GetBiome(Heightmap hm, Vector3 point)
    {
        try
        {
            hm.WorldToVertex(point, out int x, out int y);
            int index = y * (hm.m_width + 1) + x;
            bool modified = m_modifiedTerrain[index];
            if (!modified) return hm.GetBiome(point);
            Color32 color = m_terrainMask[index];
            if (color is { r: > 200, a: > 200 }) return Heightmap.Biome.AshLands;
            if (color is { b: > 200, a: > 200 }) return Heightmap.Biome.Mistlands;

            if (color.r > 200) return Heightmap.Biome.Swamp;
            if (color.g > 200) return Heightmap.Biome.Mountain | Heightmap.Biome.DeepNorth;
            if (color.b > 200) return Heightmap.Biome.BlackForest;
            if (color.a > 200) return Heightmap.Biome.Plains;
            return Heightmap.Biome.Meadows;
        }
        catch (IndexOutOfRangeException)
        {
            return hm.GetBiome(point);
        }
    }
    
    public bool HasModifiedVertex(Vector3 center, float halfSize)
    {
        if (!m_initialized) return false;

        Heightmap hm = m_terrainComp.m_hmap;
        int pitch = hm.m_width + 1;

        hm.WorldToVertex(new Vector3(center.x - halfSize, 0f, center.z - halfSize), out int minX, out int minY);
        hm.WorldToVertex(new Vector3(center.x + halfSize, 0f, center.z + halfSize), out int maxX, out int maxY);

        minX = Mathf.Max(0, minX);
        minY = Mathf.Max(0, minY);
        maxX = Mathf.Min(pitch - 1, maxX);
        maxY = Mathf.Min(pitch - 1, maxY);

        for (int y = minY; y <= maxY; ++y)
        {
            int row = y * pitch;
            for (int x = minX; x <= maxX; ++x)
            {
                if (m_modifiedTerrain[row + x]) return true;
            }
        }
        return false;
    }
    
    public bool HasForcedGrassVertex(Vector3 center, float halfSize)
    {
        if (!m_initialized) return false;

        Heightmap hm = m_terrainComp.m_hmap;
        int pitch = hm.m_width + 1;

        hm.WorldToVertex(new Vector3(center.x - halfSize, 0f, center.z - halfSize), out int minX, out int minY);
        hm.WorldToVertex(new Vector3(center.x + halfSize, 0f, center.z + halfSize), out int maxX, out int maxY);

        minX = Mathf.Max(0, minX);
        minY = Mathf.Max(0, minY);
        maxX = Mathf.Min(pitch - 1, maxX);
        maxY = Mathf.Min(pitch - 1, maxY);

        for (int y = minY; y <= maxY; ++y)
        {
            int row = y * pitch;
            for (int x = minX; x <= maxX; ++x)
            {
                if (m_forceGrass[row + x]) return true;
            }
        }
        return false;
    }
    
    public static bool TryFindTerrainColors(Vector3 pos, out TerrainColors instance)
    {
        for (int i = 0; i < instances.Count; ++i)
        {
            TerrainColors terrain = instances[i];
            float halfSize = terrain.m_terrainComp.m_size / 2f;
            Vector3 position = terrain.transform.position;
            
            if (pos.x >= position.x - halfSize && 
                pos.x <= position.x + halfSize && 
                pos.z >= position.z - halfSize &&
                pos.z <= position.z + halfSize)
            {
                instance = terrain;
                return true;
            }
        }
        instance = null;
        return false;
    }

    public static TerrainColors FindTerrainColors(Vector3 pos)
    {
        for (int i = 0; i < instances.Count; ++i)
        {
            TerrainColors instance = instances[i];
            float halfSize = instance.m_terrainComp.m_size / 2f;
            Vector3 position = instance.transform.position;
            
            if (pos.x >= position.x - halfSize && 
                pos.x <= position.x + halfSize && 
                pos.z >= position.z - halfSize &&
                pos.z <= position.z + halfSize)
            {
                return instance;
            }
        }
        return null;
    }
}