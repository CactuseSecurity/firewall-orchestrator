using FWO.Basics.Enums;

namespace FWO.Data.Extensions
{
    public static partial class ComplianceExtensions
    {
        public static ComplianceViolationType ToViolationType(this string violationType)
        {
            return violationType switch
            {
                nameof(CriterionType.Assessability) => ComplianceViolationType.NotAssessable ,
                nameof(CriterionType.Matrix) => ComplianceViolationType.MatrixViolation,
                nameof(CriterionType.ForbiddenService) => ComplianceViolationType.ServiceViolation,
                _ => throw new NotImplementedException()
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