using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Freezes game time and audio. Anything that runs on its own clock (the song, the recording) listens to Paused/Resumed.
public class GamePause : MonoBehaviour
{
    [SerializeField] private InputAction _toggleAction = CreateToggleAction();

    public bool IsPaused { get; private set; }

    public event Action Paused;
    public event Action Resumed;

    private static InputAction CreateToggleAction()
    {
        var action = new InputAction("Toggle Pause", InputActionType.Button);
        action.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        action.AddBinding("<Keyboard>/p");
        return action;
    }

    private void OnEnable()
    {
        _toggleAction.Enable();
    }

    private void OnDisable()
    {
        _toggleAction.Disable();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1.0f;
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (_toggleAction.WasPressedThisFrame())
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }
        IsPaused = true;
        Time.timeScale = 0.0f;
        AudioListener.pause = true;
        Paused?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }
        IsPaused = false;
        Time.timeScale = 1.0f;
        AudioListener.pause = false;
        Resumed?.Invoke();
    }
}
