using UnityEngine;

namespace TerrainHoe;

public class BlackForestGrass : IPaint
{
    public BlackForestGrass(string id, string name, int index = 1) : base(id, name,
        PaintMan.GetPaintType("BlackForestGrass"), index)
    {
        isBiomePaint = true;
        piece.m_icon = AssetBundleMan.GetSprite( "blackforest_grass_icon.png");
    }
    
    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    
    public override Color32 GetBiomeColor() => new (0, 0,  byte.MaxValue, 0);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.BlackForest;
}