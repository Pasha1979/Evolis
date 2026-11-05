using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Инструмент рисования полигональных зон — ядро зонирования Evolis.
///
/// Управление:
/// - ЛКМ по земле — добавить вершину полигона
/// - Линия от последней вершины следует за курсором (превью)
/// - Клавиша C — очистить все точки
///
/// Замыкание полигона и расчёт площади — задача 1.2.2 (следующий этап).
/// </summary>
public class PolygonDrawer : MonoBehaviour
{
    [Header("Визуализация")]
    [SerializeField] private float lineWidth = 0.6f;
    [SerializeField] private Color lineColor = Color.yellow;
    [Tooltip("Подъём линии над землёй — защита от z-fighting")]
    [SerializeField] private float lineHeight = 0.5f;
    [SerializeField] private float markerRadius = 0.8f;

    // Вершины полигона в мировых координатах
    private readonly List<Vector3> points = new();
    private readonly List<GameObject> markers = new();

    private LineRenderer lineRenderer;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        SetupLineRenderer();
    }

    /// <summary>
    /// LineRenderer создаётся программно — сцену настраивать не нужно.
    /// </summary>
    private void SetupLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 0;

        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", lineColor);
        lineRenderer.sharedMaterial = material;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // Точка на земле под курсором — одна на кадр, используем и для клика, и для превью
        Vector3? groundPoint = RaycastGround(mouse.position.ReadValue());

        if (mouse.leftButton.wasPressedThisFrame && groundPoint.HasValue)
            AddPoint(groundPoint.Value);

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            ClearPoints();

        UpdateVisual(groundPoint);
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
    /// Очищает вершины и маркеры (клавиша C).
    /// </summary>
    private void ClearPoints()
    {
        points.Clear();
        foreach (var marker in markers) Destroy(marker);
        markers.Clear();
        lineRenderer.positionCount = 0;
        Debug.Log("[PolygonDrawer] Полигон очищен");
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
