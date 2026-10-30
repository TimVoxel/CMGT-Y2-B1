using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Skips ahead through the show for testing, with a keyboard key or a secret controller combo for when you're in the headset:
// - skip one step: both thumbsticks clicked in and held together
// - skip to the end screen: both triggers held together
public class DebugSkipInput : MonoBehaviour
{
    private class HeldCombo
    {
        private float _heldFor;
        private bool _hasFired;

        public bool Update(bool isHeld, float holdDuration)
        {
            if (!isHeld)
            {
                _heldFor = 0.0f;
                _hasFired = false;
                return false;
            }

            _heldFor += Time.unscaledDeltaTime;
            if (_hasFired || _heldFor < holdDuration)
            {
                return false;
            }
            _hasFired = true;
            return true;
        }
    }

    [SerializeField] private InputAction _keyAction = new InputAction("Skip", InputActionType.Button, "<Keyboard>/f2");
    [SerializeField] private InputAction _leftComboAction = new InputAction("Skip Combo Left", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
    [SerializeField] private InputAction _rightComboAction = new InputAction("Skip Combo Right", InputActionType.Button, "<XRController>{RightHand}/{Primary2DAxisClick}");
    [SerializeField] private float _comboHoldDuration = 0.75f;

    [Header("Skip to end")]
    [SerializeField] private InputAction _endKeyAction = new InputAction("Skip To End", InputActionType.Button, "<Keyboard>/f3");
    [SerializeField] private InputAction _leftEndComboAction = new InputAction("Skip To End Combo Left", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
    [SerializeField] private InputAction _rightEndComboAction = new InputAction("Skip To End Combo Right", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
    [SerializeField] private float _endComboHoldDuration = 2.0f;

    [SerializeField] private bool _allowInReleaseBuilds;

    private readonly HeldCombo _skipCombo = new HeldCombo();
    private readonly HeldCombo _endCombo = new HeldCombo();

    public event Action SkipPressed;
    public event Action SkipToEndPressed;

    private InputAction[] Actions => new[] { _keyAction, _leftComboAction, _rightComboAction, _endKeyAction, _leftEndComboAction, _rightEndComboAction };

    private void OnEnable()
    {
        foreach (var action in Actions)
        {
            action.Enable();
        }
    }

    private void OnDisable()
    {
        foreach (var action in Actions)
        {
            action.Disable();
        }
    }

    private void Update()
    {
        if (!Debug.isDebugBuild && !_allowInReleaseBuilds)
        {
            return;
        }

        if (_keyAction.WasPressedThisFrame()
            || _skipCombo.Update(_leftComboAction.IsPressed() && _rightComboAction.IsPressed(), _comboHoldDuration))
        {
            SkipPressed?.Invoke();
        }

        if (_endKeyAction.WasPressedThisFrame()
            || _endCombo.Update(_leftEndComboAction.IsPressed() && _rightEndComboAction.IsPressed(), _endComboHoldDuration))
        {
            SkipToEndPressed?.Invoke();
        }
    }
}
