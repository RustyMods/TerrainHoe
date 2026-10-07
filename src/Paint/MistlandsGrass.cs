using UnityEngine;

namespace TerrainHoe;

public class MistlandsGrass : IPaint
{
    public MistlandsGrass(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("Mistlands"), index)
    {
        isBiomePaint = true;
        piece.m_icon = AssetBundleMan.GetSprite("mistlands_grass_icon.png");
    }

    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    
    public override Color32 GetBiomeColor() => new (0, 0,  byte.MaxValue, byte.MaxValue);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.Mistlands;
}

public class MistlandsDirt : IPaint
{
    public MistlandsDirt(string id, string name, int index = 1) : base(id, name,
        PaintMan.GetPaintType("MistlandsDirt"), index)
    {
        isBiomePaint = true;
        piece.m_icon = AssetBundleMan.GetSprite( "mistlands_ground_icon.png");
    }
    
    public override Color GetColor() => new Color(1f, 0f, 0f, 0f);
    
    public override Color32 GetBiomeColor() => new (0, 0,  byte.MaxValue, byte.MaxValue);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.Mistlands;
}