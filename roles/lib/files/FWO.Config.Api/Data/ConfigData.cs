using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Basics;
using FWO.Api.Data;
using FWO.Mail;

namespace FWO.Config.Api.Data
{
	[AttributeUsage(AttributeTargets.Property)]
	public class UserConfigDataAttribute : Attribute { }

	public class ConfigData : ICloneable
	{
		public readonly bool Editable;

		[JsonProperty("DefaultLanguage"), JsonPropertyName("DefaultLanguage")]
		public virtual string DefaultLanguage { get; set; } = GlobalConst.kEnglish;

		[JsonProperty("sessionTimeout"), JsonPropertyName("sessionTimeout")]
		public int SessionTimeout { get; set; } = 720;

		[JsonProperty("sessionTimeoutNoticePeriod"), JsonPropertyName("sessionTimeoutNoticePeriod")]
		public int SessionTimeoutNoticePeriod { get; set; } = 60;

		[JsonProperty("uiHostName"), JsonPropertyName("uiHostName")]
		public string UiHostName { get; set; } = "http://localhost:5000";

		[JsonProperty("welcomeMessage"), JsonPropertyName("welcomeMessage")]
		public string WelcomeMessage { get; set; } = "";

		//        [JsonProperty("maxMessages"), JsonPropertyName("maxMessages"), UserConfigData]
		//        public int MaxMessages { get; set; } = 3;

		[JsonProperty("elementsPerFetch"), JsonPropertyName("elementsPerFetch"), UserConfigData]
		public int ElementsPerFetch { get; set; } = 100;

		[JsonProperty("maxInitialFetchesRightSidebar"), JsonPropertyName("maxInitialFetchesRightSidebar")]
		public int MaxInitialFetchesRightSidebar { get; set; } = 10;

		[JsonProperty("autoFillRightSidebar"), JsonPropertyName("autoFillRightSidebar")]
		public bool AutoFillRightSidebar { get; set; } = false;

		[JsonProperty("unusedTolerance"), JsonPropertyName("unusedTolerance")]
		public int UnusedTolerance { get; set; } = 400;

		[JsonProperty("creationTolerance"), JsonPropertyName("creationTolerance")]
		public int CreationTolerance { get; set; } = 90;

		[JsonProperty("dataRetentionTime"), JsonPropertyName("dataRetentionTime")]
		public int DataRetentionTime { get; set; } = 731;

		[JsonProperty("importSleepTime"), JsonPropertyName("importSleepTime")]
		public int ImportSleepTime { get; set; } = 40;

		[JsonProperty("importCheckCertificates"), JsonPropertyName("importCheckCertificates")]
		public bool ImportCheckCertificates { get; set; } = false;

		[JsonProperty("importSuppressCertificateWarnings"), JsonPropertyName("importSuppressCertificateWarnings")]
		public bool ImportSuppressCertificateWarnings { get; set; } = true;

		[JsonProperty("autoDiscoverSleepTime"), JsonPropertyName("autoDiscoverSleepTime")]
		public int AutoDiscoverSleepTime { get; set; } = 24;

		[JsonProperty("autoDiscoverStartAt"), JsonPropertyName("autoDiscoverStartAt")]
		public DateTime AutoDiscoverStartAt { get; set; } = new DateTime();

		[JsonProperty("fwApiElementsPerFetch"), JsonPropertyName("fwApiElementsPerFetch")]
		public int FwApiElementsPerFetch { get; set; } = 150;

		[JsonProperty("impChangeNotifyRecipients"), JsonPropertyName("impChangeNotifyRecipients")]
		public string ImpChangeNotifyRecipients { get; set; } = "";

		[JsonProperty("impChangeNotifySubject"), JsonPropertyName("impChangeNotifySubject")]
		public string ImpChangeNotifySubject { get; set; } = "";

		[JsonProperty("impChangeNotifyBody"), JsonPropertyName("impChangeNotifyBody")]
		public string ImpChangeNotifyBody { get; set; } = "";

		[JsonProperty("impChangeNotifyActive"), JsonPropertyName("impChangeNotifyActive")]
		public bool ImpChangeNotifyActive { get; set; } = false;

