namespace FWO.Data
{
    public enum ComplianceViolationType
    {
        None, // rule is compliant
        NotAssessable, // compliance cant be evaluated (e.g. zone internet)
        MatrixViolation,
        ServiceViolation,
        MultipleViolations

    }
}