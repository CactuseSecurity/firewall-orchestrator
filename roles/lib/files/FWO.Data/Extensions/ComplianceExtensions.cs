using FWO.Basics.Enums;

namespace FWO.Data.Extensions
{
    public static partial class ComplianceExtensions
    {
        public static string ToAssessabilityIssueString(this AssessabilityIssue assessabilityIssue)
        {
            return assessabilityIssue switch
            {
                AssessabilityIssue.IPNull => "assess_ip_null",
                AssessabilityIssue.AllIPs => "assess_all_ips",
                AssessabilityIssue.HostAddress => "assess_host_address",
                AssessabilityIssue.Broadcast => "assess_broadcast",
                AssessabilityIssue.NoMatchingZone => "assess_no_matching_zone",
                _ => throw new NotImplementedException()
            };
        }
    }
}
