using UnityEngine;

namespace TerrainHoe;

public class MeadowsGrass : IPaint
{
    public MeadowsGrass(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("MeadowsGrass"), index)
    {
        isBiomePaint = true;
        piece.m_icon = AssetBundleMan.GetSprite( "meadows_grass_icon.png");
    }

    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    public override Color32 GetBiomeColor() => new Color32(0, 0, 0, 0);
    public override Heightmap.Biome GetBiome() => Heightmap.Biome.Meadows;
}