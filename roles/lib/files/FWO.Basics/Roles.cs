using System.Security.Claims;

namespace FWO.Basics
{
    public struct Roles
    {
        // General
        public const string Anonymous = "anonymous";
        public const string Admin = "admin";
        public const string Auditor = "auditor";
        public const string FwAdmin = "fw-admin";

        // Rules
        public const string Reporter = "reporter";
        public const string ReporterViewAll = "reporter-viewall";
        public const string Recertifier = "recertifier";
        public const string Modeller = "modeller";

        // Workflow
        public const string Requester = "requester";
        public const string Approver = "approver";
        public const string Planner = "planner";
        public const string Implementer = "implementer";
        public const string Reviewer = "reviewer";
        public const string WorkflowRolesList = $"{Requester}, {Approver}, {Planner}, {Implementer}, {Reviewer}";

        // Technical
        public const string MiddlewareServer = "middleware-server";
        public const string Importer = "importer";
        public const string DbBackup = "dbbackup";
    }

    public static class RoleGroups
    {
        public static bool IsTechnicalOrAnonymous(string role)
        {
            return role == Roles.MiddlewareServer || role == Roles.Importer || role == Roles.DbBackup || role == Roles.Anonymous;
        }

        public static ReportVisibility GetReportVisibility(ClaimsPrincipal user)
        {
            return new ReportVisibility(
                RuleRelated: user.IsInRole(Roles.Reporter)
                    || user.IsInRole(Roles.ReporterViewAll)
                    || user.IsInRole(Roles.FwAdmin)
                    || user.IsInRole(Roles.Admin)
                    || user.IsInRole(Roles.Auditor)
                    || user.IsInRole(Roles.Recertifier),
                ModellingRelated: user.IsInRole(Roles.Modeller)
                    || user.IsInRole(Roles.Admin)
                    || user.IsInRole(Roles.Auditor)
                    || user.IsInRole(Roles.Recertifier),
                ComplianceRelated: user.IsInRole(Roles.Admin)
                    || user.IsInRole(Roles.FwAdmin)
                    || user.IsInRole(Roles.Auditor),
                OwnerRelated: user.IsInRole(Roles.Admin)
                    || user.IsInRole(Roles.FwAdmin)
                    || user.IsInRole(Roles.Auditor),
                WorkflowRelated: user.IsInRole(Roles.Admin)
                    || user.IsInRole(Roles.FwAdmin)
                    || user.IsInRole(Roles.Auditor)
                    || user.IsInRole(Roles.Requester)
                    || user.IsInRole(Roles.Approver)
                    || user.IsInRole(Roles.Planner)
                    || user.IsInRole(Roles.Implementer)
                    || user.IsInRole(Roles.Reviewer));
        }
    }

    public readonly record struct ReportVisibility(bool RuleRelated, bool ModellingRelated, bool ComplianceRelated, bool OwnerRelated, bool WorkflowRelated);
}
