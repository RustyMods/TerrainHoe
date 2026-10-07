using UnityEngine;

namespace TerrainHoe;

public class Reset : IPaint
{
    public Reset(string id, string name, int index = 1) : base(id, name, PaintMan.GetPaintType("ResetAll"), index)
    {
        isBiomePaint = true;
        reset = true;
        // piece.m_icon = SpriteManager.GetSprite("reset_icon.png");
    }

    public override Color GetColor() => new Color(0f, 0f, 0f, 1f);
    
}