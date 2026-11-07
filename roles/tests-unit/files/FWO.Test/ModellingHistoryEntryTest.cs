using FWO.Data;
using FWO.Data.Modelling;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the author attribution of a change history row: the name of the user behind changer_id wins
/// over the free-text changer column, which older rows may carry as written by an arbitrary client.
/// </summary>
[TestFixture]
internal class ModellingHistoryEntryTest
{
    [Test]
    public void ResolvedUserNameWinsOverFreeTextChanger()
    {
        ModellingHistoryEntry entry = new()
        {
            Changer = "someone-else",
            ChangerId = 8,
            ChangerUser = new UiUser { DbId = 8, Name = "planner" }
        };

        Assert.That(entry.GetAuthenticatedChangerName(), Is.EqualTo("planner"));
    }

    [Test]
    public void StoredChangerIsUsedWithoutResolvedUser()
    {
        ModellingHistoryEntry entry = new() { Changer = "middleware-server", ChangerId = null };

        Assert.That(entry.GetAuthenticatedChangerName(), Is.EqualTo("middleware-server"));
    }

    [Test]
    public void StoredChangerIsUsedWhenResolvedUserHasNoName()
    {
        ModellingHistoryEntry entry = new()
        {
            Changer = "planner",
            ChangerId = 8,
            ChangerUser = new UiUser { DbId = 8, Name = "" }
        };

        Assert.That(entry.GetAuthenticatedChangerName(), Is.EqualTo("planner"));
    }

    [Test]
    public void MissingChangerYieldsEmptyName()
    {
        ModellingHistoryEntry entry = new() { Changer = null! };

        Assert.That(entry.GetAuthenticatedChangerName(), Is.EqualTo(""));
    }

    [Test]
    public void ChangerUserIsReadFromTheQueryResult()
    {
        const string kStoredRow = "{\"changer\":\"forged-name\",\"changer_id\":8,\"changer_user\":{\"uiuser_username\":\"planner\"}}";

        ModellingHistoryEntry entry = JsonConvert.DeserializeObject<ModellingHistoryEntry>(kStoredRow)!;
        ModellingHistoryEntry systemTextEntry = System.Text.Json.JsonSerializer.Deserialize<ModellingHistoryEntry>(kStoredRow)!;

        Assert.Multiple(() =>
        {
            Assert.That(entry.GetAuthenticatedChangerName(), Is.EqualTo("planner"));
            Assert.That(systemTextEntry.GetAuthenticatedChangerName(), Is.EqualTo("planner"));
        });
    }
}
