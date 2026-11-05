using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Инструмент рисования полигональных зон — ядро зонирования Evolis.
///
/// Управление:
/// - ЛКМ по земле — добавить вершину полигона
/// - ЛКМ рядом с первой вершиной (3+ точки) — ЗАМКНУТЬ зону
/// - Клавиша C — очистить текущее рисование
///
/// Замкнутая зона = отдельный GameObject с заливкой и контуром;
/// можно сразу рисовать следующую зону.
/// </summary>
public class PolygonDrawer : MonoBehaviour
{
    [Header("Визуализация")]
    [SerializeField] private float lineWidth = 0.6f;
    [SerializeField] private Color lineColor = Color.yellow;
    [Tooltip("Подъём линии над землёй — защита от z-fighting")]
    [SerializeField] private float lineHeight = 0.5f;
    [SerializeField] private float markerRadius = 0.8f;
    [SerializeField] private Color fillColor = new Color(0.2f, 0.9f, 0.4f, 0.25f);

    [Header("Замыкание")]
    [Tooltip("Клик ближе этого расстояния к первой вершине замыкает полигон")]
    [SerializeField] private float closeThreshold = 4f;

    // Вершины текущего (рисуемого) полигона в мировых координатах
    private readonly List<Vector3> points = new();
    private readonly List<GameObject> markers = new();

    private LineRenderer lineRenderer;
    private Material fillMaterial;
    private Camera cam;
    private int zoneCount;

    private void Awake()
    {
        cam = Camera.main;
        SetupLineRenderer();
    }

    /// <summary>
    /// LineRenderer и материал заливки создаются программно — сцену настраивать не нужно.
    /// </summary>
    private void SetupLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 0;

        var lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.SetColor("_BaseColor", lineColor);
        lineRenderer.sharedMaterial = lineMat;

        // Полупрозрачный материал заливки зоны
        fillMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        fillMaterial.SetColor("_BaseColor", fillColor);
        fillMaterial.SetFloat("_Surface", 1f); // 1 = прозрачная поверхность
        fillMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        fillMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        fillMaterial.SetInt("_ZWrite", 0);
        fillMaterial.renderQueue = (int)RenderQueue.Transparent;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // Точка на земле под курсором — одна на кадр, для клика и превью
        Vector3? groundPoint = RaycastGround(mouse.position.ReadValue());

        if (mouse.leftButton.wasPressedThisFrame && groundPoint.HasValue)
            HandleClick(groundPoint.Value);

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            ResetDrawing();

