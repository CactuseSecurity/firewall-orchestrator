using FWO.Data.Ai;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the additional HTTP headers of the AI settings: how they are read from and stored in the global config, and
/// which names and values are refused, so that they can neither carry credentials nor end the header line.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AiCustomHeaderListTest
{
    private const string kProfileHeader = "ki-services-profile-name";
    private const string kProfileValue = "com_1659_oheppidxtsa";
    private const string kIssuerHeader = "token-issuer";
    private const string kIssuerValue = "keycloak";
    private const string kStoredHeaders = """[{"name":" ki-services-profile-name ","value":" com_1659_oheppidxtsa "},null,{"name":"","value":" "},{"name":"token-issuer"}]""";
    private const char kNameChar = 'n';
    private const char kValueChar = 'v';

    private static readonly List<AiSettingsProblem> kNoProblems = [];
    private static readonly List<AiSettingsProblem> kTooMany = [AiSettingsProblem.CustomHeadersTooMany];
    private static readonly List<AiSettingsProblem> kNameReserved = [AiSettingsProblem.CustomHeaderNameReserved];
    private static readonly List<AiSettingsProblem> kNameDuplicate = [AiSettingsProblem.CustomHeaderNameDuplicate];
    private static readonly List<AiSettingsProblem> kValueInvalid = [AiSettingsProblem.CustomHeaderValueInvalid];
    private static readonly List<string> kReadNames = [kProfileHeader, kIssuerHeader];
    private static readonly List<string> kReadValues = [kProfileValue, ""];
    private static readonly List<AiSettingsProblem> kAllHeaderProblems =
    [
        AiSettingsProblem.CustomHeadersTooMany,
        AiSettingsProblem.CustomHeaderNameInvalid,
        AiSettingsProblem.CustomHeaderNameReserved,
        AiSettingsProblem.CustomHeaderNameDuplicate,
        AiSettingsProblem.CustomHeaderValueInvalid
    ];

    /// <summary>
    /// Verifies that a blank stored value stands for no headers, as the setting was not stored yet.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(AiCustomHeaderList.kNone)]
    public void TryParse_WithoutHeaders_ReturnsNone(string? storedHeaders)
    {
        bool readable = AiCustomHeaderList.TryParse(storedHeaders, out List<AiCustomHeader> headers);

        Assert.That(readable, Is.True);
        Assert.That(headers, Is.Empty);
    }

    /// <summary>
    /// Verifies that the stored headers are read in their order, trimmed, without null entries and blank rows; a
    /// missing value is read as empty.
    /// </summary>
    [Test]
    public void TryParse_ReadsNormalizedHeaders()
    {
        bool readable = AiCustomHeaderList.TryParse(kStoredHeaders, out List<AiCustomHeader> headers);

        Assert.Multiple(() =>
        {
            Assert.That(readable, Is.True);
            Assert.That(headers.Select(header => header.Name), Is.EqualTo(kReadNames));
            Assert.That(headers.Select(header => header.Value), Is.EqualTo(kReadValues));
        });
    }

    /// <summary>
    /// Verifies that a stored value that is not a JSON array of headers cannot be read.
    /// </summary>
    [TestCase("{not json")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("[1]")]
    [TestCase("\"text\"")]
    public void TryParse_WithUnreadableValue_ReturnsFalse(string storedHeaders)
    {
        bool readable = AiCustomHeaderList.TryParse(storedHeaders, out List<AiCustomHeader> headers);

        Assert.That(readable, Is.False);
        Assert.That(headers, Is.Empty);
    }

    /// <summary>
    /// Verifies that the stored value is normalized and is read back unchanged.
    /// </summary>
    [Test]
    public void Serialize_StoresNormalizedHeadersThatAreReadBack()
    {
        List<AiCustomHeader?> headers =
        [
            new AiCustomHeader { Name = $" {kProfileHeader} ", Value = $" {kProfileValue} " },
            null,
            new AiCustomHeader { Name = " ", Value = "" },
            new AiCustomHeader { Name = kIssuerHeader, Value = kIssuerValue }
        ];

        string storedHeaders = AiCustomHeaderList.Serialize(headers);
        bool readable = AiCustomHeaderList.TryParse(storedHeaders, out List<AiCustomHeader> readHeaders);

        Assert.Multiple(() =>
        {
            Assert.That(storedHeaders, Is.EqualTo("""[{"name":"ki-services-profile-name","value":"com_1659_oheppidxtsa"},{"name":"token-issuer","value":"keycloak"}]"""));
            Assert.That(readable, Is.True);
            Assert.That(AiCustomHeaderList.Serialize(readHeaders), Is.EqualTo(storedHeaders));
        });
    }

    /// <summary>
    /// Verifies that no headers are stored as the default value.
    /// </summary>
    [Test]
    public void Serialize_WithoutHeaders_StoresNone()
    {
        List<AiCustomHeader?> blankHeaders = [new AiCustomHeader()];

        Assert.That(AiCustomHeaderList.Serialize(blankHeaders), Is.EqualTo(AiCustomHeaderList.kNone));
    }

    /// <summary>
    /// Verifies that the headers of an API gateway, the longest name and value and the maximum number of headers are
    /// accepted.
    /// </summary>
    [Test]
    public void FindProblems_WithValidHeaders_FindsNone()
    {
        List<AiCustomHeader> gatewayHeaders =
        [
            new AiCustomHeader { Name = kProfileHeader, Value = kProfileValue },
            new AiCustomHeader { Name = kIssuerHeader, Value = kIssuerValue },
            new AiCustomHeader { Name = "X-Special!#$%&'*+.^_`|~", Value = "value with\ttab and \"quotes\" <b>" },
            new AiCustomHeader { Name = new string(kNameChar, AiCustomHeaderList.kMaxNameLength), Value = new string(kValueChar, AiCustomHeaderList.kMaxValueLength) }
        ];
        List<AiCustomHeader> maxHeaders = CreateHeaders(AiCustomHeaderList.kMaxHeaders);

        Assert.That(AiCustomHeaderList.FindProblems(gatewayHeaders), Is.EqualTo(kNoProblems));
        Assert.That(AiCustomHeaderList.FindProblems(maxHeaders), Is.EqualTo(kNoProblems));
    }

    /// <summary>
    /// Verifies that more headers than allowed are refused.
    /// </summary>
    [Test]
    public void FindProblems_WithTooManyHeaders_FindsProblem()
    {
        List<AiCustomHeader> headers = CreateHeaders(AiCustomHeaderList.kMaxHeaders + 1);

        Assert.That(AiCustomHeaderList.FindProblems(headers), Is.EqualTo(kTooMany));
    }

    /// <summary>
    /// Verifies that a name that is missing, not a token of RFC 9110 or too long is refused.
    /// </summary>
    [TestCase("")]
    [TestCase(" ki-services-profile-name")]
    [TestCase("profile name")]
    [TestCase("profile:name")]
    [TestCase("profile\r\nX-Injected")]
    [TestCase("pröfile")]
    [TestCase("profile(name)")]
    public void FindProblems_WithInvalidName_FindsProblem(string name)
    {
        List<AiCustomHeader> headers = [new AiCustomHeader { Name = name, Value = kProfileValue }];

        Assert.That(AiCustomHeaderList.FindProblems(headers), Does.Contain(AiSettingsProblem.CustomHeaderNameInvalid));
        Assert.That(AiCustomHeaderList.IsValidName(name), Is.False);
    }

    /// <summary>
    /// Verifies that a name longer than allowed is refused.
    /// </summary>
    [Test]
    public void IsValidName_WithTooLongName_IsFalse()
    {
        Assert.That(AiCustomHeaderList.IsValidName(new string(kNameChar, AiCustomHeaderList.kMaxNameLength + 1)), Is.False);
        Assert.That(AiCustomHeaderList.IsValidName(null), Is.False);
    }

    /// <summary>
    /// Verifies that the headers carrying the credentials of the providers and those framing the message are refused,
    /// ignoring case.
    /// </summary>
    [TestCase("Authorization")]
    [TestCase("authorization")]
    [TestCase("Proxy-Authorization")]
    [TestCase("Cookie")]
    [TestCase("X-API-KEY")]
    [TestCase("api-key")]
    [TestCase("x-goog-api-key")]
    [TestCase("Host")]
    [TestCase("content-type")]
    [TestCase("Content-Length")]
    [TestCase("Transfer-Encoding")]
    [TestCase("Connection")]
    public void FindProblems_WithReservedName_FindsProblem(string name)
    {
        List<AiCustomHeader> headers = [new AiCustomHeader { Name = name, Value = kProfileValue }];

        Assert.That(AiCustomHeaderList.FindProblems(headers), Is.EqualTo(kNameReserved));
        Assert.That(AiCustomHeaderList.IsReservedName($" {name} "), Is.True);
    }

    /// <summary>
    /// Verifies that a name may occur once only, ignoring case.
    /// </summary>
    [Test]
    public void FindProblems_WithDuplicateName_FindsProblem()
    {
        List<AiCustomHeader> headers =
        [
            new AiCustomHeader { Name = kIssuerHeader, Value = kIssuerValue },
            new AiCustomHeader { Name = kIssuerHeader.ToUpperInvariant(), Value = kIssuerValue }
        ];

        Assert.That(AiCustomHeaderList.FindProblems(headers), Is.EqualTo(kNameDuplicate));
    }

    /// <summary>
    /// Verifies that a value that is blank, too long or contains characters other than visible ASCII, spaces and tabs is
    /// refused, so that it cannot end the header line.
    /// </summary>
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("keycloak\r\nX-Injected: 1")]
    [TestCase("keycloak\nX-Injected: 1")]
    [TestCase("key\u0000cloak")]
    [TestCase("key\u007fcloak")]
    [TestCase("schlüssel")]
    public void FindProblems_WithInvalidValue_FindsProblem(string value)
    {
        List<AiCustomHeader> headers = [new AiCustomHeader { Name = kIssuerHeader, Value = value }];

        Assert.That(AiCustomHeaderList.FindProblems(headers), Is.EqualTo(kValueInvalid));
    }

    /// <summary>
    /// Verifies that a value longer than allowed is refused.
    /// </summary>
    [Test]
    public void IsValidValue_WithTooLongValue_IsFalse()
    {
        Assert.That(AiCustomHeaderList.IsValidValue(new string(kValueChar, AiCustomHeaderList.kMaxValueLength + 1)), Is.False);
        Assert.That(AiCustomHeaderList.IsValidValue(null), Is.False);
    }

    /// <summary>
    /// Verifies that each problem is reported once, however many headers have it.
    /// </summary>
    [Test]
    public void FindProblems_ReportsEachProblemOnce()
    {
        List<AiCustomHeader> headers = CreateHeaders(AiCustomHeaderList.kMaxHeaders);
        headers.Add(new AiCustomHeader { Name = "bad name", Value = "" });
        headers.Add(new AiCustomHeader { Name = "other bad name", Value = "\n" });
        headers.Add(new AiCustomHeader { Name = "Authorization", Value = kIssuerValue });
        headers.Add(new AiCustomHeader { Name = "authorization", Value = kIssuerValue });

        Assert.That(AiCustomHeaderList.FindProblems(headers), Is.EqualTo(kAllHeaderProblems));
    }

    /// <summary>
    /// Verifies that the helpers refuse a missing list.
    /// </summary>
    [Test]
    public void Helpers_WithoutHeaders_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AiCustomHeaderList.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => AiCustomHeaderList.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => AiCustomHeaderList.FindProblems(null!));
    }

    /// <summary>
    /// Creates the given number of valid headers with different names.
    /// </summary>
    private static List<AiCustomHeader> CreateHeaders(int count)
    {
        return [.. Enumerable.Range(0, count).Select(index => new AiCustomHeader { Name = $"x-header-{index}", Value = kIssuerValue })];
    }
}