		[JsonProperty("impChangeNotifyType"), JsonPropertyName("impChangeNotifyType")]
		public int ImpChangeNotifyType { get; set; }

		[JsonProperty("impChangeNotifySleepTime"), JsonPropertyName("impChangeNotifySleepTime")]
		public int ImpChangeNotifySleepTime { get; set; } = 60;

		[JsonProperty("impChangeNotifyStartAt"), JsonPropertyName("impChangeNotifyStartAt")]
		public DateTime ImpChangeNotifyStartAt { get; set; } = new DateTime();


		[JsonProperty("recertificationPeriod"), JsonPropertyName("recertificationPeriod")]
		public int RecertificationPeriod { get; set; } = 365;

		[JsonProperty("recertificationNoticePeriod"), JsonPropertyName("recertificationNoticePeriod")]
		public int RecertificationNoticePeriod { get; set; } = 30;

		[JsonProperty("recertificationDisplayPeriod"), JsonPropertyName("recertificationDisplayPeriod")]
		public int RecertificationDisplayPeriod { get; set; } = 30;

		[JsonProperty("ruleRemovalGracePeriod"), JsonPropertyName("ruleRemovalGracePeriod")]
		public int RuleRemovalGracePeriod { get; set; } = 60;

		[JsonProperty("commentRequired"), JsonPropertyName("commentRequired")]
		public bool CommentRequired { get; set; } = false;

		[JsonProperty("recAutocreateDeleteTicket"), JsonPropertyName("recAutocreateDeleteTicket")]
		public bool RecAutoCreateDeleteTicket { get; set; } = false;

		[JsonProperty("recDeleteRuleTicketTitle"), JsonPropertyName("recDeleteRuleTicketTitle")]
		public string RecDeleteRuleTicketTitle { get; set; } = "";

		[JsonProperty("recDeleteRuleTicketReason"), JsonPropertyName("recDeleteRuleTicketReason")]
		public string RecDeleteRuleTicketReason { get; set; } = "";

		[JsonProperty("recDeleteRuleReqTaskTitle"), JsonPropertyName("recDeleteRuleReqTaskTitle")]
		public string RecDeleteRuleReqTaskTitle { get; set; } = "";

		[JsonProperty("recDeleteRuleReqTaskReason"), JsonPropertyName("recDeleteRuleReqTaskReason")]
		public string RecDeleteRuleReqTaskReason { get; set; } = "";

		[JsonProperty("recDeleteRuleTicketPriority"), JsonPropertyName("recDeleteRuleTicketPriority")]
		public int RecDeleteRuleTicketPriority { get; set; } = 3;

		[JsonProperty("recDeleteRuleInitState"), JsonPropertyName("recDeleteRuleInitState")]
		public int RecDeleteRuleInitState { get; set; } = 0;

		[JsonProperty("recCheckActive"), JsonPropertyName("recCheckActive")]
		public bool RecCheckActive { get; set; } = false;

		[JsonProperty("recCheckParams"), JsonPropertyName("recCheckParams")]
		public string RecCheckParams { get; set; } = System.Text.Json.JsonSerializer.Serialize(new RecertCheckParams());

		[JsonProperty("recCheckEmailSubject"), JsonPropertyName("recCheckEmailSubject")]
		public string RecCheckEmailSubject { get; set; } = "";

		[JsonProperty("recCheckEmailUpcomingText"), JsonPropertyName("recCheckEmailUpcomingText")]
		public string RecCheckEmailUpcomingText { get; set; } = "";

		[JsonProperty("recCheckEmailOverdueText"), JsonPropertyName("recCheckEmailOverdueText")]
		public string RecCheckEmailOverdueText { get; set; } = "";

		[JsonProperty("recRefreshStartup"), JsonPropertyName("recRefreshStartup")]
		public bool RecRefreshStartup { get; set; } = false;

		[JsonProperty("recRefreshDaily"), JsonPropertyName("recRefreshDaily")]
		public bool RecRefreshDaily { get; set; } = false;

		[JsonProperty("pwMinLength"), JsonPropertyName("pwMinLength")]
		public int PwMinLength { get; set; } = 10;

		[JsonProperty("pwUpperCaseRequired"), JsonPropertyName("pwUpperCaseRequired")]
		public bool PwUpperCaseRequired { get; set; } = false;

