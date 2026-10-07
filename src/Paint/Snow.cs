using UnityEngine;

namespace TerrainHoe;

public class Snow : IPaint
{
    public Snow(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("Snow"), index)
    {
        isBiomePaint = true;
        
        // piece.m_icon = SpriteManager.GetSprite("snow_icon.png");
    }

    public override Color GetColor() => Color.clear;

    public override Color32 GetBiomeColor() => new Color32(0, byte.MaxValue, 0, 0);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.Mountain;
}