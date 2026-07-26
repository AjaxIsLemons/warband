using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only metadata keyed by stable content ids. Mechanical values never live here:
/// cards get those from the composed UnitDef. This catalog owns the vocabulary and art hooks a
/// player needs in order to recognise a unit before reading its numbers.
/// </summary>
internal sealed class PresentationCatalog
{
    [Serializable]
    private sealed class Document
    {
        public int version = 1;
        public UnitPresentation[] units = Array.Empty<UnitPresentation>();
        public ContentPresentation[] content = Array.Empty<ContentPresentation>();
    }

    [Serializable]
    internal sealed class UnitPresentation
    {
        public string id = "";
        public string portrait = "";
        public string role = "";
        public string roleIcon = "";
        public string musterRole = "";
        public string musterRoleIcon = "";
        public string musterSignatureKeyword = "";
        public string musterPassiveKeyword = "";
        public string abilityName = "";
        public string abilityIcon = "";
        public string abilityTrigger = "";
        public string abilitySummary = "";
        public string passiveName = "";
        public string passiveIcon = "";
        public string passiveTrigger = "";
        public string passiveSummary = "";
        public string[] keywords = Array.Empty<string>();
        public string accent = "";
    }

    [Serializable]
    internal sealed class ContentPresentation
    {
        public string id = "";
        public string icon = "";
        public string summary = "";
        public string accent = "";
    }

    private readonly Dictionary<string, UnitPresentation> _units =
        new Dictionary<string, UnitPresentation>(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentPresentation> _content =
        new Dictionary<string, ContentPresentation>(StringComparer.Ordinal);

    public int Version { get; private set; }

    public static PresentationCatalog Load()
    {
        var result = new PresentationCatalog();
        var asset = Resources.Load<TextAsset>("UI/UnitPresentation");
        if (asset == null)
        {
            Debug.LogError("[UI] Resources/UI/UnitPresentation.json is required.");
            return result;
        }

        try
        {
            var document = JsonUtility.FromJson<Document>(asset.text);
            result.Version = document?.version ?? 0;
            if (document?.units == null) return result;

            foreach (var unit in document.units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.id)) continue;
                if (!result._units.TryAdd(unit.id, unit))
                    Debug.LogWarning($"[UI] Duplicate unit presentation id '{unit.id}'.");
            }
            if (document.content != null)
                foreach (var entry in document.content)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                    if (!result._content.TryAdd(entry.id, entry))
                        Debug.LogWarning($"[UI] Duplicate content presentation id '{entry.id}'.");
                }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UI] Could not parse UnitPresentation.json: {ex.Message}");
        }
        return result;
    }

    public UnitPresentation Unit(string id)
    {
        if (_units.TryGetValue(id, out var value)) return value;
        return new UnitPresentation
        {
            id = id,
            portrait = "",
            role = "CHAMPION",
            roleIcon = "◆",
            musterRole = "Champion",
            musterRoleIcon = "frontline",
            musterSignatureKeyword = "Special",
            musterPassiveKeyword = "Innate",
            abilityName = "SIGNATURE",
            abilityIcon = "✦",
            abilityTrigger = "SIGNATURE",
            abilitySummary = "Inspect this champion for full combat information.",
            passiveName = "TRAIT",
            passiveIcon = "◇",
            passiveTrigger = "PASSIVE",
            passiveSummary = "No presentation entry authored.",
            keywords = Array.Empty<string>(),
            accent = "utility",
        };
    }

    public ContentPresentation Content(string id)
    {
        if (_content.TryGetValue(id, out var value)) return value;
        return new ContentPresentation
        {
            id = id,
            icon = "◇",
            summary = "Applies a visible rule described by its mechanical card.",
            accent = "utility",
        };
    }
}
