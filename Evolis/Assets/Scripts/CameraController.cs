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

    private void Update()
    {
        HandleMovement();
        HandleZoom();
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
}