		[JsonProperty("pwLowerCaseRequired"), JsonPropertyName("pwLowerCaseRequired")]
		public bool PwLowerCaseRequired { get; set; } = false;

		[JsonProperty("pwNumberRequired"), JsonPropertyName("pwNumberRequired")]
		public bool PwNumberRequired { get; set; } = false;

		[JsonProperty("pwSpecialCharactersRequired"), JsonPropertyName("pwSpecialCharactersRequired")]
		public bool PwSpecialCharactersRequired { get; set; } = false;

		[JsonProperty("emailServerAddress"), JsonPropertyName("emailServerAddress")]
		public string EmailServerAddress { get; set; } = "";

		[JsonProperty("emailPort"), JsonPropertyName("emailPort")]
		public int EmailPort { get; set; }

		[JsonProperty("emailTls"), JsonPropertyName("emailTls")]
		public EmailEncryptionMethod EmailTls { get; set; } = EmailEncryptionMethod.None;

		[JsonProperty("emailUser"), JsonPropertyName("emailUser")]
		public string EmailUser { get; set; } = "";

		[JsonProperty("emailPassword"), JsonPropertyName("emailPassword")]
		public string EmailPassword { get; set; } = "";

		[JsonProperty("emailSenderAddress"), JsonPropertyName("emailSenderAddress")]
		public string EmailSenderAddress { get; set; } = "";

		[JsonProperty("useDummyEmailAddress"), JsonPropertyName("useDummyEmailAddress")]
		public bool UseDummyEmailAddress { get; set; } = false;

		[JsonProperty("dummyEmailAddress"), JsonPropertyName("dummyEmailAddress")]
		public string DummyEmailAddress { get; set; } = "";

		[JsonProperty("minCollapseAllDevices"), JsonPropertyName("minCollapseAllDevices"), UserConfigData]
		public int MinCollapseAllDevices { get; set; } = 15;

		[JsonProperty("messageViewTime"), JsonPropertyName("messageViewTime"), UserConfigData]
		public int MessageViewTime { get; set; } = 7;

		[JsonProperty("dailyCheckStartAt"), JsonPropertyName("dailyCheckStartAt")]
		public DateTime DailyCheckStartAt { get; set; } = new DateTime();

		[JsonProperty("maxImportDuration"), JsonPropertyName("maxImportDuration")]
		public int MaxImportDuration { get; set; } = 4;

		[JsonProperty("maxImportInterval"), JsonPropertyName("maxImportInterval")]
		public int MaxImportInterval { get; set; } = 12;

		[JsonProperty("reqAvailableTaskTypes"), JsonPropertyName("reqAvailableTaskTypes")]
		public string ReqAvailableTaskTypes { get; set; } = "";

		[JsonProperty("reqOwnerBased"), JsonPropertyName("reqOwnerBased")]
		public bool ReqOwnerBased { get; set; } = false;

		[JsonProperty("reqReducedView"), JsonPropertyName("reqReducedView")]
		public bool ReqReducedView { get; set; } = false;

		[JsonProperty("reqAllowObjectSearch"), JsonPropertyName("reqAllowObjectSearch")]
		public bool ReqAllowObjectSearch { get; set; } = false;

		[JsonProperty("reqAllowManualOwnerAdmin"), JsonPropertyName("reqAllowManualOwnerAdmin")]
		public bool AllowManualOwnerAdmin { get; set; } = false;

		[JsonProperty("reqPriorities"), JsonPropertyName("reqPriorities")]
		public string ReqPriorities { get; set; } = "";

		[JsonProperty("reqAutoCreateImplTasks"), JsonPropertyName("reqAutoCreateImplTasks")]
		public AutoCreateImplTaskOptions ReqAutoCreateImplTasks { get; set; } = AutoCreateImplTaskOptions.never;

		[JsonProperty("reqActivatePathAnalysis"), JsonPropertyName("reqActivatePathAnalysis")]
		public bool ReqActivatePathAnalysis { get; set; } = true;

		[JsonProperty("reqShowCompliance"), JsonPropertyName("reqShowCompliance")]
		public bool ReqShowCompliance { get; set; } = false;

