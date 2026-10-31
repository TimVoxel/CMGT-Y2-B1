using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Double-press the button to have the xylophone re-fit to the player's current height, e.g. after standing up or
// sitting down. A double press rather than a single one so a stray tap doesn't move it mid-song.
public class RecalibrateHeightInput : MonoBehaviour
{
    [SerializeField] private InputAction _pressAction = CreatePressAction();
    [SerializeField] private float _doublePressWindow = 0.4f;

    private float _lastPressTime = float.NegativeInfinity;
    private bool _wasPressed;

    public event Action RecalibrateRequested;

    private static InputAction CreatePressAction()
    {
        var action = new InputAction("Recalibrate Height", InputActionType.Button);
        action.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        action.AddBinding("<Keyboard>/h");
        return action;
    }

    private void OnEnable()
    {
        _pressAction.Enable();
    }

    private void OnDisable()
    {
        _pressAction.Disable();
        _wasPressed = false;
    }

    private void Update()
    {
        var isPressed = _pressAction.IsPressed();
        if (isPressed && !_wasPressed)
        {
            var now = Time.unscaledTime;
            if (now - _lastPressTime <= _doublePressWindow)
            {
                _lastPressTime = float.NegativeInfinity;
                Debug.Log("[Height] Recalibration requested");
                RecalibrateRequested?.Invoke();
            }
            else
            {
                _lastPressTime = now;
            }
        }
        _wasPressed = isPressed;
    }
}
