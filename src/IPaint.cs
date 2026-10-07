using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerrainHoe;

public abstract class IPaint
{
    public static readonly Dictionary<Piece, IPaint> m_paintTools = new();
    private static readonly Dictionary<TerrainModifier.PaintType, IPaint> m_paintMask = new();
    private static readonly Dictionary<string, IPaint> m_paintToolNamed = new();
    public static bool IsPaintTool(Piece piece) => m_paintTools.ContainsKey(piece);
    public static bool TryGetPaintTool(TerrainModifier.PaintType type, out IPaint paintTool) => m_paintMask.TryGetValue(type, out paintTool);
    public static bool TryGetPaintTool(Piece piece, out IPaint paintTool) => m_paintTools.TryGetValue(piece, out paintTool);
    public static bool TryGetPaintTool(string name, out IPaint paintTool) => m_paintToolNamed.TryGetValue(name, out paintTool);

    protected readonly Piece piece;
    public readonly TerrainOp terrainOp;
    public bool adminOnly;
    public bool overrideAlpha = true;
    public bool blend = true;
    public bool isBiomePaint;
    public bool blendTerrain = true;
    public bool reset;

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class RegisterTerrainOp
    {
        private static void Prefix(ObjectDB __instance)
        {
            foreach (var paint in m_paintTools)
            {
                if (!__instance.m_terrainOps.Contains(paint.Value.terrainOp))
                {
                    __instance.m_terrainOps.Add(paint.Value.terrainOp);
                }
            }
        }
    }
    
    protected IPaint(string id, string name, TerrainModifier.PaintType type, int index = 1)
    {
        ItemDrop cultivator = PrefabManager.GetPrefab("Cultivator").GetComponent<ItemDrop>();
        ItemDrop hoe = PrefabManager.GetPrefab("Hoe").GetComponent<ItemDrop>();
        List<GameObject> pieces = cultivator.m_itemData.m_shared.m_buildPieces.m_pieces;
        GameObject replant = pieces[1];
        
        GameObject prefab = Object.Instantiate(replant.gameObject, MockManager.transform);
        prefab.name = id;
        piece = prefab.GetComponent<Piece>();
        piece.m_icon = hoe.m_itemData.m_shared.m_icons[0];
        piece.m_name = name;
        piece.m_description = name + "_desc";
        piece.m_vegetationGroundOnly = false;
        piece.m_category = Piece.PieceCategory.Misc;
        terrainOp = prefab.GetComponent<TerrainOp>();
        terrainOp.m_settings.m_paintType = type;
        terrainOp.m_settings.m_level = false;
        terrainOp.m_settings.m_smooth = false;
        terrainOp.m_settings.m_raise = false;
        terrainOp.m_settings.m_smoothPower = 0f;

        m_paintTools[piece] = this;
        m_paintMask[type] = this;
        m_paintToolNamed[piece.m_name] = this;

        terrainOp.m_settings.m_paintRadius = 5f;
        
        GameObject guardStone = PrefabManager.GetPrefab("dverger_guardstone");
        PrivateArea area = guardStone.GetComponent<PrivateArea>();
        GameObject marker = area.m_areaMarker.gameObject;
        GameObject instance = Object.Instantiate(marker, piece.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = "projector";
        instance.SetActive(true);

        var table = hoe.m_itemData.m_shared.m_buildPieces;
        if (table.m_pieces.Contains(prefab)) return;
        if (table.m_pieces.Count <= index) table.m_pieces.Add(prefab);
        else table.m_pieces.Insert(index, prefab);
    }
    
    public abstract Color GetColor();

    public virtual Color32 GetBiomeColor() => new Color32();

    public virtual Heightmap.Biome GetBiome() => Heightmap.Biome.None;
}