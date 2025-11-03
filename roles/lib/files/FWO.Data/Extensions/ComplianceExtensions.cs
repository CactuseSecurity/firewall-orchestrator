using FWO.Basics.Enums;

namespace FWO.Data.Extensions
{
    public static partial class ComplianceExtensions
    {


        
        public static string ToString(this ComplianceViolationType violationType)
        {
            return violationType switch
            {
                ComplianceViolationType.None => "Compliant",
                ComplianceViolationType.NotAssessable => "Not assessable",
                ComplianceViolationType.MatrixViolation => "Matrix violation",
                ComplianceViolationType.ServiceViolation => "Service violation",
                ComplianceViolationType.MultipleViolations => "Multiple violations",
                _ => $"<Unknown Status: {violationType}>"
            };
        }

        public static string ToAssessabilityIssueString(this AssessabilityIssue assessabilityIssue)
        {
            return assessabilityIssue switch
            {
                AssessabilityIssue.IPNull => "assess_ip_null",
                AssessabilityIssue.AllIPs => "assess_all_ips",
                AssessabilityIssue.HostAddress => "assess_host_address",
                AssessabilityIssue.Broadcast => "assess_broadcast",
                _ => throw new NotImplementedException()
            };
        }

        public static AssessabilityIssue ToAssessabilityIssue(this string assessabilityIssueString)
        {
            return assessabilityIssueString switch
            {
                "assess_ip_null" => AssessabilityIssue.IPNull,
                "assess_all_ips" => AssessabilityIssue.AllIPs,
                "assess_host_address" => AssessabilityIssue.HostAddress,
                "assess_broadcast" => AssessabilityIssue.Broadcast,
                _ => throw new NotImplementedException()
            };
        }
        
    }
}