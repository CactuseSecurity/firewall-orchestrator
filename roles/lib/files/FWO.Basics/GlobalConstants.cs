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
        public const int kHoursToMilliseconds = 3600000;
        public const int kMinutesToMilliseconds = 60000;
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

        public const char kAppIdSeparator = '-'; // hard-coded could be moved to settings
        public const string kModellerGroup = "ModellerGroup_";
        public const string kFullAppIdPlaceholder = "@@ExternalAppId@@";
        public const string kAppIdPlaceholder = "@@AppId@@";
        public const string kAppPrefixPlaceholder = "@@AppPrefix@@";
        public const string kLdapGroupPattern = kModellerGroup + kAppIdPlaceholder;
        public const string kImportChangeNotify = "importChangeNotify";
		public const string kExternalRequest = "externalRequest";
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
}
