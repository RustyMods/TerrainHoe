using UnityEngine;

namespace TerrainHoe;

public class AshlandsGrass : IPaint
{
    public AshlandsGrass(string id, string name, int index = 1) : 
        base(id, name, PaintMan.GetPaintType("AshlandsGrass"), index)
    {
        isBiomePaint = true;
        overrideAlpha = true;
        forceGrass = true;
        
        piece.m_icon = AssetBundleMan.GetSprite( "ashland_grass_icon.png");
    }
    
    public override Color GetColor() => new Color(0f, 0f, 0f, 0f);
    
    public override Color32 GetBiomeColor() => new (byte.MaxValue, 0,  0, byte.MaxValue);

    public override Heightmap.Biome GetBiome() => Heightmap.Biome.AshLands;
}