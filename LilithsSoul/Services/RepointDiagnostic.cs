using System;
using System.Linq;
using System.Reflection;
using ProjectM;
using ProjectM.UI;
using Stunlock.Core;
using LilithsSoul.Foundation;

// ============================================================
//  RepointDiagnostic — LilithsSoul  (TEMPORARY PROBE, v8)
//  LilithsSoul/Services/RepointDiagnostic.cs
//
//  THROWAWAY DIAGNOSTIC — delete after confirmation. NOT in .aidevs.
//
//  Why v8 — find the CALLER that knows both item AND tooltip:
//  ----------------------------------------------------------
//  v7 found TooltipManagerComponent.SetTooltip(LocalizationKey/String,...)
//  — a generic display service with NO item context. The item→text
//  decision happens in its CALLER: an inventory/hover handler that takes
//  an item (ManagedItemData / Entity) and reads its Description. That
//  caller is the Option-C patch target, and it is NOT named "Tooltip"
//  necessarily (could be InventoryEntry, ItemSlot, HoverState, etc.), and
//  it lives in a client-UI assembly v7 didn't scan.
//
//  What v8 does:
//  -------------
//  Scans ALL loaded assemblies (skipping obvious noise: System/Unity
//  core/BepInEx/Il2CppInterop) for INSTANCE methods that BOTH:
//    (a) take an item-identifying parameter (ManagedItemData / Entity /
//        InventoryItem*), AND
//    (b) look tooltip/description/hover-related by method OR declaring-type
//        name (Tooltip, Description, Hover, Inspect, Examine, ItemSlot,
//        Entry, Inventory).
//  Filtering on (a)+(b) keeps output to real candidates regardless of how
//  the type is named.
//
//  Also: explicitly lists any method ANYWHERE whose parameters include
//  LocalizedStringBuilderBase (the Description builder type) — whoever
//  consumes that builder is reading item tooltips.
//
//  NO MUTATION. Read-only reflection over type metadata.
//
//  [PERFORMANCE] One-shot. Metadata-only scan; bounded by hard line cap.
//                No game-data iteration, no per-frame cost.
// ============================================================

namespace LilithsSoul.Services;

public static class RepointDiagnostic
{
    private const string LOG_SOURCE = "LilithsSoul.RepointDiagnostic";

    // (a) parameter types proving the method knows which item.
    static readonly string[] ItemParamMarkers =
        { "ManagedItemData", "Entity", "InventoryItem", "ItemData" };

    // (b) method- or type-name markers for the item-tooltip path.
    static readonly string[] ContextMarkers =
        { "Tooltip", "Description", "Hover", "Inspect", "Examine",
          "ItemSlot", "Entry", "Inventory", "ItemPanel" };

    // Assemblies to skip (framework noise — never hosts V Rising UI).
    static readonly string[] SkipAssemblies =
        { "System", "mscorlib", "netstandard", "Unity", "UnityEngine",
          "BepInEx", "Il2CppInterop", "Il2Cppmscorlib", "Il2CppSystem",
          "Mono", "0Harmony", "Newtonsoft" };

    static int _lines;
    const int LineCap = 900;

    public static void Run(int guidHash = 0)
    {
        _lines = 0;
        SoulLogger.Info(LOG_SOURCE, "───── tooltip-caller probe (v8, all assemblies) ─────");

        var builderType = typeof(LocalizedStringBuilderBase);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                     .OrderBy(a => a.GetName().Name, StringComparer.Ordinal))
        {
            var asmName = asm.GetName().Name ?? "";
            if (SkipAssemblies.Any(s => asmName.StartsWith(s, StringComparison.Ordinal)))
                continue;

            Type[] types;
            try   { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            catch { continue; }

            foreach (var t in types)
            {
                if (t == null) continue;
                ScanType(t, asmName, builderType);
                if (_lines >= LineCap) { SoulLogger.Info(LOG_SOURCE, "..."); goto done; }
            }
        }

        done:
        SoulLogger.Info(LOG_SOURCE, "───── end probe ─────");
    }

    static void ScanType(Type t, string asmName, Type builderType)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static
                                 | BindingFlags.DeclaredOnly;

        MethodInfo[] methods;
        try   { methods = t.GetMethods(flags); }
        catch { return; }

        bool typeNameContext = ContextMarkers.Any(mk =>
            t.Name.Contains(mk, StringComparison.OrdinalIgnoreCase));

        foreach (var m in methods)
        {
            ParameterInfo[] pars;
            try   { pars = m.GetParameters(); }
            catch { continue; }

            bool takesBuilder = pars.Any(p => p.ParameterType == builderType);

            bool itemShaped = pars.Any(p => ItemParamMarkers.Any(mk =>
                p.ParameterType.Name.Contains(mk, StringComparison.Ordinal)));

            bool methodNameContext = ContextMarkers.Any(mk =>
                m.Name.Contains(mk, StringComparison.OrdinalIgnoreCase));

            // Report if: consumes the Description builder (strongest signal),
            // OR (knows an item AND is in a tooltip/inventory context).
            bool report = takesBuilder
                       || (itemShaped && (methodNameContext || typeNameContext));

            if (!report) continue;

            var sig  = string.Join(", ", pars.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            var kind = m.IsStatic ? "static " : "";
            var tag  = takesBuilder ? "   ⇐ TAKES Description BUILDER"
                     : "   ⇐ knows item + tooltip context";
            Log($"[{asmName}] {t.FullName}.{m.Name} : {kind}{m.ReturnType.Name}({sig}){tag}");
        }
    }

    static void Log(string msg)
    {
        if (_lines >= LineCap) { _lines++; return; }
        SoulLogger.Info(LOG_SOURCE, msg);
        _lines++;
    }
}