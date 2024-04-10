namespace FWO.Api.Data
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

        // Technical
        public const string MiddlewareServer = "middleware-server";
        public const string Importer = "importer";
        public const string DbBackup = "dbbackup";
    }
}
