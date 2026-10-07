using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace TerrainHoe;

public static class AssetBundleMan
{
    private struct BundleId
    {
        [UsedImplicitly] public string assetBundleFileName;
        [UsedImplicitly] public string folderName;
    }

    private static readonly Dictionary<BundleId, AssetBundle> bundleCache = new();

    public static AssetBundle RegisterAssetBundle(string assetBundleFileName, string folderName = "assets")
    {
        BundleId id = new() { assetBundleFileName = assetBundleFileName, folderName = folderName };
        if (!bundleCache.TryGetValue(id, out AssetBundle assets))
        {
            assets = bundleCache[id] =
                Resources.FindObjectsOfTypeAll<AssetBundle>().FirstOrDefault(a => a.name == assetBundleFileName) ??
                AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + $".{folderName}." +
                                               assetBundleFileName));
        }

        return assets;
    }
    
    
    public static T Load<T>(string assetBundle, string assetId) where T : Object
    {
        return RegisterAssetBundle(assetBundle).LoadAsset<T>(assetId);
    }
    
    public static Sprite GetSprite(string fileName, string folderName = "icons")
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string path = $"{TerrainHoePlugin.ModName}.{folderName}.{fileName}";
        using var stream = assembly.GetManifestResourceStream(path);
        if (stream == null) return null;
        byte[] buffer = new byte[stream.Length];
        var _ = stream.Read(buffer, 0, buffer.Length);
        Texture2D texture = new Texture2D(2, 2);
        
        Sprite sprite = texture.LoadImage4x(buffer) ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null;
        if (sprite != null) sprite.name = fileName;
        return sprite;
    }
    
    private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), nameof(ImageConversion.LoadImage), new [] { typeof(Texture2D), typeof(byte[]) });
    public static bool LoadImage4x(this Texture2D tex, byte[] data)
    {
        return (bool)LoadImage.Invoke(null, [tex , data]);
    }
}