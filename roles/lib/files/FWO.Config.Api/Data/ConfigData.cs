using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
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

        [JsonProperty("useCustomLogo"), JsonPropertyName("useCustomLogo")]
        public bool UseCustomLogo { get; set; }

        [JsonProperty("customLogoData"), JsonPropertyName("customLogoData")]
        public string CustomLogoData { get; set; } = "";

        [JsonProperty("availableModules"), JsonPropertyName("availableModules")]
        public string AvailableModules { get; set; } = "";

        [JsonProperty("maxMessages"), JsonPropertyName("maxMessages"), UserConfigData]
        public int MaxMessages { get; set; } = 3;

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

        [JsonProperty("availableReportTypes"), JsonPropertyName("availableReportTypes")]
        public string AvailableReportTypes { get; set; } = "[]";

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
        public DateTime AutoDiscoverStartAt { get; set; } = DateTime.Now;

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
        public DateTime ImpChangeNotifyStartAt { get; set; } = DateTime.Now;

        [JsonProperty("externalRequestSleepTime"), JsonPropertyName("externalRequestSleepTime")]
        public int ExternalRequestSleepTime { get; set; } = 60;

        [JsonProperty("externalRequestStartAt"), JsonPropertyName("externalRequestStartAt")]
        public DateTime ExternalRequestStartAt { get; set; } = DateTime.Now;

        [JsonProperty("recertificationMode"), JsonPropertyName("recertificationMode")]
        public RecertificationMode RecertificationMode { get; set; } = RecertificationMode.RuleByRule;

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
        public DateTime DailyCheckStartAt { get; set; } = DateTime.Now;

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

        [JsonProperty("createApplicationZones"), JsonPropertyName("createApplicationZones")]
        public bool CreateAppZones { get; set; }

        [JsonProperty("dnsLookup"), JsonPropertyName("dnsLookup")]
        public bool DnsLookup { get; set; } = false;

        [JsonProperty("overwriteExistingNames"), JsonPropertyName("overwriteExistingNames")]
        public bool OverwriteExistingNames { get; set; } = false;

        [JsonProperty("autoReplaceAppServer"), JsonPropertyName("autoReplaceAppServer")]
        public bool AutoReplaceAppServer { get; set; } = false;

        [JsonProperty("importAppDataPath"), JsonPropertyName("importAppDataPath")]
        public string ImportAppDataPath { get; set; } = "";

        [JsonProperty("importAppDataSleepTime"), JsonPropertyName("importAppDataSleepTime")]
        public int ImportAppDataSleepTime { get; set; } = 24;

        [JsonProperty("importAppDataStartAt"), JsonPropertyName("importAppDataStartAt")]
        public DateTime ImportAppDataStartAt { get; set; } = DateTime.Now;

        [JsonProperty("ownerLdapId"), JsonPropertyName("ownerLdapId")]
        public int OwnerLdapId { get; set; } = GlobalConst.kLdapInternalId;

        [JsonProperty("manageOwnerLdapGroups"), JsonPropertyName("manageOwnerLdapGroups")]
        public bool ManageOwnerLdapGroups { get; set; } = true;

        [JsonProperty("ownerLdapGroupNames"), JsonPropertyName("ownerLdapGroupNames")]
        public string OwnerLdapGroupNames { get; set; } = GlobalConst.kLdapGroupPattern;
        
        [JsonProperty("importSubnetDataPath"), JsonPropertyName("importSubnetDataPath")]
        public string ImportSubnetDataPath { get; set; } = "";

        [JsonProperty("importSubnetDataSleepTime"), JsonPropertyName("importSubnetDataSleepTime")]
        public int ImportSubnetDataSleepTime { get; set; } = 24;

        [JsonProperty("importSubnetDataStartAt"), JsonPropertyName("importSubnetDataStartAt")]
        public DateTime ImportSubnetDataStartAt { get; set; } = DateTime.Now;

        [JsonProperty("modNamingConvention"), JsonPropertyName("modNamingConvention")]
        public string ModNamingConvention { get; set; } = "";

        [JsonProperty("modIconify"), JsonPropertyName("modIconify")]
        public bool ModIconify { get; set; } = true;

        [JsonProperty("modCommonAreas"), JsonPropertyName("modCommonAreas")]
        public string ModCommonAreas { get; set; } = "";

        [JsonProperty("modSpecUserAreas"), JsonPropertyName("modSpecUserAreas")]
        public string ModSpecUserAreas { get; set; } = "";

        [JsonProperty("modUpdatableObjAreas"), JsonPropertyName("modUpdatableObjAreas")]
        public string ModUpdatableObjAreas { get; set; } = "";

        [JsonProperty("modAppServerTypes"), JsonPropertyName("modAppServerTypes")]
        public string ModAppServerTypes { get; set; } = "";

        [JsonProperty("modReqInterfaceName"), JsonPropertyName("modReqInterfaceName")]
        public string ModReqInterfaceName { get; set; } = "";

        [JsonProperty("modReqEmailReceiver"), JsonPropertyName("modReqEmailReceiver")]
        public EmailRecipientOption ModReqEmailReceiver { get; set; } = EmailRecipientOption.None;

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

        [JsonProperty("modDecommEmailReceiver"), JsonPropertyName("modDecommEmailReceiver")]
        public EmailRecipientOption ModDecommEmailReceiver { get; set; } = EmailRecipientOption.None;

        [JsonProperty("modDecommEmailSubject"), JsonPropertyName("modDecommEmailSubject")]
        public string ModDecommEmailSubject { get; set; } = "";

        [JsonProperty("modDecommEmailBody"), JsonPropertyName("modDecommEmailBody")]
        public string ModDecommEmailBody { get; set; } = "";

        [JsonProperty("modRolloutActive"), JsonPropertyName("modRolloutActive")]
        public bool ModRolloutActive { get; set; } = true;

        [JsonProperty("modRolloutResolveServiceGroups"), JsonPropertyName("modRolloutResolveServiceGroups")]
        public bool ModRolloutResolveServiceGroups { get; set; } = true;

        [JsonProperty("modRolloutBundleTasks"), JsonPropertyName("modRolloutBundleTasks")]
        public bool ModRolloutBundleTasks { get; set; } = false;

        [JsonProperty("modRolloutNatHeuristic"), JsonPropertyName("modRolloutNatHeuristic")]
        public bool ModRolloutNatHeuristic { get; set; } = false;

        [JsonProperty("modRolloutErrorText"), JsonPropertyName("modRolloutErrorText")]
        public string ModRolloutErrorText { get; set; } = "";

        [JsonProperty("modRecertActive"), JsonPropertyName("modRecertActive")]
        public bool ModRecertActive { get; set; } = false;

        [JsonProperty("modRecertExpectAllModelled"), JsonPropertyName("modRecertExpectAllModelled")]
        public bool ModRecertExpectAllModelled { get; set; } = false;

        [JsonProperty("modRecertText"), JsonPropertyName("modRecertText")]
        public string ModRecertText { get; set; } = "";

        [JsonProperty("externalRequestWaitCycles"), JsonPropertyName("externalRequestWaitCycles")]
        public int ExternalRequestWaitCycles { get; set; } = 0;

        [JsonProperty("extTicketSystems"), JsonPropertyName("extTicketSystems")]
        public string ExtTicketSystems { get; set; } = "";

        [JsonProperty("modExtraConfigs"), JsonPropertyName("modExtraConfigs")]
        public string ModExtraConfigs { get; set; } = "";

        [JsonProperty("modModelledMarker"), JsonPropertyName("modModelledMarker")]
        public string ModModelledMarker { get; set; } = "FWOC";

        [JsonProperty("modModelledMarkerLocation"), JsonPropertyName("modModelledMarkerLocation")]
        public string ModModelledMarkerLocation { get; set; } = MarkerLocation.Rulename;

        [JsonProperty("ruleRecognitionOption"), JsonPropertyName("ruleRecognitionOption")]
        public string RuleRecognitionOption { get; set; } = "";

        [JsonProperty("varianceAnalysisSleepTime"), JsonPropertyName("varianceAnalysisSleepTime")]
        public int VarianceAnalysisSleepTime { get; set; } = 0;

        [JsonProperty("varianceAnalysisStartAt"), JsonPropertyName("varianceAnalysisStartAt")]
        public DateTime VarianceAnalysisStartAt { get; set; } = DateTime.Now;

        [JsonProperty("varianceAnalysisSync"), JsonPropertyName("varianceAnalysisSync")]
        public bool VarianceAnalysisSync { get; set; } = false;

        [JsonProperty("varianceAnalysisRefresh"), JsonPropertyName("varianceAnalysisRefresh")]
        public bool VarianceAnalysisRefresh { get; set; } = false;

        [JsonProperty("resolveNetworkAreas"), JsonPropertyName("resolveNetworkAreas")]
        public bool ResolveNetworkAreas { get; set; } = false;

        [JsonProperty("complianceCheckSleepTime"), JsonPropertyName("complianceCheckSleepTime")]
        public int ComplianceCheckSleepTime { get; set; } = 0;

        [JsonProperty("complianceCheckStartAt"), JsonPropertyName("complianceCheckStartAt")]
        public DateTime ComplianceCheckStartAt { get; set; } = DateTime.Now;

        [JsonProperty("complianceCheckPolicy"), JsonPropertyName("complianceCheckPolicy")]
        public int ComplianceCheckPolicyId { get; set; } = 0;

        [JsonProperty("complianceCheckMailRecipients"), JsonPropertyName("complianceCheckMailRecipients")]
        public string ComplianceCheckMailRecipients { get; set; } = "";

        [JsonProperty("complianceCheckMailSubject"), JsonPropertyName("complianceCheckMailSubject")]
        public string ComplianceCheckMailSubject { get; set; } = "";

        [JsonProperty("complianceCheckMailBody"), JsonPropertyName("complianceCheckMailBody")]
        public string ComplianceCheckMailBody { get; set; } = "";

        [JsonProperty("complianceMatrixAllowNetworkZones"), JsonPropertyName("complianceMatrixAllowNetworkZones")]
        public bool ComplianceMatrixAllowNetworkZones { get; set; } = false;

        [JsonProperty("complianceCheckScheduledDiffReportsIntervals"), JsonPropertyName("complianceCheckScheduledDiffReportsIntervals")]
        public string ComplianceCheckScheduledDiffReportsIntervals { get; set; } = "";

        [JsonProperty("complianceCheckInternetZoneObject"), JsonPropertyName("complianceCheckInternetZoneObject")]
        public string ComplianceCheckInternetZoneObject { get; set; } = "";

        [JsonProperty("complianceCheckMaxPrintedViolations"), JsonPropertyName("complianceCheckMaxPrintedViolations")]
        public int ComplianceCheckMaxPrintedViolations { get; set; } = 0;
        
        [JsonProperty("complianceCheckSortMatrixByID"), JsonPropertyName("complianceCheckSortMatrixByID")]
        public bool ComplianceCheckSortMatrixByID { get; set; } = false;

        [JsonProperty("complianceCheckRelevantManagements"), JsonPropertyName("complianceCheckRelevantManagements")]
        public string ComplianceCheckRelevantManagements { get; set; } = "";
        
        [JsonProperty("reportSchedulerConfig"), JsonPropertyName("reportSchedulerConfig")]
        public string ReportSchedulerConfig { get; set; } = "";

        [JsonProperty("debugConfig"), JsonPropertyName("debugConfig")]
        public string DebugConfig { get; set; } = "";

        [JsonProperty("autoCalculateInternetZone"), JsonPropertyName("autoCalculateInternetZone")]
        public bool AutoCalculateInternetZone { get; set; } = true;

        [JsonProperty("autoCalculateUndefinedInternalZone"), JsonPropertyName("autoCalculateUndefinedInternalZone")]
        public bool AutoCalculateUndefinedInternalZone { get; set; } = true;

        [JsonProperty("internalZoneRange_10_0_0_0_8"), JsonPropertyName("internalZoneRange_10_0_0_0_8")]
        public bool InternalZoneRange_10_0_0_0_8 { get; set; } = true;

        [JsonProperty("internalZoneRange_172_16_0_0_12"), JsonPropertyName("internalZoneRange_172_16_0_0_12")]
        public bool InternalZoneRange_172_16_0_0_12 { get; set; } = true;

        [JsonProperty("internalZoneRange_192_168_0_0_16"), JsonPropertyName("internalZoneRange_192_168_0_0_16")]
        public bool InternalZoneRange_192_168_0_0_16 { get; set; } = true;

        [JsonProperty("internalZoneRange_0_0_0_0_8"), JsonPropertyName("internalZoneRange_0_0_0_0_8")]
        public bool InternalZoneRange_0_0_0_0_8 { get; set; } = true;

        [JsonProperty("internalZoneRange_127_0_0_0_8"), JsonPropertyName("internalZoneRange_127_0_0_0_8")]
        public bool InternalZoneRange_127_0_0_0_8 { get; set; } = true;

        [JsonProperty("internalZoneRange_169_254_0_0_16"), JsonPropertyName("internalZoneRange_169_254_0_0_16")]
        public bool InternalZoneRange_169_254_0_0_16 { get; set; } = true;

        [JsonProperty("internalZoneRange_224_0_0_0_4"), JsonPropertyName("internalZoneRange_224_0_0_0_4")]
        public bool InternalZoneRange_224_0_0_0_4 { get; set; } = true;

        [JsonProperty("internalZoneRange_240_0_0_0_4"), JsonPropertyName("internalZoneRange_240_0_0_0_4")]
        public bool InternalZoneRange_240_0_0_0_4 { get; set; } = true;

        [JsonProperty("internalZoneRange_255_255_255_255_32"), JsonPropertyName("internalZoneRange_255_255_255_255_32")]
        public bool InternalZoneRange_255_255_255_255_32 { get; set; } = true;

        [JsonProperty("internalZoneRange_192_0_2_0_24"), JsonPropertyName("internalZoneRange_192_0_2_0_24")]
        public bool InternalZoneRange_192_0_2_0_24 { get; set; } = true;

        [JsonProperty("internalZoneRange_198_51_100_0_24"), JsonPropertyName("internalZoneRange_198_51_100_0_24")]
        public bool InternalZoneRange_198_51_100_0_24 { get; set; } = true;

        [JsonProperty("internalZoneRange_203_0_113_0_24"), JsonPropertyName("internalZoneRange_203_0_113_0_24")]
        public bool InternalZoneRange_203_0_113_0_24 { get; set; } = true;

        [JsonProperty("internalZoneRange_100_64_0_0_10"), JsonPropertyName("internalZoneRange_100_64_0_0_10")]
        public bool InternalZoneRange_100_64_0_0_10 { get; set; } = true;

        [JsonProperty("internalZoneRange_192_0_0_0_24"), JsonPropertyName("internalZoneRange_192_0_0_0_24")]
        public bool InternalZoneRange_192_0_0_0_24 { get; set; } = true;

        [JsonProperty("internalZoneRange_192_88_99_0_24"), JsonPropertyName("internalZoneRange_192_88_99_0_24")]
        public bool InternalZoneRange_192_88_99_0_24 { get; set; } = true;

        [JsonProperty("internalZoneRange_198_18_0_0_15"), JsonPropertyName("internalZoneRange_198_18_0_0_15")]
        public bool InternalZoneRange_198_18_0_0_15 { get; set; } = true;

        [JsonProperty("autoCalculatedZonesAtTheEnd"), JsonPropertyName("autoCalculatedZonesAtTheEnd")]
        public bool AutoCalculatedZonesAtTheEnd { get; set; } = true;

        [JsonProperty("treatDynamicAndDomainObjectsAsInternet"), JsonPropertyName("treatDynamicAndDomainObjectsAsInternet")]
        public bool TreatDynamicAndDomainObjectsAsInternet { get; set; } = true;

        [JsonProperty("showShortColumnsInComplianceReports"), JsonPropertyName("showShortColumnsInComplianceReports")]
        public bool ShowShortColumnsInComplianceReports { get; set; } = true;

        [JsonProperty("importedMatrixReadOnly"), JsonPropertyName("importedMatrixReadOnly")]
        public bool ImportedMatrixReadOnly { get; set; } = true;

        [JsonProperty("complianceCheckElementsPerFetch"), JsonPropertyName("complianceCheckElementsPerFetch")]
        public int ComplianceCheckElementsPerFetch { get; set; } = 500;

        [JsonProperty("complianceCheckAvailableProcessors"), JsonPropertyName("complianceCheckAvailableProcessors")]
        public int ComplianceCheckAvailableProcessors { get; set; } = 4;
        
        [JsonProperty("complianceFilterOutInitialViolations"), JsonPropertyName("complianceFilterOutInitialViolations")]
        public bool ComplianceFilterOutInitialViolations { get; set; } = false;


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
