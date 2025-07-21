namespace FWO.Basics
{
    /// <summary>
    /// Global string constants used e.g. as database keys etc.
    /// </summary>
    public struct GlobalConst
    {
        public const string kFwoProdName = "fworch";
        public const string kFwoBaseDir = "/usr/local/" + kFwoProdName;
        public const string kMainKeyFile = kFwoBaseDir + "/etc/secrets/main_key";

        public const string kEnglish = "English";
        public const int kTenant0Id = 1;

        public const int kSidebarLeftWidth = 300;
        public const int kGlobLibraryWidth = kSidebarLeftWidth + 400;
        public const int kObjLibraryWidth = kSidebarLeftWidth + 300;
        public const int kSidebarRightWidth = 300;
        public const int kDaysToMilliseconds = 86400000;
        public const int kHoursToMilliseconds = 3600000;
        public const int kMinutesToMilliseconds = 60000;
        public const int kSecondsToMilliseconds = 1000;
        public const int kMaxPortNumber = 65535;

        public const string kHtml = "html";
        public const string kPdf = "pdf";
        public const string kJson = "json";
        public const string kCsv = "csv";

        public const string kAutodiscovery = "autodiscovery";
        public const string kDailyCheck = "dailycheck";
        public const string kUi = "ui";
        public const string kCertification = "Certification";
        public const string kImportAppData = "importAppData";
        public const string kAdjustAppServerNames = "adjustAppServerNames";
        public const string kImportAreaSubnetData = "importAreaSubnetData";
        public const string kVarianceAnalysis = "varianceAnalysis";
        public const string kManual = "manual";
        public const string kCSV_ = "CSV_";
        public const string kDoku_ = "Doku_";
        public const string k_user = "_user";
        public const string k_user2 = "-user";
        public const string kUpdatable = "updatable";
        public const string kNAT = "NAT";
        public const string k_demo = "_demo";

        public const char kAppIdSeparator = '-'; // hard-coded could be moved to settings
        public const string kModellerGroup = "ModellerGroup_";
        public const string kFullAppIdPlaceholder = "@@ExternalAppId@@";
        public const string kAppIdPlaceholder = "@@AppId@@";
        public const string kAppPrefixPlaceholder = "@@AppPrefix@@";
        public const string kLdapGroupPattern = kModellerGroup + kAppIdPlaceholder;
        public const string kImportChangeNotify = "importChangeNotify";
		public const string kExternalRequest = "externalRequest";
        public const string kComplianceCheck = "complianceCheck";
        public const string kLdapInternalPostfix = "dc=" + kFwoProdName + ",dc=internal";
        public const int kLdapInternalId = 1;
        public const string kDummyAppRole = "DummyAppRole";
        public const string kUndefinedText = "(undefined text)";

        public const string kStyleHighlightedRed = "color: red;";
        public const string kStyleHighlightedGreen = "color: green;";
        public const string kStyleDeleted = "color: red; text-decoration: line-through red;";
        public const string kStyleAdded = "color: green; text-decoration: bold;";

        public const string ChromeBinPathLinux = "/usr/local/fworch/bin";
        public const string TestPDFFilePath = "pdffile.pdf";
        public const string TestPDFHtmlTemplate = "<html><body><h1>test</h1><h2>test mit puppteer</h2></body></html>";

        public const int MaxUploadFileSize = 5 * 1024 * 1024; // 5 MB
    }

    public struct PageName
    {
        public const string ReportGeneration = "report/generation";
        public const string Certification = "certification";
    }

    public struct ObjectType
    {
        public const string Group = "group";
        public const string Host = "host";
        public const string Network = "network";
        public const string IPRange = "ip_range";
        public const string AccessRole = "access-role";
    }

    public struct ServiceType
    {
        public const string Group = "group";
        public const string SimpleService = "simple";
        public const string Rpc = "rpc";
    }

    public struct MarkerLocation
    {
        public const string Rulename = "rulename";
        public const string Comment = "comment";
        public const string Customfields = "customfields";
    }

    public struct QueryVar
    {
        public const string Limit = "limit";
        public const string Offset = "offset";
        public const string Time = "time";
        public const string ImportIdStart = "import_id_start";
        public const string ImportIdEnd = "import_id_end";
        public const string ImportIdOld = "import_id_old";
        public const string ImportIdNew = "import_id_new";
        public const string MgmIds = "mgmIds";
        public const string MgmId = "mgmId";
        public const string ManagementId = "management_id";
        public const string RuleIds = "ruleIds";
        public const string RuleId = "rule_id";
    }

    public struct Placeholder
    {
        public const string ExternalAppId = "@@ExternalAppId@@";
        public const string AppId = "@@AppId@@";
        public const string AppPrefix = "@@AppPrefix@@";

        public const string APPNAME = "@@APPNAME@@";
        public const string APPID = "@@APPID@@";

        public const string ACTION = "@@ACTION@@";
        public const string CHANGEACTION = "@@CHANGEACTION@@";
        public const string COMMENT = "@@COMMENT@@";
        public const string DESTINATIONS = "@@DESTINATIONS@@";
        public const string GROUPNAME = "@@GROUPNAME@@";
        public const string IP = "@@IP@@";
        public const string MANAGEMENT_ID = "@@MANAGEMENT_ID@@";
        public const string MANAGEMENT_NAME = "@@MANAGEMENT_NAME@@";
        public const string MEMBERS = "@@MEMBERS@@";
        public const string OBJECT_DETAILS = "@@OBJECT_DETAILS@@";
        public const string OBJECTNAME = "@@OBJECTNAME@@";
        public const string OBJECT_TYPE = "@@OBJECT_TYPE@@";
        public const string OBJUPDSTATUS = "@@OBJUPDSTATUS@@";
        public const string ONBEHALF = "@@ONBEHALF@@";
        public const string ORDERNAME = "@@ORDERNAME@@";
        public const string PORT = "@@PORT@@";
        public const string PRIORITY = "@@PRIORITY@@";
        public const string PROTOCOLNAME = "@@PROTOCOLNAME@@";
        public const string PROTOCOLID = "@@PROTOCOLID@@";
        public const string REASON = "@@REASON@@";
        public const string SERVICENAME = "@@SERVICENAME@@";
        public const string SERVICES = "@@SERVICES@@";
        public const string SOURCES = "@@SOURCES@@";
        public const string STATUS = "@@STATUS@@";
        public const string TASKCOMMENT = "@@TASKCOMMENT@@";
        public const string TASKS = "@@TASKS@@";
        public const string TICKET_SUBJECT = "@@TICKET_SUBJECT@@";
        public const string TYPE = "@@TYPE@@";
    }
}
