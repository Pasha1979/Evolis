using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер камеры в стиле Cities: Skylines 2.
///
/// Управление:
/// - WASD / стрелки — перемещение (скорость зависит от высоты)
/// - Курсор у края экрана — автопрокрутка (edge scrolling)
/// - Колесо мыши — зум К КУРСОРУ (как в CS2)
/// - ПКМ + движение — орбита вокруг точки на земле
/// - СКМ (средняя кнопка) + движение — перетаскивание карты
/// - Q / E — поворот вокруг точки обзора
///
/// Использует новый Input System (activeInputHandler = 1 в проекте).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Перемещение")]
    [Tooltip("Базовая скорость перемещения (м/с на высоте 50 м)")]
    [SerializeField] private float moveSpeed = 25f;
    [Tooltip("Высота, при которой скорость = базовой")]
    [SerializeField] private float speedReferenceHeight = 50f;

    [Header("Edge Scrolling")]
    [Tooltip("Двигать камеру, когда курсор у края экрана")]
    [SerializeField] private bool edgeScrollEnabled = true;
    [Tooltip("Толщина зоны у края экрана в пикселях")]
    [SerializeField] private float edgeSize = 12f;

    [Header("Зум")]
    [Tooltip("Коэффициент приближения за один щелчок колеса (0.85 = на 15% ближе)")]
    [SerializeField] private float zoomFactor = 0.85f;
    [SerializeField] private float minHeight = 10f;
    [SerializeField] private float maxHeight = 300f;

    [Header("Вращение (орбита)")]
    [Tooltip("Чувствительность вращения ПКМ (градусов на пиксель)")]
    [SerializeField] private float rotateSensitivity = 0.25f;
    [Tooltip("Скорость поворота клавишами Q/E (градусов в секунду)")]
    [SerializeField] private float rotateKeySpeed = 90f;
    [Tooltip("Минимальный и максимальный наклон камеры (градусы)")]
    [SerializeField] private float minPitch = 20f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Перетаскивание карты (СКМ)")]
    [Tooltip("Чувствительность перетаскивания (мировых единиц на пиксель, на высоте 50 м)")]
    [SerializeField] private float dragSensitivity = 0.05f;

    // Плоскость земли для всех рейкастов (y = 0)
    private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

    private void Update()
    {
        HandleMovement();
        HandleZoomToCursor();
        HandleOrbit();
        HandleMapDrag();
    }

    /// <summary>
    /// Текущая высота камеры над землёй.
    /// </summary>
    private float Height => transform.position.y;

    /// <summary>
    /// Скорость, масштабированная по высоте: выше камера — быстрее едет (как в CS2).
    /// </summary>
    private float ScaledSpeed =>
        moveSpeed * Mathf.Max(Height / speedReferenceHeight, 0.3f);

    /// <summary>
    /// WASD + стрелки + edge scrolling — движение по плоскости XZ
    /// относительно текущего поворота камеры.
    /// </summary>
    private void HandleMovement()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        Vector2 input = Vector2.zero;

        // Клавиатура
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;

        // Edge scrolling — курсор у края экрана
        if (edgeScrollEnabled && input == Vector2.zero)
        {
            Vector2 pos = mouse.position.ReadValue();
            if (pos.x < edgeSize) input.x -= 1f;
            else if (pos.x > Screen.width - edgeSize) input.x += 1f;
            if (pos.y < edgeSize) input.y -= 1f;
            else if (pos.y > Screen.height - edgeSize) input.y += 1f;
        }

        if (input == Vector2.zero) return;

        MoveRelative(input);
    }

    /// <summary>
    /// Сдвигает камеру по горизонтали относительно её поворота.
    /// </summary>
    private void MoveRelative(Vector2 input)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        transform.position += (forward * input.y + right * input.x).normalized
                              * (ScaledSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Зум К ТОЧКЕ ПОД КУРСОРОМ (фирменная фишка CS2):
    /// рейкаст из камеры через позицию мыши на плоскость земли,
    /// затем камера сдвигается к точке попадания пропорционально.
    /// При отдалении — наоборот, от точки.
    /// </summary>
    private void HandleZoomToCursor()
    {
        var mouse = Mouse.current;
        var cam = GetComponent<Camera>();
        if (mouse == null || cam == null) return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f)) return;

        // Точка на земле под курсором
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!GroundPlane.Raycast(ray, out float enter)) return;
        Vector3 hitPoint = ray.GetPoint(enter);

        // Масштабируем вектор от точки до камеры
        float factor = scrollY > 0f ? zoomFactor : 1f / zoomFactor;
        Vector3 newPosition = hitPoint + (transform.position - hitPoint) * factor;

        // Ограничиваем по высоте
        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
        transform.position = newPosition;
    }

    /// <summary>
    /// Орбита вокруг точки на земле (центр экрана):
    /// - ПКМ + движение мыши — yaw и pitch
    /// - Q / E — yaw с постоянной скоростью
    /// Камера вращается ВОКРУГ фокуса, а не вокруг себя — как в CS2.
    /// </summary>
    private void HandleOrbit()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        var cam = GetComponent<Camera>();
        if (mouse == null || keyboard == null || cam == null) return;

        float yawDelta = 0f;
        float pitchDelta = 0f;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yawDelta = delta.x * rotateSensitivity;
            pitchDelta = -delta.y * rotateSensitivity;
        }
        if (keyboard.qKey.isPressed) yawDelta -= rotateKeySpeed * Time.deltaTime;
        if (keyboard.eKey.isPressed) yawDelta += rotateKeySpeed * Time.deltaTime;

        if (Mathf.Approximately(yawDelta, 0f) && Mathf.Approximately(pitchDelta, 0f)) return;

        // Фокус орбиты — точка на земле в центре экрана
        Ray centerRay = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (!GroundPlane.Raycast(centerRay, out float enter)) return;
        Vector3 focus = centerRay.GetPoint(enter);

        float distance = Vector3.Distance(transform.position, focus);
        Vector3 euler = transform.eulerAngles;

        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        float newPitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
        float newYaw = euler.y + yawDelta;

        // Пересчитываем позицию: орбита вокруг фокуса на том же расстоянии
        Quaternion rotation = Quaternion.Euler(newPitch, newYaw, 0f);
        transform.position = focus + rotation * Vector3.back * distance;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Перетаскивание карты средней кнопкой мыши (grab-and-drag):
    /// камера движется противоположно движению мыши — как будто тянешь карту.
    /// Чувствительность масштабируется по высоте.
    /// </summary>
    private void HandleMapDrag()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed) return;

        Vector2 delta = mouse.delta.ReadValue();
        float scale = dragSensitivity * Mathf.Max(Height / speedReferenceHeight, 0.3f);

        // Прямой сдвиг без Time.deltaTime — дельта мыши уже «за кадр»
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        transform.position += (-right * delta.x - forward * delta.y) * scale;
    }
}
