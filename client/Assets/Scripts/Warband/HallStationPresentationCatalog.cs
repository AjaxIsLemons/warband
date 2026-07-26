using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-first presentation identity for the five parts of the Hourstone Hall. Gameplay legality,
/// economy, and run commands deliberately do not live here.
/// </summary>
[Serializable]
internal sealed class HallStationPresentationDefinition
{
    public string id = "";
    public string title = "";
    public string eyebrow = "";
    public string compassSlot = "";
    public string motionVerb = "";
    public string audioFamily = "";
    public float cameraX;
    public float cameraY;
    public float cameraZ;
    public float targetX;
    public float targetY;
    public float targetZ;

    public HallStation Station
    {
        get
        {
            if (!Enum.TryParse(id, true, out HallStation station) ||
                station == HallStation.Overview)
                throw new InvalidOperationException($"[HallCatalog] Unknown station id '{id}'.");
            return station;
        }
    }

    public Pose CameraPose
    {
        get
        {
            var position = new Vector3(cameraX, cameraY, cameraZ);
            var target = new Vector3(targetX, targetY, targetZ);
            return new Pose(position, Quaternion.LookRotation(target - position, Vector3.up));
        }
    }
}

[Serializable]
internal sealed class HallStationPresentationData
{
    public HallStationPresentationDefinition[] stations =
        Array.Empty<HallStationPresentationDefinition>();
}

internal sealed class HallStationPresentationCatalog
{
    private static HallStationPresentationCatalog s_shared;

    private readonly Dictionary<HallStation, HallStationPresentationDefinition> _definitions =
        new Dictionary<HallStation, HallStationPresentationDefinition>();

    public static HallStationPresentationCatalog Shared =>
        s_shared ??= Load();

    public HallStationPresentationDefinition this[HallStation station]
    {
        get
        {
            if (station == HallStation.Overview)
                return null;
            if (!_definitions.TryGetValue(station, out var definition))
                throw new InvalidOperationException(
                    $"[HallCatalog] Missing presentation for {station}.");
            return definition;
        }
    }

    public Pose PoseFor(HallStation station)
    {
        if (station == HallStation.Overview || station == HallStation.Breach)
            return new Pose(
                new Vector3(0f, 9.1f, -11.6f),
                Quaternion.LookRotation(
                    new Vector3(0f, 0.2f, 0.4f) - new Vector3(0f, 9.1f, -11.6f),
                    Vector3.up));
        return this[station].CameraPose;
    }

    public static void Validate()
    {
        HallStationPresentationCatalog catalog = Shared;
        var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (HallStation station in new[]
                 {
                     HallStation.Market, HallStation.Warband, HallStation.Armory,
                     HallStation.Hourstone, HallStation.Breach,
                 })
        {
            HallStationPresentationDefinition definition = catalog[station];
            Require(!string.IsNullOrWhiteSpace(definition.title),
                $"{station} needs a title");
            Require(!string.IsNullOrWhiteSpace(definition.motionVerb),
                $"{station} needs a motion verb");
            Require(!string.IsNullOrWhiteSpace(definition.audioFamily),
                $"{station} needs an audio family");
            Require(slots.Add(definition.compassSlot),
                $"compass slot '{definition.compassSlot}' is duplicated");
        }
    }

    private static HallStationPresentationCatalog Load()
    {
        TextAsset asset = Resources.Load<TextAsset>("UI/hall-stations");
        if (asset == null)
            throw new InvalidOperationException(
                "[HallCatalog] Resources/UI/hall-stations.json is required.");
        HallStationPresentationData data =
            JsonUtility.FromJson<HallStationPresentationData>(asset.text);
        if (data?.stations == null)
            throw new InvalidOperationException("[HallCatalog] Station data could not be read.");

        var result = new HallStationPresentationCatalog();
        foreach (HallStationPresentationDefinition definition in data.stations)
        {
            HallStation station = definition.Station;
            if (!result._definitions.TryAdd(station, definition))
                throw new InvalidOperationException(
                    $"[HallCatalog] Duplicate station '{definition.id}'.");
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[HallCatalog] " + message);
    }
}
