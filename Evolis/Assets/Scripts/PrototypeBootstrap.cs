using UnityEngine;

/// <summary>
/// Бутстрап серого прототипа: создаёт землю (Plane) с базовым материалом при старте сцены.
/// Достаточно повесить на пустой GameObject в SampleScene — никакой ручной настройки сцены не нужно.
/// </summary>
public class PrototypeBootstrap : MonoBehaviour
{
    [Header("Земля прототипа")]
    [Tooltip("Множитель размера: стандартный Plane = 10 м, значение 50 даст площадку 500×500 м")]
    [SerializeField] private float groundScale = 50f;
    [SerializeField] private Color groundColor = new Color(0.35f, 0.5f, 0.3f);

    private void Awake()
    {
        CreateGround();
    }

    /// <summary>
    /// Создаёт Plane-примитив с материалом URP Lit.
    /// </summary>
    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground_Prototype";
        ground.transform.localScale = new Vector3(groundScale, 1f, groundScale);

        var groundRenderer = ground.GetComponent<Renderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // В URP Lit базовый цвет лежит в свойстве _BaseColor (не _Color)
        material.SetColor("_BaseColor", groundColor);
        groundRenderer.sharedMaterial = material;

        Debug.Log($"[PrototypeBootstrap] Земля создана: {groundScale * 10f}×{groundScale * 10f} м");
    }
}
