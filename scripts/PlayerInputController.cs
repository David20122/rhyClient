using System;
using Godot;

public partial class PlayerInputController : Node
{
    // I'm not sure if it's planned that the player can change keybindings
    // So these bindings are hardcoded (for now) -thom

    /// <summary>
    /// Fired when the mouse moves.
    /// <para>
    /// First parameter is relative movement (delta),
    /// second parameter is absolute screen position.
    /// </para>
    /// </summary>
    public event Action<Vector2, Vector2> OnMouseMove;

    public event Action OnTogglePaused;
    public event Action OnRestartPressed;
    public event Action OnToggleReplayViewerVisibility;
    public event Action OnPauseOrSkip;
    public event Action OnToggleFade;
    public event Action OnTogglePushback;
    public event Action<bool> OnLeftMouseButton;
    public bool IsEnabled { get; private set;  } = true;

    public override void _Input(InputEvent @event)
    {
        if (!IsEnabled) return;

        handleInput(@event);
    }

    private void handleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion:
            case InputEventMouseButton { ButtonIndex: MouseButton.Left }:
                handleMouseInput(@event);
                break;
            case InputEventKey {Keycode: Key.Escape}:
            case InputEventKey {Keycode: Key.F1}:
            case InputEventKey {Keycode: Key.Space}:
            case InputEventKey {Keycode: Key.F}:
            case InputEventKey {Keycode: Key.P}:
            case InputEventKey {Keycode: Key.Quoteleft}:
                handleKeyboardInput(@event);
                break;
        }
    }

    private void handleMouseInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
            OnMouseMove?.Invoke(motion.Relative, motion.Position);

        if (@event is InputEventMouseButton mouseButton)
        {
            if (!mouseButton.Pressed || mouseButton.DoubleClick) return;

            OnLeftMouseButton?.Invoke(mouseButton.Pressed);
        }
    }

    private void handleKeyboardInput(InputEvent @event)
    {
        InputEventKey key = (InputEventKey)@event;

        if (!key.Pressed || key.Echo) return;

        switch (key)
        {
            case {Keycode: Key.Escape}:
                OnTogglePaused?.Invoke();
                break;
            case {Keycode: Key.Quoteleft}:
                OnRestartPressed?.Invoke();
                break;
            case {Keycode: Key.F1}:
                OnToggleReplayViewerVisibility?.Invoke();
                break;
            case {Keycode: Key.Space}:
                OnPauseOrSkip?.Invoke();
                break;
            case {Keycode: Key.F}:
                OnToggleFade?.Invoke();
                break;
            case {Keycode: Key.P}:
                OnTogglePushback?.Invoke();
                break;
        }
    }

    public void ToggleState() => IsEnabled = !IsEnabled;
}
