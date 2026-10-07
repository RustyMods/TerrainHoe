using UnityEngine;

namespace TerrainHoe;

public class PavedDark : IPaint
{
    public PavedDark(string id, string name, int index = 1) : base(
        id, name, PaintMan.GetPaintType("PavedCultivated"), index)
    {
        // piece.m_icon = SpriteManager.GetSprite("paved_dark_icon.png");
    }
    
    public override Color GetColor() => new Color(0f, 1f, 0.5f, 1f);

    public override Heightmap.Biome GetBiome()
    {
        throw new System.NotImplementedException();
    }
}