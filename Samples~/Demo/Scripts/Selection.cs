using System;
using System.Collections.Generic;
using UnityEngine;

public class Selection
{
    [System.Serializable]
    public struct Style
    {
        public float thickness;
        public Color fillColor;
        public Color edgeColor; 
    }
    
    public Style style = new Style
    {
        thickness = 2f,
        fillColor = new Color(0.8f, 0.8f, 0.95f, 0.25f),
        edgeColor = new Color(0.8f, 0.8f, 0.95f)
    };
    
    private Vector3 m_cursorPosition;

    public void OnSelectionBegin(Vector3 cursorScreenPos)
    {
        m_cursorPosition = cursorScreenPos;
    }
    
    public void DrawGUI(Vector3 cursorScreenPos)
    {
        // Create a rect from both cursor positions
        Rect rect = GetScreenRect(cursorScreenPos);
        Utils.DrawScreenRect(rect, style.fillColor);
        Utils.DrawScreenRectBorder(rect, style.thickness, style.edgeColor);
    }

    public Rect GetScreenRect(Vector3 cursorScreenPo)
    {
        return Utils.GetScreenRect(m_cursorPosition, cursorScreenPo);
    }
    
    public Bounds GetViewportBounds(Camera camera, Vector3 cursorScreenPos)
    {
        return Utils.GetViewportBounds(camera, m_cursorPosition, cursorScreenPos);
    }

    public Vector3 GetFirstPos()
    {
        return m_cursorPosition;
    }
}