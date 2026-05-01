using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects user taps (mouse in Editor, touch on iPhone) and forwards them as toggle
/// commands to whichever ComponentController was hit. Sits on the ARCamera so the
/// raycast origin is always the actual AR view frustum.
/// </summary>
public class TapHandler : MonoBehaviour
{
    [Tooltip("Camera used for screen->world raycasts. Defaults to the AR camera on this GameObject, then Camera.main.")]
    [SerializeField] private Camera arCamera;

    [Tooltip("Maximum raycast distance in meters. AR markers are usually <2m away.")]
    [SerializeField] private float rayMaxDistance = 10f;

    void Awake()
    {
        if (arCamera == null) arCamera = GetComponent<Camera>();
        if (arCamera == null) arCamera = Camera.main;
    }

    void Update()
    {
        if (!TryGetTapPosition(out Vector2 screenPos)) return;
        HandleTap(screenPos);
    }

    /// <summary>Returns true on the frame a new tap/click began. Single source for both Mouse and Touch.</summary>
    private static bool TryGetTapPosition(out Vector2 pos)
    {
        // Touch first — on iOS this is the only path that fires.
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        // Mouse fallback for in-Editor testing with the webcam.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pos = Mouse.current.position.ReadValue();
            return true;
        }
        pos = default;
        return false;
    }

    private void HandleTap(Vector2 screenPos)
    {
        if (arCamera == null) return;

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance)) return;

        // Walk up the parent chain to find the ComponentController. The collider we hit is
        // a single primitive part, but the controller lives on the prefab root.
        var controller = hit.collider.GetComponentInParent<ComponentController>();
        if (controller == null) return;

        controller.Toggle();
        Debug.Log($"[TapHandler] {controller.componentName} → " +
                  (controller.IsDisassembled ? "DISASSEMBLED" : "ASSEMBLED"));
    }
}
