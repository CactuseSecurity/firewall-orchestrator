using System.Security.Claims;
using FWO.Basics;

namespace FWO.Api.Client
{
    /// <summary>
    /// Provides named API role scopes for UI and middleware workflows.
    /// </summary>
    public static class ApiConnectionRoleScopeExtensions
    {
        private static readonly List<string> AdminOrAuditorRoles = [Roles.Admin, Roles.Auditor];
        private static readonly List<string> ModellingRoles = [Roles.Modeller, Roles.Admin, Roles.Auditor];
        private static readonly List<string> MonitoringRoles = [Roles.Admin, Roles.FwAdmin, Roles.Auditor];
        private static readonly List<string> ReportingRoles =
            [Roles.ReporterViewAll, Roles.Reporter, Roles.Modeller, Roles.Recertifier, Roles.Admin, Roles.Auditor, Roles.FwAdmin];
        private static readonly List<string> RecertificationRoles = [Roles.Recertifier, Roles.Admin, Roles.Auditor];
        private static readonly List<string> WorkflowRoles =
        [
            Roles.Requester,
            Roles.Approver,
            Roles.Planner,
            Roles.Implementer,
            Roles.Reviewer,
            Roles.Admin,
            Roles.FwAdmin,
            Roles.Auditor
        ];

        /// <summary>
        /// Runs an API operation with the best available admin or auditor role.
        /// </summary>
        public static Task RunWithAdminOrAuditorRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, AdminOrAuditorRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available admin or auditor role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithAdminOrAuditorRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, AdminOrAuditorRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available modelling read/write role.
        /// </summary>
        public static Task RunWithModellingRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, ModellingRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available modelling read/write role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithModellingRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, ModellingRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available monitoring role.
        /// </summary>
        public static Task RunWithMonitoringRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, MonitoringRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available monitoring role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithMonitoringRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, MonitoringRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available reporting role.
        /// </summary>
        public static Task RunWithReportingRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, ReportingRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available reporting role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithReportingRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, ReportingRoles, action);
        }

        /// <summary>
        /// Sets the best available role for generating the given report type.
        /// </summary>
        public static void SetBestRoleForReport(this ApiConnection apiConnection, ClaimsPrincipal user,
            ReportType reportType)
        {
            apiConnection.SetBestRole(user, GetReportRoles(reportType));
        }

        private static List<string> GetReportRoles(ReportType reportType)
        {
            if (reportType == ReportType.Owners || reportType.IsComplianceReport())
            {
                return [Roles.Admin, Roles.FwAdmin, Roles.Auditor];
            }
            if (reportType.IsModellingReport())
            {
                return [Roles.Admin, Roles.Modeller, Roles.Recertifier, Roles.Auditor];
            }
            if (reportType.IsWorkflowReport())
            {
                return [Roles.Admin, Roles.FwAdmin, Roles.Auditor, Roles.Requester,
                    Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer];
            }
            if (reportType.IsDeviceRelatedReport())
            {
                return [Roles.Admin, Roles.FwAdmin, Roles.ReporterViewAll, Roles.Reporter,
                    Roles.Recertifier, Roles.Auditor];
            }
            return ReportingRoles;
        }

        /// <summary>
        /// Runs an API operation with the best available recertification role.
        /// </summary>
        public static Task RunWithRecertificationRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, RecertificationRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available recertification role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithRecertificationRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, RecertificationRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available workflow role.
        /// </summary>
        public static Task RunWithWorkflowRole(this ApiConnection apiConnection, ClaimsPrincipal user,
            Func<Task> action)
        {
            return apiConnection.RunWithBestRole(user, WorkflowRoles, action);
        }

        /// <summary>
        /// Runs an API operation with the best available workflow role and returns a result.
        /// </summary>
        public static Task<TResult> RunWithWorkflowRole<TResult>(this ApiConnection apiConnection,
            ClaimsPrincipal user, Func<Task<TResult>> action)
        {
            return apiConnection.RunWithBestRole(user, WorkflowRoles, action);
        }
    }
}
