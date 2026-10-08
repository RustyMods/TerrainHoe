using UnityEngine;

namespace TerrainHoe;

public class Lava : IPaint
{
    public Lava(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("Lava"), index)
    {
        isBiomePaint = true;
        overrideAlpha = true;
        piece.m_icon = AssetBundleMan.GetSprite( "lava_ground_icon.png");

        RequiredItems.Add("SulfurStone", 1, false);
    }
    
    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    
    public override Color32 GetBiomeColor() => new (byte.MaxValue, 0,  0, byte.MaxValue);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.AshLands;
}