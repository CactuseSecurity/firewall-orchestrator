using FWO.Data.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Covers the UI helpers of the device specific settings import and snapshots.</summary>
[TestFixture]
internal class DeviceSpecificSettingsPreviewRowTest
{
    [Test]
    public void EveryChangeOfThePlanBecomesOneRow()
    {
        DeviceSpecificSettingsImportPlan plan = new()
        {
            Nodes =
            [
                new DeviceSpecificSettingsNodeChanges
                {
                    Name = "Global",
                    Changes =
                    [
                        new DeviceSpecificSettingChange { Key = "scUrl", Kind = DeviceSpecificSettingChangeKind.Added, NewValue = "\"https://sc\"" },
                        new DeviceSpecificSettingChange { Key = "installOn", Kind = DeviceSpecificSettingChangeKind.Removed, OldValue = "\"ANY\"" }
                    ]
                },
                new DeviceSpecificSettingsNodeChanges { Name = "m", UnchangedCount = 3 }
            ]
        };

        List<DeviceSpecificSettingsPreviewRow> rows = DeviceSpecificSettingsPreviewRow.FromPlan(plan);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0], Is.EqualTo(new DeviceSpecificSettingsPreviewRow("Global", "scUrl", DeviceSpecificSettingChangeKind.Added, "", "\"https://sc\"")));
            Assert.That(rows[1].OldValue, Is.EqualTo("\"ANY\""));
            Assert.That(rows[1].NewValue, Is.Empty);
            Assert.That(rows[1].Change, Is.EqualTo("Removed"));
        });
    }

    [TestCase("baseline", "baseline.fwo-dss.json")]
    [TestCase("a/b:c*d?\"e<f>g|h\\i", "a_b_c_d__e_f_g_h_i.fwo-dss.json")]
    public void SnapshotFileNamesReplaceCharactersNotAllowedInFileNames(string snapshotName, string expected)
    {
        Assert.That(DeviceSpecificSettingsSnapshots.SnapshotFileName(snapshotName), Is.EqualTo(expected));
    }
}
