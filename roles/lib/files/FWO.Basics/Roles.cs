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

    }

    public readonly record struct ReportVisibility(bool RuleRelated, bool ModellingRelated, bool ComplianceRelated, bool OwnerRelated, bool WorkflowRelated);

    /// <summary>
    /// Single source of truth for which roles fall into each report-category bucket used by
    /// <see cref="ReportVisibility"/>. Shared by the aggregate (OR-across-held-roles) visibility
    /// check and by the per-role visibility check used to resolve "Inherited" report-type overrides.
    /// </summary>
    public static class ReportVisibilityRoleSets
    {
        public static readonly string[] RuleRelated = [Roles.Reporter, Roles.ReporterViewAll, Roles.FwAdmin, Roles.Admin, Roles.Auditor, Roles.Recertifier];
        public static readonly string[] ModellingRelated = [Roles.Modeller, Roles.Admin, Roles.Auditor, Roles.Recertifier];
        public static readonly string[] ComplianceRelated = [Roles.Admin, Roles.FwAdmin, Roles.Auditor];
        public static readonly string[] OwnerRelated = [Roles.Admin, Roles.FwAdmin, Roles.Auditor];
        public static readonly string[] WorkflowRelated = [Roles.Admin, Roles.FwAdmin, Roles.Auditor, Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer];

        /// <summary>
        /// Computes the report-category visibility for a single role (as opposed to the OR of every role a user holds).
        /// </summary>
        public static ReportVisibility ForRole(string role)
        {
            return new ReportVisibility(
                RuleRelated: RuleRelated.Contains(role, StringComparer.OrdinalIgnoreCase),
                ModellingRelated: ModellingRelated.Contains(role, StringComparer.OrdinalIgnoreCase),
                ComplianceRelated: ComplianceRelated.Contains(role, StringComparer.OrdinalIgnoreCase),
                OwnerRelated: OwnerRelated.Contains(role, StringComparer.OrdinalIgnoreCase),
                WorkflowRelated: WorkflowRelated.Contains(role, StringComparer.OrdinalIgnoreCase));
        }
    }
}
