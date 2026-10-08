using UnityEngine;

namespace TerrainHoe;

public class PavedGrass : IPaint
{
    public PavedGrass(string id, string name, int index = 1) : base(
        id, name, PaintMan.GetPaintType("PavedGrass"), index)
    {
        piece.m_icon = AssetBundleMan.GetSprite( "paved_grass_icon.png");
        
        RequiredItems.Add("Stone", 1, false);
    }
    
    public override Color GetColor() => new Color(0.0f, 0.0f, 0.5f, 0.5f);
}