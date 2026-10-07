using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerrainHoe;

public static class PrefabManager
{
    public static ZNetScene _ZNetScene;
    public static ObjectDB _ObjectDB;
    internal static GameObject GetPrefab(string prefabName)
    {
        GameObject prefab;
        
        if (ZNetScene.instance != null)
        {
            prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab != null) return prefab;
        }

        if (_ZNetScene != null)
        {
            prefab = _ZNetScene.m_prefabs.Find(p => p.name == prefabName);
            if (prefab != null) return prefab;
        }

        return null;
    }


}