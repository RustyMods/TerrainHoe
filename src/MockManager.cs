using UnityEngine;
using Object = UnityEngine.Object;

namespace TerrainHoe;

public static class MockManager
{
    private static readonly GameObject root;
    public static Transform transform => root.transform;

    static MockManager()
    {
        root = new GameObject("MockManager");
        Object.DontDestroyOnLoad(root);
        root.SetActive(false);
    }
}