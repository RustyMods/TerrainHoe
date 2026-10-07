using System;
using System.Reflection;
using UnityEngine;

namespace TerrainHoe;

public static class TerrainHoe_API
{
    private static readonly MethodInfo API_GetBiome;
    private static readonly MethodInfo API_GetHeightmapBiome;
    private static bool isLoaded;

    public static bool IsLoaded() => isLoaded;
    
    static TerrainHoe_API()
    {
        isLoaded = false;
        Type type = Type.GetType("TerrainHoe.API, TerrainHoe");
        if (type != null)
        {
            isLoaded = true;
            API_GetBiome = type.GetMethod("GetBiome", BindingFlags.Static | BindingFlags.Public);
            API_GetHeightmapBiome = type.GetMethod("GetHeightmapBiome", BindingFlags.Static | BindingFlags.Public);
        }
    }
    public static Heightmap.Biome GetBiome(Vector3 position)
    {
        return (Heightmap.Biome)(API_GetBiome.Invoke(null, [position]) ?? (Heightmap.Biome)0);
    }
    public static Heightmap.Biome GetBiome(Heightmap heightmap, Vector3 position)
    {
        return (Heightmap.Biome)(API_GetHeightmapBiome.Invoke(null, [heightmap, position]) ?? heightmap.GetBiome(position, 0.02f, false));
    }

}

public static class API
{
    public static Heightmap.Biome GetBiome(Vector3 position)
    {
        return Heightmap.Biome.Meadows;
    }

    public static Heightmap.Biome GetBiome(Heightmap heightmap, Vector3 position)
    {
        return Heightmap.Biome.Meadows;
    }
}