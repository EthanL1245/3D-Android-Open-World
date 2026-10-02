using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Prevents Unity's SRP Rendering Debugger overlay from appearing in device builds.
/// On mobile Development Builds Unity normally toggles this UI with a three-finger
/// double tap, which can be triggered accidentally while using the joystick/HUD.
/// Keep the editor debugger available; disable only actual player builds.
/// </summary>
public static class DisableRuntimeRenderingDebugger
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Disable()
    {
#if !UNITY_EDITOR
        if(DebugManager.instance!=null)
            DebugManager.instance.enableRuntimeUI=false;
#endif
    }
}
