using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerrainHoe;

public abstract class IPaint
{
    public static readonly Dictionary<Piece, IPaint> m_paintTools = new();
    private static readonly Dictionary<TerrainModifier.PaintType, IPaint> m_paintMask = new();
    private static readonly Dictionary<string, IPaint> m_paintToolNamed = new();

    public static bool IsPaintTool(Piece piece)
    {
        return m_paintTools.ContainsKey(piece);
    }

    public static bool TryGetPaintTool(TerrainModifier.PaintType type, out IPaint paintTool)
    {
        return m_paintMask.TryGetValue(type, out paintTool);
    }

    public static bool TryGetPaintTool(Piece piece, out IPaint paintTool)
    {
        return m_paintTools.TryGetValue(piece, out paintTool);
    }

    public static bool TryGetPaintTool(string name, out IPaint paintTool)
    {
        return m_paintToolNamed.TryGetValue(name, out paintTool);
    }

    protected readonly Piece piece;
    public readonly TerrainOp terrainOp;
    public bool adminOnly;
    public bool overrideAlpha = true;
    public bool blend = true;
    public bool isBiomePaint;
    public bool blendTerrain = true;
    public bool forceGrass = false;
    public bool reset;

    public readonly RequiredResourcesList RequiredItems = new();
    public ConfigEntry<string> cost = null!;

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class RegisterTerrainOp
    {
        private static void Prefix(ObjectDB __instance)
        {
            foreach (var paint in m_paintTools)
            {
                if (!__instance.m_terrainOps.Contains(paint.Value.terrainOp))
                    __instance.m_terrainOps.Add(paint.Value.terrainOp);
            }
        }
    }

