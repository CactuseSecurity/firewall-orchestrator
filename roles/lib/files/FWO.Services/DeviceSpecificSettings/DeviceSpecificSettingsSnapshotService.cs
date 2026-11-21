using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>
/// Optional criteria for listing snapshots; a null criterion does not restrict the result. Names and descriptions match
/// case-insensitively as substrings, all other criteria exactly.
/// </summary>
public sealed class DeviceSpecificSettingsSnapshotFilter
{
    /// <summary>Snapshot ID; null for any.</summary>
    public long? Id { get; set; }

    /// <summary>Part of the name; null for any.</summary>
    public string? Name { get; set; }

    /// <summary>Part of the description; null for any.</summary>
    public string? Description { get; set; }

    /// <summary>FWO version; null for any.</summary>
    public string? FwoVersion { get; set; }

    /// <summary>Creation time; null for any.</summary>
    public DateTimeOffset? Created { get; set; }
}

/// <summary>
/// Stores, lists, reads and deletes named versions of the device specific settings (table
/// <c>device_specific_config_snapshot</c>). A snapshot holds a package; it is applied through the import.
/// </summary>
public sealed class DeviceSpecificSettingsSnapshotService(ApiConnection apiConnection)
{
    /// <summary>
    /// Lists snapshots, newest first and then by descending ID, matching the filter.
    /// </summary>
    /// <param name="filter">Criteria; null lists all snapshots.</param>
    /// <param name="limit">Number of snapshots to read.</param>
    /// <param name="offset">Number of snapshots to skip.</param>
    public async Task<List<DeviceSpecificSettingsSnapshotSummary>> GetSnapshotsAsync(DeviceSpecificSettingsSnapshotFilter? filter, int limit, int offset)
    {
        return await apiConnection.SendQueryAsync<List<DeviceSpecificSettingsSnapshotSummary>>(
            DeviceSpecificSettingsQueries.getSnapshots, new { where = BuildWhere(filter), limit, offset }) ?? [];
    }

    /// <summary>Reads a snapshot with its package, or null if it does not exist.</summary>
    public async Task<DeviceSpecificSettingsSnapshotData?> GetSnapshotAsync(long id)
    {
        return await apiConnection.SendQueryAsync<DeviceSpecificSettingsSnapshotData?>(DeviceSpecificSettingsQueries.getSnapshotById, new { id });
    }

    /// <summary>
    /// Stores a package as a new snapshot after checking name, description and the package structure. The package is
    /// not checked against this installation, so that versions of other installations can be kept as well.
    /// </summary>
    /// <exception cref="InvalidDataException">Name, description or package are invalid; the message names every problem.</exception>
    public async Task<DeviceSpecificSettingsSnapshotSummary> AddSnapshotAsync(string name, string? description, DeviceSpecificSettingsPackage package)
    {
        List<DeviceSpecificSettingsImportIssue> errors = ValidateSnapshot(name, description, package);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(" ", errors.Select(error => $"{error.Path}: {error.Message}")));
        }

        return await apiConnection.SendQueryAsync<DeviceSpecificSettingsSnapshotSummary>(DeviceSpecificSettingsQueries.addSnapshot, new
        {
            name = name.Trim(),
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            fwoVersion = package.FwoVersion,
            content = DeviceSpecificSettingsPackageJson.ToStorage(package)
        });
    }

    /// <summary>Deletes a snapshot.</summary>
    public async Task DeleteSnapshotAsync(long id)
    {
        await apiConnection.SendQueryAsync<object>(DeviceSpecificSettingsQueries.deleteSnapshot, new { id });
    }

    /// <summary>
    /// Checks the name (required, at most <see cref="DeviceSpecificSettingsSnapshotSummary.kMaxNameLength"/> characters),
    /// the description (at most <see cref="DeviceSpecificSettingsSnapshotSummary.kMaxDescriptionLength"/> characters) and
    /// the structure of the package. Paths are relative to the snapshot: <c>name</c>, <c>description</c>, <c>package.*</c>.
    /// </summary>
    public static List<DeviceSpecificSettingsImportIssue> ValidateSnapshot(string? name, string? description, DeviceSpecificSettingsPackage? package)
    {
        List<DeviceSpecificSettingsImportIssue> errors = [];
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new("name", "A snapshot name is required."));
        }
        else if (name.Trim().Length > DeviceSpecificSettingsSnapshotSummary.kMaxNameLength)
        {
            errors.Add(new("name", $"A snapshot name must not be longer than {DeviceSpecificSettingsSnapshotSummary.kMaxNameLength} characters."));
        }

        if (description is not null && description.Trim().Length > DeviceSpecificSettingsSnapshotSummary.kMaxDescriptionLength)
        {
            errors.Add(new("description", $"A snapshot description must not be longer than {DeviceSpecificSettingsSnapshotSummary.kMaxDescriptionLength} characters."));
        }

        errors.AddRange(DeviceSpecificSettingsPackageValidator.ValidateStructure(package)
            .Select(error => new DeviceSpecificSettingsImportIssue(error.Path.Length == 0 ? "package" : $"package.{error.Path}", error.Message)));
        return errors;
    }

    /// <summary>Builds the GraphQL filter from the set criteria only.</summary>
    public static Dictionary<string, object> BuildWhere(DeviceSpecificSettingsSnapshotFilter? filter)
    {
        Dictionary<string, object> where = [];
        if (filter is null)
        {
            return where;
        }
        if (filter.Id is long id)
        {
            where["id"] = new Dictionary<string, object> { ["_eq"] = id };
        }
        if (filter.Name is not null)
        {
            where["name"] = new Dictionary<string, object> { ["_ilike"] = ContainsPattern(filter.Name) };
        }
        if (filter.Description is not null)
        {
            where["description"] = new Dictionary<string, object> { ["_ilike"] = ContainsPattern(filter.Description) };
        }
        if (filter.FwoVersion is not null)
        {
            where["fwo_version"] = new Dictionary<string, object> { ["_eq"] = filter.FwoVersion };
        }
        if (filter.Created is DateTimeOffset created)
        {
            where["created"] = new Dictionary<string, object> { ["_eq"] = created };
        }
        return where;
    }

    /// <summary>An ILIKE pattern matching the text anywhere, with the wildcards and the escape character of the text escaped.</summary>
    private static string ContainsPattern(string text)
    {
        string escaped = text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
