using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only utility that generates the 3 PC component prefabs (motherboard, GPU, PSU)
/// from primitives. Run via menu: <b>Tools → Build PC Components</b>.
///
/// Why a builder script instead of placing primitives by hand:
///   - Reproducibility: tweak a constant, regenerate, see the result.
///   - Rich hierarchies: 20+ primitives per component would be tedious to position manually.
///   - Both assembled AND disassembled positions are encoded once, in code, with full control.
///
/// The script is idempotent: re-running it overwrites the existing prefabs without piling
/// up duplicates. Materials are shared between matching parts (one Mat_PCB across all the
/// boards, etc.), so changing a colour later is one edit.
/// </summary>
public static class PCComponentBuilder
{
    private const string PREFAB_DIR = "Assets/Prefabs/Components";
    private const string MAT_DIR = "Assets/Materials/Components";

    // ─── Shared colour palette (PBR-friendly, slightly desaturated to read well in AR) ───
    private static readonly Color PCB_GREEN = new(0.10f, 0.40f, 0.15f);
    private static readonly Color METAL_SILVER = new(0.78f, 0.78f, 0.80f);
    private static readonly Color BLACK_PLASTIC = new(0.08f, 0.08f, 0.08f);
    private static readonly Color DARK_GREY = new(0.20f, 0.20f, 0.22f);
    private static readonly Color RAM_BLUE = new(0.08f, 0.20f, 0.55f);
    private static readonly Color GOLD_PIN = new(0.85f, 0.70f, 0.20f);
    private static readonly Color WARNING_RED = new(0.78f, 0.10f, 0.10f);
    private static readonly Color HEATSINK_COPPER = new(0.72f, 0.45f, 0.20f);
    private static readonly Color CONNECTOR_WHITE = new(0.90f, 0.90f, 0.90f);

    // Per-build material cache — populated as parts are added, looked up by semantic name.
    private static Dictionary<string, Material> matCache;

