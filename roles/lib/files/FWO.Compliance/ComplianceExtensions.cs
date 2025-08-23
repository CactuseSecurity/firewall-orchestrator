using FWO.Data;

namespace FWO.Compliance
{
    public static class ComplianceExtensions
    {
        public static string ToString(this ComplianceViolationType violationType)
        {
            return violationType switch
            {
                ComplianceViolationType.None => "Compliant",
                ComplianceViolationType.NotEvaluable => "Not evaluable",
                ComplianceViolationType.MatrixViolation => "Matrix violation",
                ComplianceViolationType.ServiceViolation => "Service violation",
                ComplianceViolationType.MultipleViolations => "Multiple violations",
                _ => $"<Unknown Status: {violationType}>"
            };
        }
        
    }
}