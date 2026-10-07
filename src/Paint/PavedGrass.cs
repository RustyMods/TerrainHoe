using UnityEngine;

namespace TerrainHoe;

public class PavedGrass : IPaint
{
    public PavedGrass(string id, string name, int index = 1) : base(
        id, name, PaintMan.GetPaintType("PavedGrass"), index)
    {
        // piece.m_icon = SpriteManager.GetSprite("paved_grass_icon.png");
    }
    
    public override Color GetColor() => new Color(0.0f, 0.0f, 0.5f, 0.5f);
}