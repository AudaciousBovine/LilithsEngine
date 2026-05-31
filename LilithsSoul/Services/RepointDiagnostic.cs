using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectM;
using ProjectM.UI;            // LocalizedStringBuilderBase
using Stunlock.Localization;  // LocalizationKey, AssetGuid
using Stunlock.Core;          // PrefabGUID
using LilithsSoul.Foundation;

// ============================================================
//  RepointDiagnostic — LilithsSoul  (TEMPORARY PROBE, v5)
//  LilithsSoul/Services/RepointDiagnostic.cs
//
//  THROWAWAY DIAGNOSTIC — delete after confirmation. NOT in .aidevs.
//
//  Why v5 — confirm the tooltip source:
//  ------------------------------------
//  Read-back proved item.Description does NOT persist a repoint:
//  after `item.Description = builder`, re-getting Description still
//  resolves vanilla. So the Description PROPERTY round-trips through
//  a copy, and the real tooltip source is elsewhere.
//
//  v5 enumerates ManagedItemData's ENTIRE member surface (all fields
//  + properties, full base chain) so we can find:
//    - a BACKING FIELD behind Description we could write directly
//      (property setters often have a private backing field), and/or
//    - any OTHER description/tooltip-bearing member the UI reads.
//
//  It also reports, for Name and Description properties, whether each
//  is auto-implemented (has a <Name>k__BackingField) so we know if a
//  direct field write is even an option.
//
//  NO MUTATION. Read-only reflection.
//
//  [PERFORMANCE] One-shot. Single type's member surface. Trivial.
// ============================================================

namespace LilithsSoul.Services;

public static class RepointDiagnostic
{
    private const string LOG_SOURCE = "LilithsSoul.RepointDiagnostic";

    private const int BoneSwordGuidHash = -2085919458;
    private const string KnownDescCompact = "01e7d9c32bf44c6094d41e75be7cf658";

    public static void Run(int guidHash = BoneSwordGuidHash)
    {
        if (Soul.ClientWorld == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "Client world not ready — cannot run probe.");
            return;
        }

        var registry = Soul.ClientWorld
            .GetExistingSystemManaged<ManagedDataSystem>()
            ?.ManagedDataRegistry;
        if (registry == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "ManagedDataRegistry not available — cannot run probe.");
            return;
        }

        var item = registry.GetOrDefault<ManagedItemData>(new PrefabGUID(guidHash));
        if (item == null)
        {
            SoulLogger.Warning(LOG_SOURCE, $"No ManagedItemData for GUID {guidHash} — cannot run probe.");
            return;
        }

        SoulLogger.Info(LOG_SOURCE, $"───── ManagedItemData full member surface (GUID {guidHash}) ─────");
        SoulLogger.Info(LOG_SOURCE, $"(vanilla sword DescKey compact = {KnownDescCompact})");

        var type = item.GetType();
        while (type != null
               && type != typeof(object)
               && !type.Name.Contains("Il2CppObjectBase", StringComparison.Ordinal))
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public
                                     | BindingFlags.NonPublic   | BindingFlags.Instance;

            foreach (var f in type.GetFields(flags))
            {
                string val;
                try   { val = f.GetValue(item)?.ToString() ?? "null"; }
                catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                var line = $"[field] {type.Name}.{f.Name} : {f.FieldType.Name} = {Trim(val)}";
                if (LooksDescriptionRelated(f.Name, f.FieldType.Name, val)) line += "   ⇐ description-related?";
                SoulLogger.Info(LOG_SOURCE, line);
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                string val;
                try   { val = p.GetValue(item)?.ToString() ?? "null"; }
                catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                var setter = p.GetSetMethod(true);
                var setInfo = setter == null ? "no-set" : (setter.IsPublic ? "set:public" : "set:nonpublic");
                var line = $"[prop ] {type.Name}.{p.Name} : {p.PropertyType.Name} [{setInfo}] = {Trim(val)}";
                if (LooksDescriptionRelated(p.Name, p.PropertyType.Name, val)) line += "   ⇐ description-related?";
                SoulLogger.Info(LOG_SOURCE, line);
            }

            type = type.BaseType;
        }

        // Explicitly report whether Name/Description are auto-props with backing fields.
        ReportBackingField(item.GetType(), "Description");
        ReportBackingField(item.GetType(), "Name");

        SoulLogger.Info(LOG_SOURCE, "───── end dump ─────");
    }

    // Auto-implemented properties compile to a "<PropName>k__BackingField".
    // If one exists we can write it directly, bypassing a copy-returning setter.
    static void ReportBackingField(Type type, string propName)
    {
        var backingName = $"<{propName}>k__BackingField";
        FieldInfo? f = null;
        var t = type;
        while (t != null && f == null && t != typeof(object))
        {
            f = t.GetField(backingName, BindingFlags.NonPublic | BindingFlags.Instance);
            t = t.BaseType;
        }
        SoulLogger.Info(LOG_SOURCE,
            f == null
              ? $"[backing] {propName}: NO auto-backing field (custom getter/setter — likely the copy path)."
              : $"[backing] {propName}: HAS backing field '{f.Name}' : {f.FieldType.Name} — directly writable.");
    }

    static bool LooksDescriptionRelated(string memberName, string typeName, string value)
    {
        if (memberName.IndexOf("desc", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (memberName.IndexOf("tooltip", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (typeName.Contains("LocalizedString", StringComparison.Ordinal)) return true;
        if (value.Contains(KnownDescCompact, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string Trim(string s) => s.Length <= 110 ? s : s.Substring(0, 107) + "...";
}