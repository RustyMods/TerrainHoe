using System;
using System.Xml.XPath;
using HarmonyLib;
using UnityEngine;
using Object = System.Object;
using Random = UnityEngine.Random;

namespace TerrainHoe;

public static class GrassMan
{
    [HarmonyPatch(typeof(ClutterSystem), nameof(ClutterSystem.GetPatchBiomes))]
    private static class ClutterSystem_GetPatchBiomes_Prefix
    {
        private static bool Prefix(Vector3 center, float halfSize, ref Heightmap.Biome __result)
        {
            Heightmap.Biome biome = GetPatchBiomesByMesh(center, halfSize);
            if (biome == Heightmap.Biome.None) return true;
            __result = biome;
            return false;
        }
    }

    [HarmonyPatch(typeof(ClutterSystem), nameof(ClutterSystem.GetGroundInfo))]
    private static class ClutterSystem_GetGroundInfo_Prefix
    {
        private static bool Prefix(ClutterSystem __instance,
            Vector3 p,
            out Vector3 point,
            out Vector3 normal,
            out Heightmap hmap,
            out Heightmap.Biome biome,
            ref bool __result)
        {
            var result = __instance.GetGroundInfoByMesh(p, out point, out normal, out hmap, out biome);
            if (biome == Heightmap.Biome.None) return true;
            __result = result;
            return false;
        }
    }
    
