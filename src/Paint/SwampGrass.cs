using UnityEngine;

namespace TerrainHoe;

public class SwampGrass : IPaint
{
    public SwampGrass(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("SwampGrass"), index)
    {
        isBiomePaint = true;
        // piece.m_icon = SpriteManager.GetSprite("swamp_grass_icon.png");
    }

    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    public override Color32 GetBiomeColor() => new Color32(byte.MaxValue, 0, 0, 0);
    public override Heightmap.Biome GetBiome() => Heightmap.Biome.Swamp;
}