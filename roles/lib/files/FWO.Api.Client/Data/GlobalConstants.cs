namespace FWO.Api.Data
{
    public struct GlobalConst
    {
        public const string kEnglish = "English";

        public const int kSidebarLeftWidth = 300;
        public const int kSidebarRightWidth = 300;
        public const int kHoursToMilliseconds = 3600000;

        public const string kHtml = "html";
        public const string kPdf = "pdf";
        public const string kJson = "json";
        public const string kCsv = "csv";

        public const string kAutodiscovery = "autodiscovery";
        public const string kDailyCheck = "dailycheck";
        public const string kUi = "ui";
        public const string kCertification = "Certification";
        public const string kImportAppData = "importAppData";
        public const string kImportAreaSubnetData = "importAreaSubnetData";
        public const string kManual = "manual";
        public const string kModellerGroup = "ModellerGroup_";
        public const string kImportChangeNotify = "importChangeNotify";
    }
    
    public struct Roles
    {
        public const string Anonymous = "anonymous";
        public const string Admin = "admin";
        public const string Auditor = "auditor";
        public const string MiddlewareServer = "middleware-server";
        public const string Importer = "importer";
        public const string FwAdmin = "fw-admin";
        public const string Recertifier = "recertifier";
        public const string Modeller = "modeller";
        public const string Reporter = "reporter";
        public const string ReporterViewAll = "reporter-viewall";
        public const string Requester = "requester";
        public const string Approver = "approver";
        public const string Planner = "planner";
        public const string Implementer = "implementer";
        public const string Reviewer = "reviewer";
    }
    
    public struct Icons
    {
        public const string Add = "oi oi-plus";
        public const string Edit = "oi oi-wrench";
        public const string Delete = "oi oi-trash";
        public const string Search = "oi oi-magnifying-glass";
        public const string Use = "oi oi-arrow-thick-right";
        public const string Unuse = "oi oi-arrow-thick-left";

        public const string ModObject = "oi oi-tag";
        public const string Service = "oi oi-wrench";
        public const string ServiceGroup = "oi oi-list-rich";
        public const string AppServer = "oi oi-laptop";
        public const string AppRole = "oi oi-list-rich";
        public const string NwGroup = "oi oi-folder";
    }
}
