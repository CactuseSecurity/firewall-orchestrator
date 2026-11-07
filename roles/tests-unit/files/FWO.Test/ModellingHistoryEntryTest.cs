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
    public void UserIdIsShownWhenTheUserCannotBeResolved()
    {
        // e.g. a user of another tenant: the stored changer is the writer's free text and is not shown
        ModellingHistoryEntry unresolved = new() { Changer = "someone-else", ChangerId = 8 };
        ModellingHistoryEntry withoutName = new()
        {
            Changer = "someone-else",
            ChangerId = 8,
            ChangerUser = new UiUser { DbId = 8, Name = "" }
        };

        Assert.Multiple(() =>
        {
            Assert.That(unresolved.GetAuthenticatedChangerName(), Is.EqualTo("8"));
            Assert.That(withoutName.GetAuthenticatedChangerName(), Is.EqualTo("8"));
        });
    }

    [Test]
    public void ChangerNameIsTheAuthenticatedNameAndIsNotSerialized()
    {
        ModellingHistoryEntry entry = new()
        {
            Changer = "someone-else",
            ChangerId = 8,
            ChangerUser = new UiUser { DbId = 8, Name = "planner" }
        };

        Assert.Multiple(() =>
        {
            Assert.That(entry.ChangerName, Is.EqualTo("planner"));
            Assert.That(JsonConvert.SerializeObject(entry), Does.Not.Contain("ChangerName"));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(entry), Does.Not.Contain("ChangerName"));
        });
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