        UpdateVisual(groundPoint);
    }

    /// <summary>
    /// Клик ЛКМ: либо замыкание полигона (рядом с первой вершиной),
    /// либо добавление новой вершины.
    /// </summary>
    private void HandleClick(Vector3 point)
    {
        if (points.Count >= 3 && XZDistance(point, points[0]) < closeThreshold)
        {
            ClosePolygon();
            return;
        }
        AddPoint(point);
    }

    /// <summary>
    /// Рейкаст из камеры через позицию мыши в коллайдер земли.
    /// </summary>
    private Vector3? RaycastGround(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out RaycastHit hit) ? hit.point : null;
    }

    /// <summary>
    /// Расстояние по плоскости XZ (высота игнорируется).
    /// </summary>
    private static float XZDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    /// <summary>
    /// Добавляет вершину и маркер-сферу в точке клика.
    /// </summary>
    private void AddPoint(Vector3 point)
    {
        Vector3 elevated = point + Vector3.up * lineHeight;
        points.Add(elevated);

        // Маркер-сфера — видимая вершина полигона
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = $"ZonePoint_{points.Count}";
        marker.transform.position = elevated;
        marker.transform.localScale = Vector3.one * markerRadius * 2f;
        Destroy(marker.GetComponent<Collider>()); // маркер не должен ловить рейкасты
        marker.GetComponent<Renderer>().sharedMaterial = lineRenderer.sharedMaterial;
        markers.Add(marker);

        Debug.Log($"[PolygonDrawer] Точка {points.Count}: {point}");
    }

    /// <summary>
    /// Замыкает полигон: создаёт объект зоны с заливкой и контуром,
    /// считает площадь, очищает рисовальщик для следующей зоны.
    /// </summary>
    private void ClosePolygon()
    {
        float area = SignedArea();
        float absArea = Mathf.Abs(area);

        zoneCount++;
        var zone = new GameObject($"Zone_{zoneCount}");
        BuildFill(zone, area < 0f);
        BuildOutline(zone);

        // Маркеры переезжают в объект зоны вместо удаления
        foreach (var marker in markers) marker.transform.SetParent(zone.transform);
        markers.Clear();
        points.Clear();
        lineRenderer.positionCount = 0;

        Debug.Log($"[PolygonDrawer] Зона {zoneCount} замкнута: " +
                  $"{zone.transform.childCount} вершин, площадь ≈ {absArea:F0} м²");
    }

    /// <summary>
    /// Площадь полигона по формуле Shoelace на плоскости XZ.
    /// Знак показывает направление обхода (нужен для нормалей заливки).
    /// </summary>
    private float SignedArea()
    {
        float sum = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            Vector3 q = points[(i + 1) % points.Count];
            sum += p.x * q.z - q.x * p.z;
        }
        return sum * 0.5f;
    }

    /// <summary>
    /// Заливка зоны — меш, триангулированный методом «отрезания ушей» (ear clipping).
    /// Работает и на выпуклых, и на вогнутых полигонах.
    /// </summary>
    private void BuildFill(GameObject zone, bool reversed)
    {
        var verts3d = points.Select(p => p - Vector3.up * (lineHeight - 0.2f)).ToArray();

        // Проекция на 2D (X,Z); при обходе по часовой — разворачиваем порядок
        var verts = verts3d.Select(p => new Vector2(p.x, p.z)).ToList();
        var order = Enumerable.Range(0, verts.Count).ToList();
        if (reversed) order.Reverse();

        var triangles = Triangulate(verts, order);

        var mesh = new Mesh
        {
            vertices = (reversed ? verts3d.Reverse() : verts3d).ToArray(),
            triangles = triangles,
            normals = Enumerable.Repeat(Vector3.up, verts.Count).ToArray()
        };

        var mf = zone.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = zone.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fillMaterial;
    }

    /// <summary>
    /// Ear-clipping триангуляция: отрезаем выпуклые «уши» до остатка-треугольника.
    /// Возвращает индексы в массиве verts3d (учитывая order).
    /// </summary>
    private static int[] Triangulate(List<Vector2> verts, List<int> order)
    {
        var indices = new List<int>();
        var ids = new List<int>(order);

        while (ids.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < ids.Count; i++)
            {
                int i0 = ids[(i - 1 + ids.Count) % ids.Count];
                int i1 = ids[i];
                int i2 = ids[(i + 1) % ids.Count];

                if (Cross(verts[i0], verts[i1], verts[i2]) <= 0f) continue; // ухо не выпуклое

                bool inside = ids.Any(j =>
                    j != i0 && j != i1 && j != i2 &&
                    PointInTriangle(verts[j], verts[i0], verts[i1], verts[i2]));
                if (inside) continue;

                indices.AddRange(new[] { i0, i1, i2 });
                ids.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // вырожденный полигон — страховка от зацикливания
        }
        if (ids.Count == 3) indices.AddRange(new[] { ids[0], ids[1], ids[2] });
        return indices.ToArray();
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
        (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(a, b, p);
        float d2 = Cross(b, c, p);
        float d3 = Cross(c, a, p);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    /// <summary>
    /// Контур зоны — собственный LineRenderer в режиме loop на объекте зоны.
    /// </summary>
    private void BuildOutline(GameObject zone)
    {
        var lr = zone.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.sharedMaterial = lineRenderer.sharedMaterial;
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            lr.SetPosition(i, points[i]);
    }

    /// <summary>
    /// Очищает текущее рисование, не трогая завершённые зоны (клавиша C).
    /// </summary>
    private void ResetDrawing()
    {
        points.Clear();
        foreach (var marker in markers) Destroy(marker);
        markers.Clear();
        lineRenderer.positionCount = 0;
        Debug.Log("[PolygonDrawer] Текущее рисование очищено");
    }

    /// <summary>
    /// Обновляет LineRenderer: все вершины + превью-линия к курсору.
    /// </summary>
    private void UpdateVisual(Vector3? cursorPoint)
    {
        int count = points.Count;
        bool showPreview = count > 0 && cursorPoint.HasValue;

        lineRenderer.positionCount = count + (showPreview ? 1 : 0);
        for (int i = 0; i < count; i++)
            lineRenderer.SetPosition(i, points[i]);

        if (showPreview)
            lineRenderer.SetPosition(count, cursorPoint.Value + Vector3.up * lineHeight);
    }
}
