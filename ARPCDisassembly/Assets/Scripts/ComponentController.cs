using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One PC component (motherboard, GPU, or PSU). Owns its parts (DisassemblablePart children)
/// and toggles them between assembled and disassembled state. Holds metadata (display name +
/// specs) shown in the spec UI overlay when the user taps the component.
/// </summary>
public class ComponentController : MonoBehaviour
{
    [Tooltip("Human-readable name shown in the spec UI (e.g. 'Motherboard').")]
    public string componentName = "Component";

    [Tooltip("Spec rows shown in the UI panel when this component is tapped.")]
    public List<SpecEntry> specs = new List<SpecEntry>();

    /// <summary>Key/value spec row, e.g. ("Form Factor", "ATX").</summary>
    [System.Serializable]
    public class SpecEntry
    {
        public string label;
        public string value;
    }

    public bool IsDisassembled { get; private set; }

    private DisassemblablePart[] parts;

    void Awake()
    {
        // Cache the part list once. GetComponentsInChildren is a deep search — picks up
        // every DisassemblablePart in the prefab, which is exactly what we want.
        parts = GetComponentsInChildren<DisassemblablePart>(includeInactive: true);
    }

    /// <summary>Toggle between assembled and disassembled.</summary>
    public void Toggle()
    {
        if (IsDisassembled) Assemble();
        else Disassemble();
    }

    public void Disassemble()
    {
        if (IsDisassembled) return;
        IsDisassembled = true;
        foreach (var p in parts) p.MoveToDisassembled();
        SpecUI.Instance.Show(this, componentName, specs);
    }

    public void Assemble()
    {
        if (!IsDisassembled) return;
        IsDisassembled = false;
        foreach (var p in parts) p.MoveToAssembled();
        SpecUI.Instance.Hide(this);
    }
}
