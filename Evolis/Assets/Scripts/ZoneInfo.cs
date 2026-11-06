using UnityEngine;

/// <summary>
/// Данные зоны: тип, площадь, число вершин.
/// Вешается на каждый созданный объект зоны (Zone_N_Type).
/// Основа для будущей системы застройки институтами.
/// </summary>
public class ZoneInfo : MonoBehaviour
{
    public PolygonDrawer.ZoneType zoneType;
    public float area;
    public int vertexCount;
}
