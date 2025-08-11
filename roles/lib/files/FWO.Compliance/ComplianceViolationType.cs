namespace FWO.Compliance
{
    public enum ComplianceViolationType
    {
        None, // rule is compliant
        NotEvaluable, // compliance cant be evaluated (e.g. zone internet)
        MatrixViolation,
        ServiceViolation,
        MultipleViolations

    }
}