    public static bool GetGroundInfoByMesh(
        this ClutterSystem cs, 
        Vector3 p, 
        out Vector3 point, 
        out Vector3 normal, 
        out Heightmap hmap,
        out Heightmap.Biome biome)
    {
        Vector3 origin = p + Vector3.up * 500f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, cs.m_placeRayMask))
        {
            point = hit.point;
            normal = hit.normal;
            hmap = hit.collider.GetComponent<Heightmap>();
            biome = hmap.GetBiomeFromMesh(point);
            return true;
        }

        point = p;
        normal = Vector3.up;
        hmap = null;
        biome = Heightmap.Biome.None;
        return false;
    }
    
    private static Heightmap.Biome GetPatchBiomesByMesh(Vector3 center, float halfSize)
    {
        Heightmap.Biome bc1 = GetBiomeFromMeshColor(new Vector3(center.x - halfSize, 0.0f, center.z - halfSize));
        Heightmap.Biome bc2 = GetBiomeFromMeshColor(new Vector3(center.x + halfSize, 0.0f, center.z - halfSize));
        Heightmap.Biome bc3 = GetBiomeFromMeshColor(new Vector3(center.x - halfSize, 0.0f, center.z + halfSize));
        Heightmap.Biome bc4 = GetBiomeFromMeshColor(new Vector3(center.x + halfSize, 0.0f, center.z + halfSize));
        
        if (bc1 == Heightmap.Biome.None ||
            bc2 == Heightmap.Biome.None ||
            bc3 == Heightmap.Biome.None ||
            bc4 == Heightmap.Biome.None)
        {
            return Heightmap.Biome.None;
        }
        return bc1 | bc2 | bc4 | bc3;
    }
    
    private static Heightmap.Biome GetBiomeFromMeshColor(Vector3 point)
    {
        if (ZoneSystem.instance && !ZoneSystem.instance.IsZoneLoaded(point))
        {
            return Heightmap.Biome.None;
        }
        Heightmap hm = Heightmap.FindHeightmap(point);
        if (hm == null) return Heightmap.Biome.None;
        return hm.GetBiomeFromMesh(point);
    }

    [HarmonyPatch(typeof(ClutterSystem), nameof(ClutterSystem.GenerateVegPatch))]
    private static class ClutterSystem_GenerateVegPatch_Prefix
    {
        private static bool Prefix(ClutterSystem __instance, Vector2Int patchID, float size,
            ref ClutterSystem.PatchData __result)
        {
            if (!Player.m_localPlayer) return true;
            
            Vector3 vegPatchCenter = __instance.GetVegPatchCenter(patchID);
            float halfSize = size / 2f;
            bool forceGrass = false;

            if (TerrainColors.TryFindTerrainColors(vegPatchCenter, out var colors))
            {
                forceGrass = colors.HasForcedGrassVertex(vegPatchCenter, halfSize);
            }

            if (!forceGrass) return true;
            
            __result = GenerateVegPatch(__instance, patchID, size);
            return false;
        }
    }

    public static ClutterSystem.PatchData GenerateVegPatch(ClutterSystem __instance, Vector2Int patchID, float size)
    {
        Vector3 vegPatchCenter = __instance.GetVegPatchCenter(patchID);
        float halfSize = size / 2f;
        Heightmap.Biome patchBiomes = __instance.GetPatchBiomes(vegPatchCenter, halfSize);
        if (patchBiomes == Heightmap.Biome.None)
        {
            return null;
        }

        Random.State state = Random.state;
        ClutterSystem.PatchData vegPatch = __instance.AllocatePatch();
        vegPatch.center = vegPatchCenter;

        for (int i = 0; i < __instance.m_clutter.Count; ++i)
        {
            ClutterSystem.Clutter clutter = __instance.m_clutter[i];
            if (!clutter.m_enabled) continue;
            var isMatchingBiome = (patchBiomes & clutter.m_biome) != Heightmap.Biome.None;
            if (!isMatchingBiome) continue;
            
            int seed = patchID.x * (patchID.y * 1374) + i * 9321;
            Random.InitState(seed);
            Vector3 fractalPos = new Vector3(clutter.m_fractalOffset, 0.0f, 0.0f);
            float maxTilt = Mathf.Cos((float)Math.PI / 180f * clutter.m_maxTilt);
            float minTilt = Mathf.Cos((float)Math.PI / 180f * clutter.m_minTilt);
            int quality = __instance.m_quality switch
            {
                ClutterSystem.Quality.Low => clutter.m_amount / 4,
                ClutterSystem.Quality.Med => clutter.m_amount / 2,
                _ => clutter.m_amount
            };
            int amount = (int)(quality * __instance.m_amountScale);
            
            GenerateVeg(__instance, vegPatchCenter, halfSize, amount, fractalPos, maxTilt, minTilt, clutter, vegPatch);
        }
        
        Random.state = state;
        return vegPatch;
    }

    private static void GenerateVeg(ClutterSystem __instance, 
        Vector3 vegPatchCenter, 
        float halfSize, 
        float amount, 
        Vector3 fractalPos, 
        float maxTilt, 
        float minTilt, 
        ClutterSystem.Clutter clutter, 
        ClutterSystem.PatchData vegPatch)
    {
        for (int i = 0; i < amount; ++i)
        {
            var x = Random.Range(vegPatchCenter.x - halfSize, vegPatchCenter.x + halfSize);
            var z = Random.Range(vegPatchCenter.z - halfSize, vegPatchCenter.z + halfSize);
            var position = new Vector3(x, 0.0f, z);
            float y = Random.Range(0, 360);
            
            if (clutter.m_inForest)
            {
                float forestFactor = WorldGenerator.GetForestFactor(position);
                if (forestFactor < clutter.m_forestTresholdMin || forestFactor > clutter.m_forestTresholdMax)
                {
                    continue;
                }
            }

            // if (clutter.m_fractalScale > 0.0 && !isCustomTerrain)
            // {
            //     float fractalScale = Utils.Fbm(position * 0.01f * clutter.m_fractalScale + fractalPos, 3, 1.6f, 0.7f);
            //     if (fractalScale < clutter.m_fractalTresholdMin || fractalScale > clutter.m_fractalTresholdMax)
            //     {
            //         continue;
            //     }
            // }

            var hasGroundInfo = __instance.GetGroundInfo(position, out var point, out var normal, out var hmap, out var biome);
            if (!hasGroundInfo) continue;
            
            var isMatchingBiome = (clutter.m_biome & biome) != Heightmap.Biome.None;
            if (!isMatchingBiome)
            {
                continue;
            }
            
            // float altitude = point.y - __instance.m_waterLevel;
            // var withinAltitudeRange = altitude >= clutter.m_minAlt && altitude <= clutter.m_maxAlt;
            //
            // if (!withinAltitudeRange && !isCustomTerrain) continue;
            
            var withinTiltRange = normal.y >= maxTilt && normal.y <= minTilt;
            if (!withinTiltRange) continue;
            
            if (!Mathf.Approximately(clutter.m_minOceanDepth, clutter.m_maxOceanDepth))
            {
                float oceanDepth = hmap.GetOceanDepth(position);
                if (oceanDepth < clutter.m_minOceanDepth || oceanDepth > clutter.m_maxOceanDepth)
                {
                    continue;
                }
            }

            if (!Mathf.Approximately(clutter.m_minVegetation, clutter.m_maxVegetation))
            {
                float vegetationMask = hmap.GetVegetationMask(position);
                if (vegetationMask > clutter.m_maxVegetation || vegetationMask < clutter.m_minVegetation)
                {
                    continue;
                }
            }

            if (!clutter.m_onCleared || !clutter.m_onUncleared)
            {
                bool isCleared = hmap.IsCleared(point);
                if (clutter.m_onCleared && !isCleared || clutter.m_onUncleared & isCleared)
                {
                    continue;
                }
            }
            
            SpawnVeg(__instance, vegPatchCenter, y, normal, point, clutter, vegPatch);
        }
    }

    private static void SpawnVeg(ClutterSystem __instance, 
        Vector3 vegPatchCenter, 
        float y, 
        Vector3 normal, 
        Vector3 point, 
        ClutterSystem.Clutter clutter, 
        ClutterSystem.PatchData vegPatch)
    {
        Vector3 position = point;
        if (clutter.m_snapToWater)
        {
            position.y = __instance.m_waterLevel;
        }

        if (clutter.m_randomOffset != 0.0)
        {
            position.y += Random.Range(-clutter.m_randomOffset, clutter.m_randomOffset);
        }

        var rotation = !clutter.m_terrainTilt
            ? Quaternion.Euler(0.0f, y, 0.0f)
            : Quaternion.AngleAxis(y, normal);

        if (clutter.m_instanced)
        {
            GameObject prefab = UnityEngine.Object.Instantiate(clutter.m_prefab,
                vegPatchCenter, Quaternion.identity, __instance.m_grassRoot.transform);
            var instanceRenderer = prefab.GetComponent<InstanceRenderer>();
            if (instanceRenderer.m_lodMaxDistance >
                __instance.m_distance - __instance.m_grassPatchSize / 2.0)
            {
                vegPatch.m_objects.Add(prefab);
            }
                    
            float scale = Random.Range(clutter.m_scaleMin, clutter.m_scaleMax);
            instanceRenderer.AddInstance(position, rotation, scale);
        }
        else
        {
            GameObject prefab = UnityEngine.Object.Instantiate(clutter.m_prefab, position,
                rotation, __instance.m_grassRoot.transform);
            vegPatch.m_objects.Add(prefab);
        }
    }
}