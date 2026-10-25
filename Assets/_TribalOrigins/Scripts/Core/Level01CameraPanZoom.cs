using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TribalOrigins.Core
{
    /// <summary>
    /// M1.10 PC camera: WASD/arrows pan, middle-mouse drag, wheel zoom.
    /// Right mouse remains reserved for A* move orders (M1.9).
    /// Camera is clamped to 64x48 map, with special handling when view > map.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class Level01CameraPanZoom : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(1f)] private float keyboardPanSpeed = 14f;
        [SerializeField] private bool allowMiddleMouseDrag = true;
        [Header("Zoom")]
        [SerializeField, Min(1f)] private float minOrthographicSize = 3.5f;
        [SerializeField, Min(2f)] private float maxOrthographicSize = 28f;
        [SerializeField, Range(1.02f, 1.5f)] private float zoomFactorPerNotch = 1.15f;
        [SerializeField] private bool clampToMap = true;

        private Camera cam;
        private bool wasDragging;
        private Vector2 previousMouse;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            float moveX = 0f;
            float moveY = 0f;
            bool middleHeld = false;
            Vector2 mousePixel = Vector2.zero;
            float wheel = 0f;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY += 1f;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                middleHeld = mouse.middleButton.isPressed;
                mousePixel = mouse.position.ReadValue();
                wheel = mouse.scroll.ReadValue().y;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveY -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveY += 1f;
            middleHeld = Input.GetMouseButton(2);
            mousePixel = Input.mousePosition;
            wheel = Input.mouseScrollDelta.y;
#endif
            Vector2 keyboardDirection = new Vector2(moveX, moveY);
            if (keyboardDirection.sqrMagnitude > 1f) keyboardDirection.Normalize();
            transform.position += new Vector3(keyboardDirection.x, keyboardDirection.y, 0f) *
                keyboardPanSpeed * Time.unscaledDeltaTime;

            bool pointerInView = cam.pixelRect.Contains(mousePixel);
            if (allowMiddleMouseDrag && middleHeld && pointerInView)
            {
                if (wasDragging)
                {
                    Vector2 pixelDelta = mousePixel - previousMouse;
                    float worldPerPixel = 2f * cam.orthographicSize / Mathf.Max(1f, cam.pixelHeight);
                    transform.position -= new Vector3(pixelDelta.x, pixelDelta.y, 0f) * worldPerPixel;
                }
                previousMouse = mousePixel;
                wasDragging = true;
            }
            else wasDragging = false;

            if (pointerInView && Mathf.Abs(wheel) > 0.01f)
            {
                // New Input System typically reports ±120 per notch; legacy reports ±1.
                float notches = Mathf.Clamp(Mathf.Abs(wheel) >= 10f ? wheel / 120f : wheel, -3f, 3f);
                float factor = Mathf.Pow(zoomFactorPerNotch, -notches);
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor,
                    minOrthographicSize, maxOrthographicSize);
            }
            if (clampToMap) ClampCamera();
        }

        private void ClampCamera()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float halfMapW = Level01DebugMap.MapWidth * 0.5f;
            float halfMapH = Level01DebugMap.MapHeight * 0.5f;
            Vector3 position = transform.position;
            position.x = halfW >= halfMapW ? halfMapW :
                Mathf.Clamp(position.x, halfW, Level01DebugMap.MapWidth - halfW);
            position.y = halfH >= halfMapH ? halfMapH :
                Mathf.Clamp(position.y, halfH, Level01DebugMap.MapHeight - halfH);
            transform.position = position;
        }
    }
}
