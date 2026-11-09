using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointSectionNameTemplateTest
{
    [TestCase("fixed section", false, false)]
    [TestCase("@@SOURCE_ZONE@@", true, false)]
    [TestCase("@@SOURCE_ZONE@@@@DESTINATION_ZONE@@", true, true)]
    [TestCase("@@SOURCE_ZONE@@/@@SOURCE_ZONE@@/@@SOURCE_ZONE@@", true, false)]
    public void Parse_AcceptsFixedAdjacentAndRepeatedParts(string text, bool requiresSource, bool requiresDestination)
    {
        CheckPointSectionNameTemplate template = Parse(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(template.RequiredVariables.Contains(CheckPointSectionNameVariable.SourceZone), Is.EqualTo(requiresSource));
            Assert.That(template.RequiredVariables.Contains(CheckPointSectionNameVariable.DestinationZone), Is.EqualTo(requiresDestination));
            Assert.That(template.RequiredVariables.Count, Is.EqualTo((requiresSource ? 1 : 0) + (requiresDestination ? 1 : 0)));
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("@@@@")]
    [TestCase("@@UNKNOWN@@")]
    [TestCase("@@source_zone@@")]
    [TestCase("before@@SOURCE_ZONE@after")]
    [TestCase("before@@after")]
    public void Parse_RejectsEmptyUnknownOrMalformedSyntax(string? text)
    {
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> result =
            CheckPointSectionNameTemplate.Parse(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TryGetValue(out _), Is.False);
            Assert.That(result.TryGetFailure(out CheckPointSectionNameTemplateFailureCategory failure), Is.True);
            Assert.That(failure, Is.EqualTo(CheckPointSectionNameTemplateFailureCategory.InvalidTemplate));
        }
    }

    [Test]
    public void Render_DefaultTemplate_TrimsValuesAndNormalizesWholeName()
    {
        CheckPointSectionNameTemplate template = Parse("[@@SOURCE_ZONE@@] -> [@@DESTINATION_ZONE@@]");

        string rendered = Render(template, new Dictionary<CheckPointSectionNameVariable, string>()
        {
            [CheckPointSectionNameVariable.SourceZone] = " source ",
            [CheckPointSectionNameVariable.DestinationZone] = " destination "
        });

        Assert.That(rendered, Is.EqualTo("[SOURCE] -> [DESTINATION]"));
    }

    [Test]
    public void Render_ReplacesRepeatedAndReorderedVariables()
    {
        CheckPointSectionNameTemplate template = Parse(
            "@@DESTINATION_ZONE@@:@@SOURCE_ZONE@@:@@DESTINATION_ZONE@@");

        string rendered = Render(template, new Dictionary<CheckPointSectionNameVariable, string>()
        {
            [CheckPointSectionNameVariable.SourceZone] = "src",
            [CheckPointSectionNameVariable.DestinationZone] = "dst"
        });

        Assert.That(rendered, Is.EqualTo("DST:SRC:DST"));
    }

    [Test]
    public void Render_DoesNotInterpretPlaceholderTextInsideAValue()
    {
        CheckPointSectionNameTemplate template = Parse("@@SOURCE_ZONE@@");

        string rendered = Render(template, new Dictionary<CheckPointSectionNameVariable, string>()
        {
            [CheckPointSectionNameVariable.SourceZone] = "@@DESTINATION_ZONE@@"
        });

        Assert.That(rendered, Is.EqualTo("@@DESTINATION_ZONE@@"));
    }

    [Test]
    public void Render_PreservesInteriorWhitespaceWhileNormalizingEdgesAndCase()
    {
        CheckPointSectionNameTemplate template = Parse("  Prefix  @@SOURCE_ZONE@@  Suffix  ");

        string rendered = Render(template, new Dictionary<CheckPointSectionNameVariable, string>()
        {
            [CheckPointSectionNameVariable.SourceZone] = " source "
        });

        Assert.That(rendered, Is.EqualTo("PREFIX  SOURCE  SUFFIX"));
    }

    [Test]
    public void Render_RejectsMissingRequiredValue()
    {
        CheckPointSectionNameTemplate template = Parse("@@SOURCE_ZONE@@");

        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> result =
            template.Render(new Dictionary<CheckPointSectionNameVariable, string>());

        AssertFailure(result, CheckPointSectionNameTemplateFailureCategory.MissingValue);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Render_RejectsEmptyRequiredValue(string value)
    {
        CheckPointSectionNameTemplate template = Parse("@@SOURCE_ZONE@@");

        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> result = template.Render(new Dictionary<
            CheckPointSectionNameVariable, string>
        { [CheckPointSectionNameVariable.SourceZone] = value });

        AssertFailure(result, CheckPointSectionNameTemplateFailureCategory.InvalidValue);
    }

    private static CheckPointSectionNameTemplate Parse(string text)
    {
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> result =
            CheckPointSectionNameTemplate.Parse(text);
        Assert.That(result.TryGetValue(out CheckPointSectionNameTemplate? template), Is.True);
        return template!;
    }

    private static string Render(
        CheckPointSectionNameTemplate template,
        IReadOnlyDictionary<CheckPointSectionNameVariable, string> values)
    {
        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> result = template.Render(values);
        Assert.That(result.TryGetValue(out string? rendered), Is.True);
        return rendered!;
    }

    private static void AssertFailure(
        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> result,
        CheckPointSectionNameTemplateFailureCategory expected)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TryGetValue(out _), Is.False);
            Assert.That(result.TryGetFailure(out CheckPointSectionNameTemplateFailureCategory failure), Is.True);
            Assert.That(failure, Is.EqualTo(expected));
        }
    }
}
