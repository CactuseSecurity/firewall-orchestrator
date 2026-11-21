using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Covers storing, listing, reading and deleting device specific settings snapshots.</summary>
[TestFixture]
internal class DeviceSpecificSettingsSnapshotServiceTest
{
    [Test]
    public async Task ASnapshotIsStoredListedReadAndDeleted()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceSpecificSettingsSnapshotService service = new(api);
        DeviceSpecificSettingsPackage package = new() { FwoVersion = "9.9.4", Nodes = [new DeviceSpecificSettingsPackageNode { Level = "global" }] };

        DeviceSpecificSettingsSnapshotSummary first = await service.AddSnapshotAsync(" first ", "  ", package);
        DeviceSpecificSettingsSnapshotSummary second = await service.AddSnapshotAsync("second", "with description", package);
        List<DeviceSpecificSettingsSnapshotSummary> listed = await service.GetSnapshotsAsync(null, 10, 0);
        DeviceSpecificSettingsSnapshotData? read = await service.GetSnapshotAsync(first.Id);
        await service.DeleteSnapshotAsync(first.Id);

        Assert.Multiple(() =>
        {
            Assert.That(first.Name, Is.EqualTo("first"), "the name is trimmed");
            Assert.That(api.Snapshots.Single().Id, Is.EqualTo(second.Id));
            Assert.That(listed.Select(snapshot => snapshot.Name), Is.EqualTo(new List<string> { "second", "first" }), "newest first");
            Assert.That(listed[1].Description, Is.Null, "a blank description is stored as null");
            Assert.That(read, Is.Not.Null);
            Assert.That(DeviceSpecificSettingsPackageJson.FromStorage(read!.Content).Nodes.Single().Level, Is.EqualTo("global"));
            Assert.That(read.FwoVersion, Is.EqualTo("9.9.4"));
        });
    }

    [Test]
    public void AnInvalidSnapshotIsRejectedWithEveryProblem()
    {
        DeviceSpecificSettingsSnapshotService service = new(new InMemoryDeviceSpecificSettingsApiConnection());
        DeviceSpecificSettingsPackage invalidPackage = new() { Version = 2 };

        InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.AddSnapshotAsync(" ", null, invalidPackage));
        List<DeviceSpecificSettingsImportIssue> issues = DeviceSpecificSettingsSnapshotService.ValidateSnapshot(
            new string('n', DeviceSpecificSettingsSnapshotSummary.kMaxNameLength + 1),
            new string('d', DeviceSpecificSettingsSnapshotSummary.kMaxDescriptionLength + 1), null);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("name").And.Contain("package.version"));
            Assert.That(issues.Select(issue => issue.Path), Is.EqualTo(new List<string> { "name", "description", "package" }));
        });
    }

    [Test]
    public void TheFilterOnlyContainsSetCriteriaAndEscapesWildcards()
    {
        DateTimeOffset created = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

        Dictionary<string, object> empty = DeviceSpecificSettingsSnapshotService.BuildWhere(null);
        Dictionary<string, object> all = DeviceSpecificSettingsSnapshotService.BuildWhere(new DeviceSpecificSettingsSnapshotFilter
        {
            Id = 3,
            Name = "50%_a\\b",
            Description = "test",
            FwoVersion = "9.9.4",
            Created = created
        });

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Empty);
            Assert.That(all.Keys, Is.EquivalentTo(["id", "name", "description", "fwo_version", "created"]));
            Assert.That(((Dictionary<string, object>)all["name"])["_ilike"], Is.EqualTo("%50\\%\\_a\\\\b%"));
            Assert.That(((Dictionary<string, object>)all["id"])["_eq"], Is.EqualTo(3L));
            Assert.That(((Dictionary<string, object>)all["created"])["_eq"], Is.EqualTo(created));
        });
    }
}
