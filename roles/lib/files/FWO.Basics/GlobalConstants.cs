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

		public const string kLdapInternalPostfix = "dc=" + kFwoProdName + ",dc=internal";

		public const string kDummyAppRole = "DummyAppRole";
		public const string kUndefinedText = "(undefined text)";
		
		public const string kStyleHighlighted = "color:red;";
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
	}
	
	public struct ServiceType
    {
        public const string Group = "group";
        public const string SimpleService = "simple";
        public const string Rpc = "rpc";
    }

}
