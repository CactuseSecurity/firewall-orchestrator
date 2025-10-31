using System.Linq.Expressions;
using FWO.Data;

namespace FWO.Compliance
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

        public static string ToString(this AssessabilityIssue assessabilityIssue)
        {
            return assessabilityIssue switch
            {
                AssessabilityIssue.IPNull => "assess_ip_null",
                AssessabilityIssue.AllIPs => "assess_all_ips",
                AssessabilityIssue.HostAddress => "assess_host_address",
                AssessabilityIssue.Broadcast => "assess_broadcast",
                _ => $"<Assessability Issue: {assessabilityIssue}>"
            };
        }

        public static AssessabilityIssue? ToAssessibilityIssue(this Expression<Func<bool>> expr)
        {
            // Evaluate the expression result
            bool result = expr.Compile().Invoke();

            // If the condition is false, return null immediately
            if (!result)
                return null;

            // Convert the expression body to string for simple pattern detection
            var body = expr.Body.ToString();

            // Detect specific patterns in the expression text
            // Note: string matching is case-sensitive; adjust if necessary
            if (body.Contains("IP == null") && body.Contains("IpEnd == null"))
                return AssessabilityIssue.IPNull;

            if ((body.Contains("IP == \"0.0.0.0/32\"") && body.Contains("IpEnd == \"255.255.255.255/32\""))
                || (body.Contains("IP == \"::/128\"") && body.Contains("IpEnd == \"ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128\"")))
                return AssessabilityIssue.AllIPs;

            if (body.Contains("IP == \"255.255.255.255/32\"") && body.Contains("IpEnd == \"255.255.255.255/32\""))
                return AssessabilityIssue.Broadcast;

            if (body.Contains("IP == \"0.0.0.0/32\"") && body.Contains("IpEnd == \"0.0.0.0/32\""))
                return AssessabilityIssue.HostAddress;

            // If none of the patterns match, return null
            return null;
        }
        
    }
}