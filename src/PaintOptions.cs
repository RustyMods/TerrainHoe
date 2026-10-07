using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TerrainHoe;

public class PaintOptions : MonoBehaviour
{
    public static PaintOptions instance;
    public Piece m_piece;
    public float radius;
    public ParticleSystem m_ring;
    public ParticleSystem m_particles;
    public CircleProjector projector;

    private IPaint m_tool;
    
    public string m_pieceInfo = "";
    private float m_minRadius = 5f;
    private float m_maxRadius = 20f;
    private float m_maxSmooth = 10f;
    private float m_maxRaiseDelta = 20f;
    
    public void Awake()
    {
        if (!TryGetComponent(out TerrainOp terrain) || 
            !IPaint.TryGetPaintTool(terrain.m_settings.m_paintType, out m_tool))
        {
            TerrainHoePlugin.LogWarning("Invalid paint tool");
            Destroy(this);
        }
        
        m_piece = GetComponent<Piece>();
        radius = m_tool.terrainOp.m_settings.m_paintRadius;
        m_ring = transform.Find("_GhostOnly").GetComponent<ParticleSystem>();
        m_particles = transform.Find("_GhostOnly/particles").GetComponent<ParticleSystem>();
        projector = GetComponentInChildren<CircleProjector>(true);
        projector.m_radius = radius;
        projector.m_mask = LayerMask.GetMask("terrain");
        projector.m_nrOfSegments = Mathf.CeilToInt(radius / 2.0f);
        
        UpdateHudPieceInfo();
        
        instance = this;
    }

    public void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void FixedUpdate()
    {
        if (!Player.m_localPlayer) return;
        
        float scroll = ZInput.GetMouseScrollWheel();

        if (UpdateSmooth(scroll) || 
            UpdateRadius(scroll) || 
            UpdateRaise(scroll) ||
            UpdateLevel(scroll))
        {
            UpdateHudPieceInfo();
        }
    }

    public void UpdateHudPieceInfo()
    {
        if (!Hud.instance) return;
        StringBuilder sb = new StringBuilder(256);
        sb.Append(m_piece.m_description);
        sb.AppendFormat(
            "\n[<color=orange>{0}</color> + <color=orange>{1}</color>] +- radius" +
            "\n[<color=orange>{0}</color> + <color=orange>{2}</color>] +- smooth" +
            "\n[<color=orange>{0}</color> + <color=orange>{3}</color>] +- raise" +
            "\n[<color=orange>{0}</color> + <color=orange>{4}</color>] +- level\n", 
            "Scroll",
            KeyCode.LeftShift,
            KeyCode.Q,
            KeyCode.F,
            KeyCode.L);
        sb.Append($"SmoothPower: <color=orange>{m_tool.terrainOp.m_settings.m_smoothPower}</color>");
        sb.Append($", RaisePower: <color=orange>{m_tool.terrainOp.m_settings.m_raisePower}</color>");
        sb.Append($", RaiseDelta: <color=orange>{m_tool.terrainOp.m_settings.m_raiseDelta}</color>");
        sb.Append($", LevelOffset: <color=orange>{m_tool.terrainOp.m_settings.m_levelOffset}</color>");
        m_pieceInfo = Localization.instance.Localize(sb.ToString());
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.SetupPieceInfo))]
    private static class Hud_SetupPieceInfo_Patch
    {
        private static void Postfix(Hud __instance, Piece piece)
        {
            if (piece == null || instance == null) return;
            __instance.m_pieceDescription.text = instance.m_pieceInfo;
        }
    }


    private bool UpdateRadius(float scroll)
    {
        if (!ZInput.GetKey(KeyCode.LeftShift)) return false;
        
        bool changed = false;
        
        if (scroll > Player.m_localPlayer.m_scrollAmountThreshold)
        {
            radius = Mathf.Clamp(radius + 0.5f, m_minRadius, m_maxRadius);
            changed = true;
        }

        if (scroll < -Player.m_localPlayer.m_scrollAmountThreshold)
        {
            radius = Mathf.Clamp(radius - 0.5f, m_minRadius, m_maxRadius);
            changed = true;
        }

        if (!changed) return false;
        
        m_tool.terrainOp.m_settings.m_levelRadius = radius;
        m_tool.terrainOp.m_settings.m_raiseRadius = radius;
        m_tool.terrainOp.m_settings.m_paintRadius = radius;
        m_tool.terrainOp.m_settings.m_smoothRadius = radius + 1f;
        
        projector.m_radius = radius;
        projector.m_nrOfSegments = Math.Max(Mathf.CeilToInt(radius), 4);
        return true;
    }

    private bool UpdateSmooth(float scroll)
    {
        if (!ZInput.GetKey(KeyCode.Q)) return false;
        
        bool changed = false;
        
        if (scroll > Player.m_localPlayer.m_scrollAmountThreshold)
        {
            m_tool.terrainOp.m_settings.m_smoothPower = Mathf.Clamp(m_tool.terrainOp.m_settings.m_smoothPower + 0.5f, 0f, m_maxSmooth);
            changed = true;
        }

        if (scroll < -Player.m_localPlayer.m_scrollAmountThreshold)
        {
            m_tool.terrainOp.m_settings.m_smoothPower = Mathf.Clamp(m_tool.terrainOp.m_settings.m_smoothPower - 0.5f, 0f, m_maxSmooth);
            changed = true;
        }

        if (!changed) return false;

        m_tool.terrainOp.m_settings.m_smooth = m_tool.terrainOp.m_settings.m_smoothPower > 0f;
        return true;
    }

    private bool UpdateRaise(float scroll)
    {
        if (!ZInput.GetKey(KeyCode.F)) return false;
        
        bool changed = false;
        
        if (scroll > Player.m_localPlayer.m_scrollAmountThreshold)
        {
            m_tool.terrainOp.m_settings.m_raisePower = Mathf.Clamp01(m_tool.terrainOp.m_settings.m_raisePower + 0.1f);
            m_tool.terrainOp.m_settings.m_raiseDelta = Mathf.Clamp(m_tool.terrainOp.m_settings.m_raiseDelta + 0.5f, 0f, m_maxRaiseDelta);
            changed = true;
        }

        if (scroll < -Player.m_localPlayer.m_scrollAmountThreshold)
        {
            m_tool.terrainOp.m_settings.m_raisePower = Mathf.Clamp01(m_tool.terrainOp.m_settings.m_raisePower - 0.1f);
            m_tool.terrainOp.m_settings.m_raiseDelta = Mathf.Clamp(m_tool.terrainOp.m_settings.m_raiseDelta - 0.5f, 0f, m_maxRaiseDelta);
            changed = true;
        }

        if (!changed) return false;
        
        m_tool.terrainOp.m_settings.m_raise = m_tool.terrainOp.m_settings.m_raiseDelta > 0f;
        return true;
    }

    private bool UpdateLevel(float scroll)
    {
        if (!ZInput.GetKey(KeyCode.L)) return false;
        
        bool changed = false;
        
        if (scroll > Player.m_localPlayer.m_scrollAmountThreshold)
        {
            ++m_tool.terrainOp.m_settings.m_levelOffset;
            changed = true;
        }

        if (scroll < -Player.m_localPlayer.m_scrollAmountThreshold)
        {
            --m_tool.terrainOp.m_settings.m_levelOffset;
            changed = true;
        }

        if (!changed) return false;

        m_tool.terrainOp.m_settings.m_level = m_tool.terrainOp.m_settings.m_levelOffset != 0f;
        return true;
    }
}