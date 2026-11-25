using FWO.Ai.Client.ReasonChecks;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests reading the verdict from the answer of a reason check: verdict lines with and without markup, the verdict
/// words, NOT_GENUINE versus GENUINE, partial answers while they are streamed, and answers without verdict.
/// </summary>
[TestFixture]
[Parallelizable]
internal class ReasonVerdictParserTest
{
    /// <summary>
    /// Verifies that the verdict line of the answer format is read.
    /// </summary>
    [TestCase("Verdict: GENUINE\nCriteria:\nPurpose: met", ReasonVerdict.Genuine)]
    [TestCase("Verdict: INSUFFICIENT\r\nKriterien:", ReasonVerdict.Insufficient)]
    [TestCase("Verdict: NOT_GENUINE\nKriterien:", ReasonVerdict.NotGenuine)]
    public void Parse_WithVerdictLine_ReturnsVerdict(string answer, ReasonVerdict expectedVerdict)
    {
        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(expectedVerdict));
    }

    /// <summary>
    /// Verifies that verdict lines decorated with markup, numbering or other case are read.
    /// </summary>
    [TestCase("1. **Verdict:** NOT_GENUINE", ReasonVerdict.NotGenuine)]
    [TestCase("## Verdict: INSUFFICIENT", ReasonVerdict.Insufficient)]
    [TestCase("> **verdict** - `genuine`", ReasonVerdict.Genuine)]
    [TestCase("VERDICT:NOT GENUINE", ReasonVerdict.NotGenuine)]
    [TestCase("  - Verdict = Not-Genuine.", ReasonVerdict.NotGenuine)]
    public void Parse_WithDecoratedVerdictLine_ReturnsVerdict(string answer, ReasonVerdict expectedVerdict)
    {
        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(expectedVerdict));
    }

    /// <summary>
    /// Verifies that the verdict line wins over verdict words before it, e.g. in an introduction.
    /// </summary>
    [Test]
    public void Parse_WithVerdictWordBeforeVerdictLine_ReturnsVerdictOfLine()
    {
        string answer = "I check whether the reason is GENUINE.\nVerdict: INSUFFICIENT";

        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(ReasonVerdict.Insufficient));
    }

    /// <summary>
    /// Verifies that without a verdict line the first standalone verdict word is read, NOT_GENUINE never being read as
    /// GENUINE.
    /// </summary>
    [TestCase("Urteil: NOT_GENUINE, da kein Zweck erkennbar ist.", ReasonVerdict.NotGenuine)]
    [TestCase("Bewertung NOT GENUINE", ReasonVerdict.NotGenuine)]
    [TestCase("The result is INSUFFICIENT; it is not GENUINE.", ReasonVerdict.Insufficient)]
    [TestCase("Ergebnis:\nGENUINE", ReasonVerdict.Genuine)]
    [TestCase("Verdict (overall): GENUINE", ReasonVerdict.Genuine)]
    public void Parse_WithoutVerdictLine_ReturnsFirstVerdictWord(string answer, ReasonVerdict expectedVerdict)
    {
        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(expectedVerdict));
    }

    /// <summary>
    /// Verifies that verdict words inside other words or in lower case prose are not read as verdict.
    /// </summary>
    [TestCase("The reason is genuine and sufficient.")]
    [TestCase("GENUINELY_INSUFFICIENTX")]
    [TestCase("XGENUINE")]
    [TestCase("Criteria:\nPurpose: met")]
    public void Parse_WithoutVerdict_ReturnsUnknown(string answer)
    {
        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(ReasonVerdict.Unknown));
    }

    /// <summary>
    /// Verifies that partial answers while they are streamed give no verdict until the verdict word is complete.
    /// </summary>
    [TestCase("", ReasonVerdict.Unknown)]
    [TestCase("Verd", ReasonVerdict.Unknown)]
    [TestCase("Verdict: ", ReasonVerdict.Unknown)]
    [TestCase("Verdict: NOT", ReasonVerdict.Unknown)]
    [TestCase("Verdict: NOT_GEN", ReasonVerdict.Unknown)]
    [TestCase("Verdict: INSUFF", ReasonVerdict.Unknown)]
    [TestCase("Verdict: NOT_GENUINE", ReasonVerdict.NotGenuine)]
    public void Parse_WithPartialAnswer_ReturnsVerdictOnceComplete(string answer, ReasonVerdict expectedVerdict)
    {
        Assert.That(ReasonVerdictParser.Parse(answer), Is.EqualTo(expectedVerdict));
    }

    /// <summary>
    /// Verifies that no answer gives no verdict.
    /// </summary>
    [Test]
    public void Parse_WithNull_ReturnsUnknown()
    {
        Assert.That(ReasonVerdictParser.Parse(null), Is.EqualTo(ReasonVerdict.Unknown));
    }
}
