using FWO.Basics;
using FWO.Data.Ai;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the role filters of the AI functions: reading and writing the stored JSON array of role names, and deciding
/// whether a user holding certain roles may use a function.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AiFunctionRolesTest
{
    private static readonly List<string> kNoRoles = [];
    private static readonly List<string> kAdminRoles = [Roles.Admin];
    private static readonly List<string> kAdminAndModellerRoles = [Roles.Admin, Roles.Modeller];
    private static readonly List<string> kReporterRoles = [Roles.Reporter];
    private static readonly List<string> kReporterAndModellerRoles = [Roles.Reporter, Roles.Modeller];
    private static readonly List<string> kUpperCaseAdminRoles = ["ADMIN"];
    private static readonly List<string?> kUnnormalizedRoles = [" modeller ", "Admin", "admin", "", "  ", null];
    private static readonly List<string> kNormalizedRoles = ["Admin", "modeller"];

    /// <summary>
    /// Verifies that a missing or blank value is an empty filter, as the setting was not stored yet.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(AiFunctionRoles.kNoRestriction)]
    public void TryParse_WithoutRoles_ReadsEmptyFilter(string? text)
    {
        bool readable = AiFunctionRoles.TryParse(text, out List<string> roles);

        Assert.That(readable, Is.True);
        Assert.That(roles, Is.Empty);
    }

    /// <summary>
    /// Verifies that the stored role names are trimmed, blank names and duplicates (ignoring case) dropped and the
    /// remaining names sorted.
    /// </summary>
    [Test]
    public void TryParse_NormalizesRoles()
    {
        bool readable = AiFunctionRoles.TryParse("[\" modeller \",\"Admin\",\"admin\",\"\",\"  \",null]", out List<string> roles);

        Assert.That(readable, Is.True);
        Assert.That(roles, Is.EqualTo(kNormalizedRoles));
    }

    /// <summary>
    /// Verifies that a value that is not a JSON array of role names cannot be read and gives no roles, so that the
    /// caller switches the function off instead of allowing every user.
    /// </summary>
    [TestCase("admin")]
    [TestCase("null")]
    [TestCase("{\"role\":\"admin\"}")]
    [TestCase("[1,2]")]
    [TestCase("[\"admin\"")]
    [TestCase("[[\"admin\"]]")]
    public void TryParse_WithUnreadableValue_Fails(string text)
    {
        bool readable = AiFunctionRoles.TryParse(text, out List<string> roles);

        Assert.That(readable, Is.False);
        Assert.That(roles, Is.Empty);
    }

    /// <summary>
    /// Verifies that the roles are stored normalized, so that the same roles always give the same value, and that the
    /// stored value is read back.
    /// </summary>
    [Test]
    public void Serialize_StoresNormalizedRolesThatAreReadBack()
    {
        string stored = AiFunctionRoles.Serialize(kUnnormalizedRoles);
        bool readable = AiFunctionRoles.TryParse(stored, out List<string> readBack);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo("[\"Admin\",\"modeller\"]"));
            Assert.That(AiFunctionRoles.Serialize(kNoRoles), Is.EqualTo(AiFunctionRoles.kNoRestriction));
            Assert.That(readable, Is.True);
            Assert.That(readBack, Is.EqualTo(kNormalizedRoles));
        });
    }

    /// <summary>
    /// Verifies that role names are stored as JSON strings, so that quotes and markup in a name do not change the
    /// structure of the stored value.
    /// </summary>
    [Test]
    public void Serialize_EscapesRoleNames()
    {
        const string kOddRoleName = "<b>\"role\"</b>,x";
        List<string> oddRoles = [kOddRoleName];

        bool readable = AiFunctionRoles.TryParse(AiFunctionRoles.Serialize(oddRoles), out List<string> readBack);

        Assert.That(readable, Is.True);
        Assert.That(readBack, Is.EqualTo(oddRoles));
    }

    /// <summary>
    /// Verifies that missing roles are refused.
    /// </summary>
    [Test]
    public void Serialize_WithoutRoles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AiFunctionRoles.Serialize(null!));
    }

    /// <summary>
    /// Verifies that an empty filter allows every user, also one without roles, and that a filter allows a user holding
    /// at least one of its roles, ignoring case, and no other user.
    /// </summary>
    [Test]
    public void IsAllowed_NeedsOneOfTheRolesUnlessFilterIsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AiFunctionRoles.IsAllowed(kNoRoles, kNoRoles), Is.True);
            Assert.That(AiFunctionRoles.IsAllowed(kNoRoles, kReporterRoles), Is.True);
            Assert.That(AiFunctionRoles.IsAllowed(kAdminAndModellerRoles, kReporterAndModellerRoles), Is.True);
            Assert.That(AiFunctionRoles.IsAllowed(kAdminRoles, kUpperCaseAdminRoles), Is.True);
            Assert.That(AiFunctionRoles.IsAllowed(kAdminAndModellerRoles, kReporterRoles), Is.False);
            Assert.That(AiFunctionRoles.IsAllowed(kAdminRoles, kNoRoles), Is.False);
        });
    }

    /// <summary>
    /// Verifies that a missing filter or missing user roles are refused.
    /// </summary>
    [Test]
    public void IsAllowed_WithoutFilterOrUserRoles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AiFunctionRoles.IsAllowed(null!, kAdminRoles));
        Assert.Throws<ArgumentNullException>(() => AiFunctionRoles.IsAllowed(kAdminRoles, null!));
    }
}
