using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputDispatcher : MonoBehaviour
{
    public static InputDispatcher Instance { get; private set; }

    public event System.Action<Vector2> OnPointerPressed;
    public event System.Action<Vector2> OnPointerDragged;
    public event System.Action OnPointerReleased;

    public bool IsPressed { get; private set; }
    public Vector2 Position { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null)
        {
            IsPressed = false;
            return;
        }
        bool pressed = pointer.press.isPressed;
        Vector2 pos = pointer.position.ReadValue();
        if (pressed)
        {
            Position = pos;
            if (!IsPressed)
            {
                IsPressed = true;
                OnPointerPressed?.Invoke(pos);
            }
            else
            {
                OnPointerDragged?.Invoke(pos);
            }
        }
        else
        {
            if (IsPressed)
            {
                IsPressed = false;
                OnPointerReleased?.Invoke();
            }
        }
    }
}
