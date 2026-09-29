using FWO.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests that the recorded violation type survives the persistence round trip.
/// </summary>
[TestFixture]
internal class ComplianceViolationPersistenceTest
{
    private const string CidrCriterion = """
        "criterion": { "id": 11, "name": "MinimumCIDRLength", "criterion_type": "MinimumCIDRLength" }
        """;

    /// <summary>
    /// Verifies the type recorded by the compliance check is handed to the insert.
    /// </summary>
    [Test]
    public void CreateBase_WithRecordedType_PersistsTheType()
    {
        ComplianceViolation violation = new()
        {
            RuleId = 1,
            CriterionId = 11,
            Type = ComplianceViolationType.NotAssessable
        };

        ComplianceViolationBase violationBase = ComplianceViolationBase.CreateBase(violation, false);

        Assert.That(violationBase.ViolationType, Is.EqualTo("NotAssessable"));
    }

    /// <summary>
    /// Verifies no type is persisted for a violation that was recorded without one.
    /// </summary>
    [Test]
    public void CreateBase_WithoutRecordedType_PersistsNothing()
    {
        ComplianceViolation violation = new()
        {
            RuleId = 1,
            CriterionId = 11
        };

        ComplianceViolationBase violationBase = ComplianceViolationBase.CreateBase(violation, false);

        Assert.That(violationBase.ViolationType, Is.Null);
    }

    /// <summary>
    /// Verifies a persisted type wins over the type derived from the criterion, which is what makes a
    /// criterion that could not be evaluated readable as not assessable in the compliance report.
    /// </summary>
    [Test]
    public void ReadJson_WithPersistedType_PrefersItOverTheCriterion()
    {
        string json = $$"""
            { "id": 7, "rule_id": 1, "criterion_id": 11, "violation_type": "NotAssessable", {{CidrCriterion}} }
            """;

        ComplianceViolation? violation = JsonConvert.DeserializeObject<ComplianceViolation>(json);

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.Type, Is.EqualTo(ComplianceViolationType.NotAssessable));
    }

    /// <summary>
    /// Verifies rows written before the type was persisted keep deriving it from the criterion.
    /// </summary>
    [TestCase("")]
    [TestCase(", \"violation_type\": null")]
    [TestCase(", \"violation_type\": \"NoSuchType\"")]
    public void ReadJson_WithoutUsablePersistedType_DerivesItFromTheCriterion(string persistedType)
    {
        string json = $$"""
            { "id": 7, "rule_id": 1, "criterion_id": 11{{persistedType}}, {{CidrCriterion}} }
            """;

        ComplianceViolation? violation = JsonConvert.DeserializeObject<ComplianceViolation>(json);

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.Type, Is.EqualTo(ComplianceViolationType.MinimumCIDRLengthViolation));
    }
}