    protected IPaint(string id, string name, TerrainModifier.PaintType type, int index = 1)
    {
        var cultivator = PrefabManager.GetPrefab("Cultivator").GetComponent<ItemDrop>();
        var hoe = PrefabManager.GetPrefab("Hoe").GetComponent<ItemDrop>();
        var pieces = cultivator.m_itemData.m_shared.m_buildPieces.m_pieces;
        var replant = pieces[1];

        var prefab = Object.Instantiate(replant.gameObject, MockManager.transform);
        prefab.name = id;
        piece = prefab.GetComponent<Piece>();
        piece.m_icon = hoe.m_itemData.m_shared.m_icons[0];
        piece.m_name = name;
        piece.m_description = name + "_desc";
        piece.m_vegetationGroundOnly = false;
        piece.m_canRotate = false;
        piece.m_category = Piece.PieceCategory.Misc;
        piece.m_usage = Piece.UsageTagFlags.Misc;
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

        var guardStone = PrefabManager.GetPrefab("dverger_guardstone");
        var area = guardStone.GetComponent<PrivateArea>();
        var marker = area.m_areaMarker.gameObject;
        var instance = Object.Instantiate(marker, piece.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = "projector";
        instance.SetActive(true);

        var table = hoe.m_itemData.m_shared.m_buildPieces;
        if (table.m_pieces.Contains(prefab)) return;
        if (table.m_pieces.Count <= index) table.m_pieces.Add(prefab);
        else table.m_pieces.Insert(index, prefab);
    }

    public void SetupConfigs()
    {
        var englishName = piece.m_name;

        ConfigEntry<string> itemConfig(string name, string value, string desc)
        {
            ConfigurationManagerAttributes attributes = new() { CustomDrawer = DrawConfigTable };
            return TerrainHoePlugin.instance.config(englishName, name, value,
                new ConfigDescription(desc, null, attributes));
        }

        cost = itemConfig("Cost", new SerializedRequirements(RequiredItems.Requirements).ToString(), "Item cost to use tool");

        void onConfigChanged(object sender, EventArgs args)
        {
            var requirements = SerializedRequirements.toPieceReqs(new SerializedRequirements(cost.Value));
            piece.m_resources = requirements;
        }

        cost.SettingChanged += onConfigChanged;
        onConfigChanged(null, null);
    }

    public abstract Color GetColor();

    public virtual Color32 GetBiomeColor()
    {
        return new Color32();
    }

    public virtual Heightmap.Biome GetBiome()
    {
        return Heightmap.Biome.None;
    }


    private static void DrawConfigTable(ConfigEntryBase cfg)
    {
        var locked = cfg.Description.Tags
            .Select(a =>
                a.GetType().Name == "ConfigurationManagerAttributes"
                    ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a)
                    : null).FirstOrDefault(v => v != null) ?? false;

        List<Requirement> newReqs = [];
        var wasUpdated = false;

        GUILayout.BeginVertical();
        foreach (var req in new SerializedRequirements((string)cfg.BoxedValue).Reqs)
        {
            GUILayout.BeginHorizontal();

            var amount = req.amount;
            if (int.TryParse(
                    GUILayout.TextField(amount.ToString(), new GUIStyle(GUI.skin.textField) { fixedWidth = 40 }),
                    out var newAmount) && newAmount != amount && !locked)
            {
                amount = newAmount;
                wasUpdated = true;
            }

            var newItemName = GUILayout.TextField(req.itemName,
                new GUIStyle(GUI.skin.textField));
            var itemName = locked ? req.itemName : newItemName;
            wasUpdated = wasUpdated || itemName != req.itemName;

            var recover = req.recover;
            if (GUILayout.Toggle(req.recover, "Recover", new GUIStyle(GUI.skin.toggle) { fixedWidth = 67 }) !=
                req.recover)
            {
                recover = !recover;
                wasUpdated = true;
            }

            if (GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
                wasUpdated = true;
            else
                newReqs.Add(new Requirement { amount = amount, itemName = itemName, recover = recover });

            if (GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
            {
                wasUpdated = true;
                newReqs.Add(new Requirement { amount = 1, itemName = "", recover = false });
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();

        if (wasUpdated) cfg.BoxedValue = new SerializedRequirements(newReqs).ToString();
    }

    private class SerializedRequirements
    {
        public readonly List<Requirement> Reqs;

        public SerializedRequirements(List<Requirement> reqs)
        {
            Reqs = reqs;
        }

        public SerializedRequirements(string reqs)
        {
            Reqs = reqs.Split(',').Select(r =>
            {
                var parts = r.Split(':');
                return new Requirement
                {
                    itemName = parts[0],
                    amount = parts.Length > 1 && int.TryParse(parts[1], out var amount) ? amount : 1,
                    recover = parts.Length <= 2 || !bool.TryParse(parts[2], out var recover) || recover
                };
            }).ToList();
        }

        public override string ToString()
        {
            return string.Join(",", Reqs.Select(r => $"{r.itemName}:{r.amount}:{r.recover}"));
        }

        public static ItemDrop fetchByName(ObjectDB objectDB, string name)
        {
            var item = objectDB.GetItemPrefab(name)?.GetComponent<ItemDrop>();
            if (item == null) TerrainHoePlugin.LogWarning($"The required item '{name}' does not exist.");
            return item;
        }

        public static Piece.Requirement[] toPieceReqs(SerializedRequirements craft)
        {
            ItemDrop ResItem(Requirement r)
            {
                return PrefabManager.GetPrefab(r.itemName)?.GetComponent<ItemDrop>();
            }

            var resources = craft.Reqs.Where(r => r.itemName != "")
                .ToDictionary(r => r.itemName,
                    r => ResItem(r) is { } item
                        ? new Piece.Requirement { m_amount = r.amount, m_resItem = item, m_recover = r.recover }
                        : null);

            return resources.Values.Where(v => v != null).ToArray()!;
        }
    }
}

public class ConfigurationManagerAttributes
{
    [UsedImplicitly] public int? Order;
    [UsedImplicitly] public bool? Browsable;
    [UsedImplicitly] public string? Category;
    [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
}

[PublicAPI]
public class RequiredResourcesList
{
    public readonly List<Requirement> Requirements = [];

    public void Add(string item, int amount, bool recover)
    {
        Requirements.Add(new Requirement
            { itemName = item, amount = amount, recover = recover });
    }
}

public struct Requirement
{
    public string itemName;
    public int amount;
    public bool recover;
}