    [MenuItem("Tools/Build PC Components")]
    public static void BuildAll()
    {
        EnsureDir(PREFAB_DIR);
        EnsureDir(MAT_DIR);
        matCache = new Dictionary<string, Material>();

        SaveAsPrefab(WithSpecUI(BuildMotherboard()), "MotherboardModel");
        SaveAsPrefab(WithSpecUI(BuildGPU()), "GPUModel");
        SaveAsPrefab(WithSpecUI(BuildPSU()), "PSUModel");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PCComponentBuilder] Built 3 prefabs at {PREFAB_DIR}/");
        EditorUtility.DisplayDialog("PC Components Built",
            "3 prefabs are ready in Assets/Prefabs/Components.\n\n" +
            "Next:\n" +
            "  1. Delete the placeholder cubes under each ImageTarget.\n" +
            "  2. Drag the matching prefab onto each ImageTarget in the Hierarchy.\n" +
            "  3. Re-run if you tweak a constant in the script.",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MOTHERBOARD — a flat green PCB with a CPU stack, RAM bank, capacitors,
    //  bridge chips, I/O backplate, and a PCIe slot. Disassembly lifts everything
    //  off the PCB along +Y to reveal the board underneath.
    // ─────────────────────────────────────────────────────────────────────────
    private static GameObject BuildMotherboard()
    {
        var root = new GameObject("MotherboardModel");
        var ctrl = root.AddComponent<ComponentController>();
        ctrl.componentName = "Motherboard";
        ctrl.specs = new List<ComponentController.SpecEntry>
        {
            new() { label = "Form Factor",  value = "ATX" },
            new() { label = "Chipset",      value = "Intel Z790" },
            new() { label = "Socket",       value = "LGA 1700" },
            new() { label = "Memory",       value = "DDR5 up to 7200 MHz" },
            new() { label = "Memory Slots", value = "4x DIMM (max 128 GB)" },
            new() { label = "Expansion",    value = "PCIe 5.0 x16" },
        };

        // Base PCB — anchor, doesn't move.
        AddPart(root, "PCB", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.0025f, 0),
            disassembled: new Vector3(0, 0.0025f, 0),
            scale: new Vector3(0.16f, 0.005f, 0.16f),
            material: GetMat("PCB", PCB_GREEN));

        // CPU socket (lifts a bit, then the heatsink lifts much further to reveal it)
        AddPart(root, "CPUSocket", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.009f, 0.02f),
            disassembled: new Vector3(0, 0.045f, 0.02f),
            scale: new Vector3(0.04f, 0.008f, 0.04f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        AddPart(root, "CPUHeatsink", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.026f, 0.02f),
            disassembled: new Vector3(0, 0.090f, 0.02f),
            scale: new Vector3(0.05f, 0.025f, 0.05f),
            material: GetMat("MetalSilver", METAL_SILVER));

        // 4 RAM sticks on the right edge — fan out vertically
        for (int i = 0; i < 4; i++)
        {
            float x = 0.045f + i * 0.012f;
            AddPart(root, $"RAM_{i + 1}", PrimitiveType.Cube,
                assembled: new Vector3(x, 0.014f, -0.01f),
                disassembled: new Vector3(x, 0.060f + i * 0.008f, -0.01f),
                scale: new Vector3(0.005f, 0.022f, 0.06f),
                material: GetMat("RAM", RAM_BLUE));
        }

        // 6 capacitors scattered across the board
        var caps = new (float x, float z)[]
        {
            (-0.05f, 0.04f), (-0.04f, -0.04f), (0.05f, -0.04f),
            (-0.06f, 0.0f),  (0.04f, 0.05f),  (-0.02f, -0.06f)
        };
        for (int i = 0; i < caps.Length; i++)
        {
            var (x, z) = caps[i];
            AddPart(root, $"Capacitor_{i + 1}", PrimitiveType.Cylinder,
                assembled: new Vector3(x, 0.011f, z),
                disassembled: new Vector3(x, 0.050f + (i % 3) * 0.008f, z),
                scale: new Vector3(0.008f, 0.008f, 0.008f),
                material: GetMat("BlackPlastic", BLACK_PLASTIC));
        }

        // Northbridge chip
        AddPart(root, "NorthBridge", PrimitiveType.Cube,
            assembled: new Vector3(-0.04f, 0.008f, 0.045f),
            disassembled: new Vector3(-0.04f, 0.050f, 0.045f),
            scale: new Vector3(0.025f, 0.005f, 0.025f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // Southbridge chip (smaller, opposite corner)
        AddPart(root, "SouthBridge", PrimitiveType.Cube,
            assembled: new Vector3(0.04f, 0.008f, -0.06f),
            disassembled: new Vector3(0.04f, 0.040f, -0.06f),
            scale: new Vector3(0.020f, 0.005f, 0.020f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // I/O backplate on the left edge — slides outward (-X)
        AddPart(root, "IOBackplate", PrimitiveType.Cube,
            assembled: new Vector3(-0.077f, 0.018f, 0.04f),
            disassembled: new Vector3(-0.140f, 0.018f, 0.04f),
            scale: new Vector3(0.005f, 0.030f, 0.06f),
            material: GetMat("MetalSilver", METAL_SILVER));

        // PCIe slot on the bottom edge
        AddPart(root, "PCIeSlot", PrimitiveType.Cube,
            assembled: new Vector3(0.0f, 0.009f, -0.045f),
            disassembled: new Vector3(0.0f, 0.045f, -0.045f),
            scale: new Vector3(0.10f, 0.005f, 0.012f),
            material: GetMat("PCIeSlotWhite", CONNECTOR_WHITE));

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GPU — long thin PCB with a cooler shroud, dual fans, heatsink, GPU die,
    //  VRAM chips, gold PCIe edge connector, and display ports. Shroud + fans
    //  lift highest, exposing the heatsink, then the chips on the PCB.
    // ─────────────────────────────────────────────────────────────────────────
    private static GameObject BuildGPU()
    {
        var root = new GameObject("GPUModel");
        var ctrl = root.AddComponent<ComponentController>();
        ctrl.componentName = "Graphics Card";
        ctrl.specs = new List<ComponentController.SpecEntry>
        {
            new() { label = "GPU",       value = "NVIDIA RTX 4070" },
            new() { label = "Memory",    value = "12 GB GDDR6X" },
            new() { label = "CUDA Cores",value = "5,888" },
            new() { label = "Bus",       value = "PCIe 4.0 x16" },
            new() { label = "TDP",       value = "200 W" },
            new() { label = "Outputs",   value = "1x HDMI 2.1, 3x DP 1.4a" },
        };

        AddPart(root, "PCB", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.0025f, 0),
            disassembled: new Vector3(0, 0.0025f, 0),
            scale: new Vector3(0.18f, 0.005f, 0.07f),
            material: GetMat("PCB", PCB_GREEN));

        // Cooler shroud — covers the upper half, lifts highest
        AddPart(root, "CoolerShroud", PrimitiveType.Cube,
            assembled: new Vector3(0.01f, 0.022f, 0.005f),
            disassembled: new Vector3(0.01f, 0.110f, 0.005f),
            scale: new Vector3(0.155f, 0.025f, 0.060f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // 2 fans visible on top of the shroud
        AddPart(root, "Fan_Left", PrimitiveType.Cylinder,
            assembled: new Vector3(-0.040f, 0.036f, 0.005f),
            disassembled: new Vector3(-0.080f, 0.140f, 0.005f),
            scale: new Vector3(0.045f, 0.003f, 0.045f),
            material: GetMat("FanGrey", new Color(0.15f, 0.15f, 0.15f)));

        AddPart(root, "Fan_Right", PrimitiveType.Cylinder,
            assembled: new Vector3(0.040f, 0.036f, 0.005f),
            disassembled: new Vector3(0.080f, 0.140f, 0.005f),
            scale: new Vector3(0.045f, 0.003f, 0.045f),
            material: GetMat("FanGrey", new Color(0.15f, 0.15f, 0.15f)));

        // Copper heatsink under the shroud (revealed when shroud lifts)
        AddPart(root, "Heatsink", PrimitiveType.Cube,
            assembled: new Vector3(0.01f, 0.013f, 0.005f),
            disassembled: new Vector3(0.01f, 0.065f, 0.005f),
            scale: new Vector3(0.135f, 0.012f, 0.05f),
            material: GetMat("HeatsinkCopper", HEATSINK_COPPER));

        // GPU die at center
        AddPart(root, "GPUDie", PrimitiveType.Cube,
            assembled: new Vector3(0.01f, 0.008f, 0.005f),
            disassembled: new Vector3(0.01f, 0.040f, 0.005f),
            scale: new Vector3(0.025f, 0.005f, 0.025f),
            material: GetMat("MetalSilver", METAL_SILVER));

        // 8 VRAM chips around the die — spread radially
        var vram = new (float x, float z)[]
        {
            (-0.025f, 0.012f), (-0.025f, -0.008f),
            ( 0.045f, 0.012f), ( 0.045f, -0.008f),
            ( 0.010f, 0.020f), ( 0.010f, -0.020f),
            (-0.005f, 0.020f), ( 0.025f, -0.020f),
        };
        for (int i = 0; i < vram.Length; i++)
        {
            var (x, z) = vram[i];
            // Disassembled: spread further out from center + lift
            float xOut = x + (x - 0.01f) * 0.6f;
            float zOut = z * 1.6f;
            AddPart(root, $"VRAM_{i + 1}", PrimitiveType.Cube,
                assembled: new Vector3(x, 0.008f, z),
                disassembled: new Vector3(xOut, 0.035f, zOut),
                scale: new Vector3(0.012f, 0.003f, 0.008f),
                material: GetMat("BlackPlastic", BLACK_PLASTIC));
        }

        // Gold PCIe edge connector at the bottom — slides downward
        AddPart(root, "PCIeConnector", PrimitiveType.Cube,
            assembled: new Vector3(0.020f, 0.005f, -0.030f),
            disassembled: new Vector3(0.020f, 0.005f, -0.080f),
            scale: new Vector3(0.090f, 0.005f, 0.005f),
            material: GetMat("GoldPin", GOLD_PIN));

        // 3 display ports on the left edge — slide outward (-X)
        for (int i = 0; i < 3; i++)
        {
            float z = -0.020f + i * 0.018f;
            AddPart(root, $"DisplayPort_{i + 1}", PrimitiveType.Cube,
                assembled: new Vector3(-0.092f, 0.014f, z),
                disassembled: new Vector3(-0.150f, 0.014f, z),
                scale: new Vector3(0.005f, 0.012f, 0.014f),
                material: GetMat("BlackPlastic", BLACK_PLASTIC));
        }

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PSU — boxy enclosure with 4 walls + bottom plate, top fan grille, internal
    //  fan, big main capacitor, secondary cap, copper heatsink, and a power switch.
    //  Disassembly: walls split outward, top lifts off, internals rise to view.
    // ─────────────────────────────────────────────────────────────────────────
    private static GameObject BuildPSU()
    {
        var root = new GameObject("PSUModel");
        var ctrl = root.AddComponent<ComponentController>();
        ctrl.componentName = "Power Supply";
        ctrl.specs = new List<ComponentController.SpecEntry>
        {
            new() { label = "Wattage",     value = "850 W" },
            new() { label = "Efficiency",  value = "80+ Gold (87% @ 50% load)" },
            new() { label = "Modularity",  value = "Fully modular" },
            new() { label = "Connectors",  value = "1x ATX 24, 2x EPS 8, 4x PCIe 6+2" },
            new() { label = "Form Factor", value = "ATX (140 × 150 × 86 mm)" },
            new() { label = "Cooling",     value = "120 mm hydraulic bearing fan" },
        };

        // Bottom plate — anchor, doesn't move
        AddPart(root, "BottomPlate", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.0025f, 0),
            disassembled: new Vector3(0, 0.0025f, 0),
            scale: new Vector3(0.150f, 0.005f, 0.140f),
            material: GetMat("DarkGrey", DARK_GREY));

        // 4 side walls — split outward like an exploded carton
        AddPart(root, "Wall_Front", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.040f, -0.067f),
            disassembled: new Vector3(0, 0.040f, -0.180f),
            scale: new Vector3(0.150f, 0.075f, 0.005f),
            material: GetMat("DarkGrey", DARK_GREY));

        AddPart(root, "Wall_Back", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.040f, 0.067f),
            disassembled: new Vector3(0, 0.040f, 0.180f),
            scale: new Vector3(0.150f, 0.075f, 0.005f),
            material: GetMat("DarkGrey", DARK_GREY));

        AddPart(root, "Wall_Left", PrimitiveType.Cube,
            assembled: new Vector3(-0.072f, 0.040f, 0),
            disassembled: new Vector3(-0.180f, 0.040f, 0),
            scale: new Vector3(0.005f, 0.075f, 0.140f),
            material: GetMat("DarkGrey", DARK_GREY));

        AddPart(root, "Wall_Right", PrimitiveType.Cube,
            assembled: new Vector3(0.072f, 0.040f, 0),
            disassembled: new Vector3(0.180f, 0.040f, 0),
            scale: new Vector3(0.005f, 0.075f, 0.140f),
            material: GetMat("DarkGrey", DARK_GREY));

        // Top fan grille — lifts straight up
        AddPart(root, "TopGrille", PrimitiveType.Cube,
            assembled: new Vector3(0, 0.080f, 0),
            disassembled: new Vector3(0, 0.180f, 0),
            scale: new Vector3(0.135f, 0.005f, 0.135f),
            material: GetMat("MetalSilver", METAL_SILVER));

        // Internal 120mm fan — under the grille
        AddPart(root, "InternalFan", PrimitiveType.Cylinder,
            assembled: new Vector3(0, 0.070f, 0),
            disassembled: new Vector3(0, 0.140f, 0),
            scale: new Vector3(0.110f, 0.003f, 0.110f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // Main bulk capacitor (the big intimidating one)
        AddPart(root, "MainCapacitor", PrimitiveType.Cylinder,
            assembled: new Vector3(-0.040f, 0.040f, -0.030f),
            disassembled: new Vector3(-0.080f, 0.110f, -0.060f),
            scale: new Vector3(0.030f, 0.035f, 0.030f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // Secondary capacitor
        AddPart(root, "SecondaryCap", PrimitiveType.Cylinder,
            assembled: new Vector3(0.045f, 0.030f, -0.020f),
            disassembled: new Vector3(0.090f, 0.090f, -0.040f),
            scale: new Vector3(0.020f, 0.025f, 0.020f),
            material: GetMat("BlackPlastic", BLACK_PLASTIC));

        // Internal copper heatsink
        AddPart(root, "Heatsink", PrimitiveType.Cube,
            assembled: new Vector3(0.0f, 0.030f, 0.040f),
            disassembled: new Vector3(0.0f, 0.080f, 0.080f),
            scale: new Vector3(0.100f, 0.030f, 0.025f),
            material: GetMat("HeatsinkCopper", HEATSINK_COPPER));

        // Red power switch on the back wall area
        AddPart(root, "PowerSwitch", PrimitiveType.Cube,
            assembled: new Vector3(-0.050f, 0.030f, 0.067f),
            disassembled: new Vector3(-0.130f, 0.080f, 0.180f),
            scale: new Vector3(0.015f, 0.010f, 0.005f),
            material: GetMat("WarningRed", WARNING_RED));

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static GameObject AddPart(GameObject parent, string name, PrimitiveType type,
        Vector3 assembled, Vector3 disassembled, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent.transform, worldPositionStays: false);
        go.transform.localPosition = assembled;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        var dp = go.AddComponent<DisassemblablePart>();
        dp.assembledLocalPosition = assembled;
        dp.disassembledLocalPosition = disassembled;

        return go;
    }

    /// <summary>Get-or-create a Material asset by semantic name. Cached per-build.</summary>
    private static Material GetMat(string name, Color color)
    {
        if (matCache.TryGetValue(name, out var cached)) return cached;

        var path = $"{MAT_DIR}/Mat_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")) { color = color };
            mat.SetFloat("_Glossiness", 0.30f);  // subtle gloss reads better in AR
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            // Refresh colour in case constants changed between runs
            mat.color = color;
            EditorUtility.SetDirty(mat);
        }
        matCache[name] = mat;
        return mat;
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    /// <summary>Attach a SpecUI child to the prefab root. The UI builds itself at runtime.</summary>
    private static GameObject WithSpecUI(GameObject root)
    {
        var uiGo = new GameObject("SpecUI");
        uiGo.transform.SetParent(root.transform, worldPositionStays: false);
        uiGo.AddComponent<SpecUI>();
        return root;
    }

    private static void SaveAsPrefab(GameObject root, string name)
    {
        var path = $"{PREFAB_DIR}/{name}.prefab";
        // SaveAsPrefabAsset overwrites in place and preserves the prefab's GUID, so scene
        // instances (children of ImageTargets) stay linked and pick up structural changes
        // — including newly added child GameObjects like SpecUI — on the next refresh.
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);  // remove the temporary scene instance
    }
}
