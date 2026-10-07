using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace TerrainHoe;

public static class PaintMan
{
    private static readonly Dictionary<string, TerrainModifier.PaintType> paintTypes;
    private static readonly Dictionary<TerrainModifier.PaintType, string> customPaintTypes;

    static PaintMan()
    {
        paintTypes = new Dictionary<string, TerrainModifier.PaintType>();
        customPaintTypes = new Dictionary<TerrainModifier.PaintType, string>();
    }

    public static void Init()
    {
        Harmony harmony = TerrainHoePlugin.instance._harmony;
        harmony.Patch(AccessTools.Method(typeof(Enum), nameof(Enum.GetValues)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_Enum_GetValues))));
        harmony.Patch(AccessTools.Method(typeof(Enum), nameof(Enum.GetNames)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_Enum_GetNames))));
        harmony.Patch(AccessTools.Method(typeof(Enum), nameof(Enum.GetName)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_Enum_GetName))));
        
        harmony.Patch(AccessTools.Method(typeof(TerrainComp), nameof(TerrainComp.PaintCleared)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_TerrainComp_PaintCleared))));
        harmony.Patch(AccessTools.Method(typeof(TerrainComp), nameof(TerrainComp.Awake)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_TerrainComp_Awake))));
        harmony.Patch(AccessTools.Method(typeof(TerrainComp), nameof(TerrainComp.Initialize)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_TerrainComp_Initialize))));
        harmony.Patch(AccessTools.Method(typeof(TerrainComp), nameof(TerrainComp.Save)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_TerrainComp_Save))));
        harmony.Patch(AccessTools.Method(typeof(TerrainComp), nameof(TerrainComp.Load)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_TerrainComp_Load))));
        
        harmony.Patch(AccessTools.Method(typeof(Heightmap), nameof(Heightmap.RebuildRenderMesh)),
            transpiler: new HarmonyMethod(AccessTools.Method(typeof(PaintMan),
                nameof(Transpile_Heightmap_RebuildRenderMesh))));

        harmony.Patch(AccessTools.Method(typeof(ClutterSystem), nameof(ClutterSystem.GetPatchBiomes)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan),
                nameof(Patch_ClutterSystem_GetPatchBiomes))));
        harmony.Patch(AccessTools.Method(typeof(ClutterSystem), nameof(ClutterSystem.GetGroundInfo)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_ClutterSystem_GetGroundInfo))));

        harmony.Patch(AccessTools.Method(typeof(Character), nameof(Character.UpdateLava)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PaintMan), nameof(Patch_Character_UpdateLava))));
    }

    public static bool TryGetPaintType(string name, out TerrainModifier.PaintType paintType)
    {
        if (Enum.TryParse(name, true, out paintType)) return true;
        if (paintTypes.TryGetValue(name,  out paintType)) return true;
        return false;
    }

    public static TerrainModifier.PaintType GetPaintType(string name)
    {
        if (Enum.TryParse(name, true, out TerrainModifier.PaintType type)) return type;
        if (paintTypes.TryGetValue(name, out type)) return type;

        Dictionary<TerrainModifier.PaintType, string> map = GetPaintTypes();
        foreach (KeyValuePair<TerrainModifier.PaintType, string> kvp in map)
        {
            if (kvp.Value != name) continue;
            type = kvp.Key;
            paintTypes[name] = type;
            return type;
        }

        type = (TerrainModifier.PaintType)name.GetStableHashCode();
        paintTypes[name] = type;
        customPaintTypes[type] = name;
        return type;
    }

    private static Dictionary<TerrainModifier.PaintType, string> GetPaintTypes()
    {
        Array values = Enum.GetValues(typeof(TerrainModifier.PaintType));
        string[] names = Enum.GetNames(typeof(TerrainModifier.PaintType));
        Dictionary<TerrainModifier.PaintType, string> map = new();
        for (int i = 0; i < values.Length; ++i)
        {
            map[(TerrainModifier.PaintType)values.GetValue(i)] = names[i];
        }

        foreach (KeyValuePair<TerrainModifier.PaintType, string> kvp in customPaintTypes)
        {
            map[kvp.Key] = kvp.Value;
        }
        return map;
    }

    private static void Patch_Enum_GetValues(Type enumType, ref Array __result)
    {
        if (enumType != typeof(TerrainModifier.PaintType)) return;
        if (paintTypes.Count == 0) return;
        TerrainModifier.PaintType[] f = new TerrainModifier.PaintType[__result.Length + paintTypes.Count];
        __result.CopyTo(f, 0);
        paintTypes.Values.CopyTo(f, __result.Length);
        __result = f;
    }

    private static bool Patch_Enum_GetName(Type enumType, object value, ref string __result)
    {
        if (enumType != typeof(TerrainModifier.PaintType)) return true;
        if (customPaintTypes.TryGetValue((TerrainModifier.PaintType)value, out string data))
        {
            __result = data;
            return false;
        }

        return true;
    }

    private static void Patch_Enum_GetNames(Type enumType, ref string[] __result)
    {
        if (enumType != typeof(TerrainModifier.PaintType)) return;
        if (paintTypes.Count == 0) return;
        __result = __result.AddRangeToArray(paintTypes.Keys.ToArray());
    }

    private static bool Patch_TerrainComp_PaintCleared(
        TerrainComp __instance,
        Vector3 worldPos,
        Vector3 rot,
        TerrainOp.Settings settings)
    {
        if (!IPaint.TryGetPaintTool(settings.m_paintType, out IPaint paint)) return true;
        
        TerrainColors terrainColors = __instance.GetComponent<TerrainColors>();
        if (settings.m_halfOffset)
        {
            worldPos.x -= 0.5f;
            worldPos.z -= 0.5f;
        }

        float heightOffset  = worldPos.y - __instance.transform.position.y;
        __instance.m_hmap.WorldToVertexMask(worldPos, out int x, out int y);
        float radiusInVertices = settings.m_paintRadius / __instance.m_hmap.m_scale;
        int vertexSearchRadius = Mathf.CeilToInt(radiusInVertices);
        Vector2 centerVertex = new Vector2(x, y);
            
        for (int i = y - vertexSearchRadius; 
             i <= y + vertexSearchRadius; 
             ++i)
        {
            UpdatePaints(
                __instance, 
                terrainColors,
                paint, 
                centerVertex, 
                x, 
                vertexSearchRadius, 
                i, 
                settings.m_paintHeightCheck, 
                heightOffset, 
                radiusInVertices,
                worldPos);
        }
        return false;
    }
    
    private static void UpdatePaints(
        TerrainComp comp, 
        TerrainColors terrainColors, 
        IPaint paint, 
        Vector2 centerVertex, 
        int centerVertex_x, 
        int vertexSearchRadius, 
        int vertex_y, 
        bool heightCheck, 
        float heightOffset, 
        float radiusInVertices,
        Vector3 worldPos)
    {
        for (int i = centerVertex_x - vertexSearchRadius; 
             i <= centerVertex_x + vertexSearchRadius; 
             ++i)
        {
            UpdatePaint(
                comp, 
                terrainColors,
                paint, 
                centerVertex, 
                i, 
                vertex_y, 
                heightCheck, 
                heightOffset, 
                radiusInVertices,
                worldPos);
        }
    }

    private static void UpdatePaint(
        TerrainComp comp, 
        TerrainColors terrainColors, 
        IPaint paint, 
        Vector3 centerVertex, 
        int vertex_x, 
        int vertex_y, 
        bool heightCheck, 
        float heightOffset, 
        float radiusInVertices,
        Vector3 worldPos)
    {
        float distanceFromCenter = Vector2.Distance(centerVertex, new Vector2(vertex_x, vertex_y));
        int gridStride = comp.m_width + 1;
                    
        bool withinBounds = vertex_x >= 0 && vertex_y >= 0 && vertex_x < gridStride && vertex_y < gridStride;
        bool passesHeightCheck = !heightCheck || comp.m_hmap.GetHeight(vertex_x, vertex_y) <= heightOffset;

        if (withinBounds && passesHeightCheck)
        {
            // int index = vertex_y * gridStride + vertex_x;
            int index = Util.GetIndex(vertex_x, vertex_y, comp.m_pitch);
            
            PaintMask(comp, paint, index, vertex_x, vertex_y, distanceFromCenter, radiusInVertices, worldPos);
            PaintTerrain(terrainColors, paint, index, vertex_x, vertex_y, distanceFromCenter, radiusInVertices, worldPos);
            if (paint.reset)
            {
                comp.m_modifiedHeight[index] = false;
                comp.m_levelDelta[index] = 0f;
                comp.m_smoothDelta[index] = 0f;

                // if (comp.TryGetNeighbor(worldPos, vertex_x, vertex_y, radiusInVertices) is {} neighbor)
                // {
                //     neighbor.GetNearestVertex(comp, vertex_x, vertex_y, out int x, out int y);
                //     var i = Util.GetIndex(neighbor, x, y);
                //     neighbor.m_modifiedHeight[i] = false;
                //     neighbor.m_levelDelta[i] = 0f;
                //     neighbor.m_smoothDelta[i] = 0f;
                // }
            }
        }
    }

    private static void PaintMask(
        TerrainComp comp, 
        IPaint paint, 
        int index, 
        int vertex_x, 
        int vertex_y, 
        float distanceFromCenter, 
        float radiusInVertices,
        Vector3 worldPos)
    {
        if (paint.reset)
        {
            comp.m_modifiedPaint[index] = false;
            comp.m_paintMask[index] = new Color(0f, 0f, 0f, 1f);
            
            // if (comp.TryGetNeighbor(worldPos, vertex_x, vertex_y, radiusInVertices) is { } neighbor)
            // {
            //     neighbor.GetNearestVertex(comp, vertex_x, vertex_y, out int x, out int y);
            //     var i = Util.GetIndex(neighbor, x, y);
            //     neighbor.m_modifiedPaint[i] = false;
            //     neighbor.m_paintMask[i] = new Color(0f, 0f, 0f, 1f);
            // }
            
        }
        else
        {
            Color color;
            Color current = comp.m_hmap.m_doLateUpdate != 1 ? 
                comp.m_hmap.GetPaintMask(vertex_x, vertex_y) : 
                comp.m_paintMask[index];

            if (paint.blend)
            {
                float blendWeight = Mathf.Pow(1f - Mathf.Clamp01(distanceFromCenter / radiusInVertices), 0.1f);
                float alpha = current.a;
                color = Color.Lerp(current, paint.GetColor(), blendWeight);
                if (!paint.overrideAlpha) color.a = alpha;
            }
            else
            {
                color = paint.GetColor();
                if (!paint.overrideAlpha) color.a = current.a;
            }
        
            comp.m_modifiedPaint[index] = true;
            comp.m_paintMask[index] = color;

            // if (comp.TryGetNeighbor(worldPos, vertex_x, vertex_y, radiusInVertices) is { } neighbor)
            // {
            //     neighbor.GetNearestVertex(comp, vertex_x, vertex_y, out int x, out int y);
            //     var i = Util.GetIndex(neighbor, x, y);
            //     neighbor.m_modifiedPaint[i] = true;
            //     neighbor.m_paintMask[i] = color;
            // }
        }
    }

    private static void PaintTerrain(
        TerrainColors colors, 
        IPaint paint, 
        int index, 
        int vertex_x, 
        int vertex_y,
        float distanceFromCenter, 
        float radiusInVertices, 
        Vector3 worldPos)
    {
        if (!paint.isBiomePaint || colors == null) return;
        
        if (paint.reset)
        {
            colors.ResetTerrain(index);
        }
        else
        {
            Color32 color;
            if (paint.blendTerrain)
            {
                float weight = Mathf.Pow(1f - Mathf.Clamp01(distanceFromCenter / radiusInVertices), 0.1f);
                Color32 current = colors.GetCurrentBiomeColor(index, vertex_x, vertex_y);
                color = Color32.Lerp(current, paint.GetBiomeColor(), weight);
            }
            else
            {
                color = paint.GetBiomeColor();
            }
                
            colors.SetBiomeColor(index, color);
        }
    }

    private static List<TerrainComp> GetNeighbors(this TerrainComp comp, Vector3 worldPos, int x, int y, float radius)
    {
        List<TerrainComp> neighbors = [];

        if (x == 0)
        {
            if (comp.TryGetNeighbor(worldPos, 0, 1, radius) is {} n1)
            {
                neighbors.Add(n1);
            }

            if (y == 0)
            {
                if (comp.TryGetNeighbor(worldPos, 0, 0, radius) is { } n2)
                {
                    neighbors.Add(n2);
                }

                if (comp.TryGetNeighbor(worldPos, 1, 0, radius) is { } n3)
                {
                    neighbors.Add(n3);
                }
            }
            else
            {
                if (y == comp.m_width)
                {
                    if (comp.TryGetNeighbor(worldPos, 0, 2, radius) is { } n4)
                    {
                        neighbors.Add(n4);
                    }

                    if (comp.TryGetNeighbor(worldPos, 1, 2, radius) is { } n5)
                    {
                        neighbors.Add(n5);
                    }
                }
            }
        }
        else
        {
            if (x == comp.m_width)
            {
                if (comp.TryGetNeighbor(worldPos, 2, 1, radius) is { } n6)
                {
                    neighbors.Add(n6);
                }

                if (y == 0)
                {
                    if (comp.TryGetNeighbor(worldPos, 2, 0, radius) is { } n7)
                    {
                        neighbors.Add(n7);
                    }

                    if (comp.TryGetNeighbor(worldPos, 1, 0, radius) is { } n8)
                    {
                        neighbors.Add(n8);
                    }
                }
                else
                {
                    if (y == comp.m_width)
                    {
                        if (comp.TryGetNeighbor(worldPos, 2, 2, radius) is { } n9)
                        {
                            neighbors.Add(n9);
                        }

                        if (comp.TryGetNeighbor(worldPos, 1, 2, radius) is { } n10)
                        {
                            neighbors.Add(n10);
                        }
                    }
                }
            }
            else
            {
                if (y == 0)
                {
                    if (comp.TryGetNeighbor(worldPos, 1, 0, radius) is { } n11)
                    {
                        neighbors.Add(n11);
                    }
                }
                else
                {
                    if (y == comp.m_width)
                    {
                        if (comp.TryGetNeighbor(worldPos, 1, 2, radius) is { } n12)
                        {
                            neighbors.Add(n12);
                        }
                    }
                }
            }
        }
        
        
        return neighbors;
    }
    
    private static void Patch_TerrainComp_Awake(TerrainComp __instance) => __instance.gameObject.AddComponent<TerrainColors>();

    private static void Patch_TerrainComp_Initialize(TerrainComp __instance) => __instance.GetComponent<TerrainColors>()?.Initialize();
    
    private static void Patch_TerrainComp_Save(TerrainComp __instance) => __instance.GetComponent<TerrainColors>()?.Save();

    private static void Patch_TerrainComp_Load(TerrainComp __instance) => __instance.GetComponent<TerrainColors>()?.Load();

    private static IEnumerable<CodeInstruction> Transpile_Heightmap_RebuildRenderMesh(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target = AccessTools.Method(typeof(Mesh), nameof(Mesh.GetVertices));
        MethodInfo insert = AccessTools.Method(typeof(PaintMan), nameof(Apply_TerrainColor_Modifiers));
        CodeInstruction[] newInstructions =
        {
            new(OpCodes.Ldarg_0), 
            new(OpCodes.Call, insert)
        };
            
        return new CodeMatcher(instructions)
            .Start()
            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, target))
            .Insert(newInstructions)
            .InstructionEnumeration();
    }
    private static void Apply_TerrainColor_Modifiers(Heightmap heightmap)
    {
        if (heightmap.m_isDistantLod) return;
        
        if (!TerrainColors.TryFindTerrainColors(heightmap.transform.position, out TerrainColors terrainColors)) return;
        if (!terrainColors.m_initialized) return;

        int num = heightmap.m_width + 1;
        for (int x = 0; x < num; ++x)
        {
            for (int y = 0; y < num; ++y)
            {
                int index = x * num + y;
                bool modified = terrainColors.m_modifiedTerrain[index];
                if (!modified) continue;
                Color32 color = terrainColors.m_terrainMask[index];
                Heightmap.s_tempColors[index] = color;
            }
        }
    }

    private static Heightmap.Biome GetBiomeFromMesh(
        this Heightmap hm, 
        Vector3 point, 
        float oceanLevel = 0.02f, 
        bool waterAlwaysOcean = false)
    {
        if (hm.m_isDistantLod | waterAlwaysOcean)
        {
            return WorldGenerator.instance.GetBiome(point.x, point.z, oceanLevel, waterAlwaysOcean);
        }

        if (!TerrainColors.TryFindTerrainColors(hm.transform.position, out TerrainColors component))
        {
            return hm.GetBiome(point);
        }

        return component.GetBiome(hm, point);
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
    


    private static bool Patch_ClutterSystem_GetPatchBiomes(Vector3 center, float halfSize, ref Heightmap.Biome __result)
    {
        Heightmap.Biome biome = GetPatchBiomesByMesh(center, halfSize);
        if (biome == Heightmap.Biome.None) return true;
        __result = biome;
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

    private static bool Patch_ClutterSystem_GetGroundInfo(
        ClutterSystem __instance, 
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
    
    private static bool GetGroundInfoByMesh(
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

    private static void Patch_Character_UpdateLava(Character __instance, float dt)
    {
        if (__instance.m_tolerateFire) return;
            
        if (!TerrainColors.TryFindTerrainColors(__instance.transform.position, out TerrainColors colors)) return;
        if (!colors.m_initialized) return;
        
        colors.m_terrainComp.m_hmap.WorldToVertex(__instance.transform.position, out int x, out int y);
        int index = y * (colors.m_width + 1) + x;
        bool modified = colors.m_modifiedTerrain[index];
        if (!modified)
        {
            if (!WorldGenerator.IsAshlands(__instance.transform.position.x, __instance.transform.position.z))
            {
                __instance.m_lavaHeatLevel -= dt * __instance.m_heatCooldownBase;
            }
        }
        else
        {
            Color32 color = colors.m_terrainMask[index];
            Color paint = colors.m_terrainComp.m_paintMask[index];
            bool isLava = color.r > 200 && color.a > 200 && paint.a > 0.6f;
            
            if (isLava)
            {
                __instance.m_lavaHeatLevel += dt * __instance.m_heatBuildupBase *
                                              (1f - __instance.GetEquipmentHeatResistanceModifier());
            }
            else
            {
                __instance.m_lavaHeatLevel -= dt * __instance.m_heatCooldownBase;
            }
        }
            
        __instance.m_lavaHeatLevel = Mathf.Clamp(__instance.m_lavaHeatLevel, 0.0f, 1.0f);
    }
}