using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер камеры серого прототипа.
/// Перемещение — WASD/стрелки, зум — колесо мыши (по высоте).
/// Использует новый Input System: в проекте activeInputHandler = 1,
/// классический Input.GetKey здесь НЕ работает.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Перемещение")]
    [Tooltip("Скорость перемещения камеры в метрах в секунду")]
    [SerializeField] private float moveSpeed = 25f;

    [Header("Зум")]
    [Tooltip("Скорость изменения высоты при прокрутке колеса")]
    [SerializeField] private float zoomSpeed = 40f;
    [SerializeField] private float minHeight = 10f;
    [SerializeField] private float maxHeight = 300f;

    [Header("Вращение")]
    [Tooltip("Скорость вращения при зажатой ПКМ (градусов на пиксель)")]
    [SerializeField] private float rotateSensitivity = 0.25f;
    [Tooltip("Скорость поворота клавишами Q/E (градусов в секунду)")]
    [SerializeField] private float rotateKeySpeed = 90f;
    [Tooltip("Минимальный и максимальный наклон камеры (градусы)")]
    [SerializeField] private float minPitch = 20f;
    [SerializeField] private float maxPitch = 85f;

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleRotation();
    }

    /// <summary>
    /// WASD и стрелки — движение камеры по плоскости XZ.
    /// Движение относительно текущего поворота камеры по горизонтали.
    /// </summary>
    private void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (input == Vector2.zero) return;

        // Переводим ввод в мировые направления относительно поворота камеры
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        transform.position += (forward * input.y + right * input.x).normalized
                              * (moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Колесо мыши — подъём/опускание камеры.
    /// Значение scroll отличается на разных платформах (~±1 или ~±120),
    /// поэтому используем только знак — зум предсказуем везде.
    /// </summary>
    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f)) return;

        float direction = Mathf.Sign(scrollY);
        Vector3 position = transform.position;
        position.y = Mathf.Clamp(
            position.y - direction * zoomSpeed * Time.deltaTime,
            minHeight, maxHeight);
        transform.position = position;
    }

    /// <summary>
    /// Вращение камеры:
    /// - зажатая ПКМ + движение мыши — поворот по горизонтали (yaw) и наклон (pitch)
    /// - клавиши Q/E — поворот по горизонтали с постоянной скоростью
    /// Наклон ограничен диапазоном minPitch..maxPitch, чтобы не уйти под землю/в зенит.
    /// </summary>
    private void HandleRotation()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        float yawDelta = 0f;
        float pitchDelta = 0f;

        // ПКМ зажата — вращаем мышью
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yawDelta = delta.x * rotateSensitivity;
            pitchDelta = -delta.y * rotateSensitivity;
        }

        // Клавиши Q/E — поворот по горизонтали
        if (keyboard.qKey.isPressed) yawDelta -= rotateKeySpeed * Time.deltaTime;
        if (keyboard.eKey.isPressed) yawDelta += rotateKeySpeed * Time.deltaTime;

        if (Mathf.Approximately(yawDelta, 0f) && Mathf.Approximately(pitchDelta, 0f)) return;

        // Текущие углы: yaw — по мировой оси Y, pitch — наклон с нормализацией в -180..180
        Vector3 euler = transform.eulerAngles;
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
        float yaw = euler.y + yawDelta;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