		[JsonProperty("ruleOwnershipMode"), JsonPropertyName("ruleOwnershipMode")]
		public RuleOwnershipMode RuleOwnershipMode { get; set; } = RuleOwnershipMode.mixed;


		[JsonProperty("allowServerInConn"), JsonPropertyName("allowServerInConn")]
		public bool AllowServerInConn { get; set; } = true;

		[JsonProperty("allowServiceInConn"), JsonPropertyName("allowServiceInConn")]
		public bool AllowServiceInConn { get; set; } = true;

		[JsonProperty("overviewDisplayLines"), JsonPropertyName("overviewDisplayLines")]
		public int OverviewDisplayLines { get; set; } = 3;

		[JsonProperty("reducedProtocolSet"), JsonPropertyName("reducedProtocolSet")]
		public bool ReducedProtocolSet { get; set; } = true;

		[JsonProperty("importAppDataPath"), JsonPropertyName("importAppDataPath")]
		public string ImportAppDataPath { get; set; } = "";

		[JsonProperty("importAppDataSleepTime"), JsonPropertyName("importAppDataSleepTime")]
		public int ImportAppDataSleepTime { get; set; } = 24;

		[JsonProperty("importAppDataStartAt"), JsonPropertyName("importAppDataStartAt")]
		public DateTime ImportAppDataStartAt { get; set; } = new DateTime();

		[JsonProperty("importSubnetDataPath"), JsonPropertyName("importSubnetDataPath")]
		public string ImportSubnetDataPath { get; set; } = "";

		[JsonProperty("importSubnetDataSleepTime"), JsonPropertyName("importSubnetDataSleepTime")]
		public int ImportSubnetDataSleepTime { get; set; } = 24;

		[JsonProperty("importSubnetDataStartAt"), JsonPropertyName("importSubnetDataStartAt")]
		public DateTime ImportSubnetDataStartAt { get; set; } = new DateTime();

		[JsonProperty("modNamingConvention"), JsonPropertyName("modNamingConvention")]
		public string ModNamingConvention { get; set; } = "";

		[JsonProperty("modIconify"), JsonPropertyName("modIconify")]
		public bool ModIconify { get; set; } = true;

		[JsonProperty("modCommonAreas"), JsonPropertyName("modCommonAreas")]
		public string ModCommonAreas { get; set; } = "";

		[JsonProperty("modAppServerTypes"), JsonPropertyName("modAppServerTypes")]
		public string ModAppServerTypes { get; set; } = "";

		[JsonProperty("modReqInterfaceName"), JsonPropertyName("modReqInterfaceName")]
		public string ModReqInterfaceName { get; set; } = "";

		[JsonProperty("modReqEmailReceiver"), JsonPropertyName("modReqEmailReceiver")]
		public EmailRecipientOption ModReqEmailReceiver { get; set; } = EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty;

		[JsonProperty("modReqEmailRequesterInCc"), JsonPropertyName("modReqEmailRequesterInCc")]
		public bool ModReqEmailRequesterInCc { get; set; } = true;

		[JsonProperty("modReqEmailSubject"), JsonPropertyName("modReqEmailSubject")]
		public string ModReqEmailSubject { get; set; } = "";

		[JsonProperty("modReqEmailBody"), JsonPropertyName("modReqEmailBody")]
		public string ModReqEmailBody { get; set; } = "";

		[JsonProperty("modReqTicketTitle"), JsonPropertyName("modReqTicketTitle")]
		public string ModReqTicketTitle { get; set; } = "";

		[JsonProperty("modReqTaskTitle"), JsonPropertyName("modReqTaskTitle")]
		public string ModReqTaskTitle { get; set; } = "";


		[JsonProperty("extTicketSystems"), JsonPropertyName("extTicketSystems")]
		public string ExtTicketSystems { get; set; } = "";

		public ConfigData(bool editable = false)
		{
			Editable = editable;
		}

		public object Clone()
		{
			// Watch out for references they need to be deep cloned (currently none)
			ConfigData configData = (ConfigData)MemberwiseClone();
			return configData;
		}

		public object CloneEditable()
		{
			object clone = Clone();
			typeof(ConfigData).GetProperty("Editable")?.SetValue(clone, true);
			return clone;
		}
	}
}
