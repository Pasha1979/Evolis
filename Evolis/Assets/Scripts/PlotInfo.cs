using UnityEngine;

/// <summary>
/// Данные участка (парцеллы) внутри зоны.
/// Основа для будущей застройки институтами.
/// </summary>
public class PlotInfo : MonoBehaviour
{
    public int plotId;
    public int zoneId;
    public bool isOccupied;
    public Vector3 center;
}
