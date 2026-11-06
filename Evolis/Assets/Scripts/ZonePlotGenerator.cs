using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Генератор сетки участков внутри зоны.
/// Вызывается из PolygonDrawer при создании зоны — получает вершины полигона
/// и раскладывает прямоугольные парцеллы по внутренней области.
/// </summary>
public class ZonePlotGenerator : MonoBehaviour
{
    [Header("Участки")]
    [Tooltip("Размер участка в метрах (X × Z)")]
    public Vector2 plotSize = new Vector2(10f, 10f);

    [Header("Отладка")]
    [Tooltip("Показывать полупрозрачные quads на участках")]
    public bool debugVisualization = true;
    [SerializeField] private float plotY = 0.3f;

    private static Material debugMaterial;

    /// <summary>
    /// Точка входа: вершины полигона (мировые координаты) и id зоны.
    /// </summary>
    public void Generate(List<Vector3> polygonPoints, int zoneId)
    {
        // Проекция полигона на плоскость XZ
        var poly2d = new List<Vector2>(polygonPoints.Count);
        foreach (var p in polygonPoints) poly2d.Add(new Vector2(p.x, p.z));

        // Bounding box полигона
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in poly2d)
        {
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minZ = Mathf.Min(minZ, v.y); maxZ = Mathf.Max(maxZ, v.y);
        }

        int plotId = 0;
        for (float x = minX; x + plotSize.x <= maxX + 0.001f; x += plotSize.x)
        {
            for (float z = minZ; z + plotSize.y <= maxZ + 0.001f; z += plotSize.y)
            {
                // Центр ячейки — участок засчитывается, если центр внутри полигона
                Vector2 center = new Vector2(x + plotSize.x / 2f, z + plotSize.y / 2f);
                if (!IsInsidePolygon(center, poly2d)) continue;

                plotId++;
                CreatePlot(zoneId, plotId, center);
            }
        }

        Debug.Log($"[ZonePlotGenerator] Зона {zoneId}: создано {plotId} участков ({plotSize.x}×{plotSize.y} м)");
    }

    /// <summary>
    /// Точка внутри полигона — метод ray casting на плоскости XZ.
    /// </summary>
    private static bool IsInsidePolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Создаёт GameObject участка с PlotInfo и отладочным quad.
    /// </summary>
    private void CreatePlot(int zoneId, int plotId, Vector2 center2d)
    {
        Vector3 center = new Vector3(center2d.x, plotY, center2d.y);

        var plot = new GameObject($"Plot_Zone{zoneId}_Plot{plotId}");
        plot.transform.SetParent(transform);
        plot.transform.position = center;

        var info = plot.AddComponent<PlotInfo>();
        info.plotId = plotId;
        info.zoneId = zoneId;
        info.isOccupied = false;
        info.center = center;

        if (debugVisualization) CreateDebugQuad(plot.transform, center);
    }

    /// <summary>
    /// Полупрозрачный quad для визуальной отладки участка.
    /// </summary>
    private void CreateDebugQuad(Transform parent, Vector3 center)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "DebugQuad";
        quad.transform.SetParent(parent);
        quad.transform.position = center;
        // Quad лежит в XY — поворачиваем горизонтально в XZ, с небольшим отступом от краёв
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(plotSize.x * 0.9f, plotSize.y * 0.9f, 1f);
        Destroy(quad.GetComponent<Collider>());

        if (debugMaterial == null)
        {
            debugMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            debugMaterial.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 0.3f));
            debugMaterial.SetFloat("_Surface", 1f);
            debugMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            debugMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            debugMaterial.SetInt("_ZWrite", 0);
            debugMaterial.renderQueue = (int)RenderQueue.Transparent;
        }
        quad.GetComponent<Renderer>().sharedMaterial = debugMaterial;
    }
}
