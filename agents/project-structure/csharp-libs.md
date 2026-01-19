C# shared libraries live under `roles/lib/files` and provide the common building blocks for the middleware, UI, and services. They include API clients, data models, reporting, compliance, configuration, and integration helpers. The summaries below describe each code file under roles/lib/files/ so agents can quickly locate the right library entry points.

## FWO.Api.Client

### `roles/lib/files/FWO.Api.Client/APIConnection.cs`
API connection wrapper for api connection. It manages request headers, endpoints, and execution.

### `roles/lib/files/FWO.Api.Client/ApiConstants.cs`
API client helper for api constants. It supports GraphQL or REST interactions.

### `roles/lib/files/FWO.Api.Client/ApiResponse.cs`
API response model for api response payloads. It wraps data and error handling for client calls.

### `roles/lib/files/FWO.Api.Client/ApiSubscription.cs`
Subscription helper for GraphQL updates (api subscription). It handles callbacks and error handling for live updates.

### `roles/lib/files/FWO.Api.Client/GraphQlApiConnection.cs`
API connection wrapper for graph ql api connection. It manages request headers, endpoints, and execution.

### `roles/lib/files/FWO.Api.Client/GraphQlApiSubscription.cs`
Subscription helper for GraphQL updates (graph ql api subscription). It handles callbacks and error handling for live updates.

### `roles/lib/files/FWO.Api.Client/Queries/AuthQueries.cs`
GraphQL query definitions for auth operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ComplianceQueries.cs`
GraphQL query definitions for compliance operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ConfigQueries.cs`
GraphQL query definitions for config operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/DeviceQueries.cs`
GraphQL query definitions for device operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ExtRequestQueries.cs`
GraphQL query definitions for ext request operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ImportQueries.cs`
GraphQL query definitions for import operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ModellingQueries.cs`
GraphQL query definitions for modelling operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/MonitorQueries.cs`
GraphQL query definitions for monitor operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/NetworkAnalysisQueries.cs`
GraphQL query definitions for network analysis operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/NotificationQueries.cs`
GraphQL query definitions for notification operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ObjectQueries.cs`
GraphQL query definitions for object operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/OwnerQueries.cs`
GraphQL query definitions for owner operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/Queries.cs`
GraphQL query definitions for shared operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/RecertQueries.cs`
GraphQL query definitions for recert operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/ReportQueries.cs`
GraphQL query definitions for report operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/RequestQueries.cs`
GraphQL query definitions for request operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/RuleQueries.cs`
GraphQL query definitions for rule operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/Queries/StmQueries.cs`
GraphQL query definitions for stm operations. Used by API clients to assemble Hasura requests and subscriptions.

### `roles/lib/files/FWO.Api.Client/RestApiClient.cs`
REST API client helper for rest api client. It wraps HTTP calls to REST endpoints.

## FWO.Basics

### `roles/lib/files/FWO.Basics/BooleanExtensions.cs`
Extension methods for boolean extensions. They add helper behavior to existing types.

### `roles/lib/files/FWO.Basics/Comparer/ComplianceViolationComparer.cs`
Comparer implementation for Compliance Violation values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Basics/Comparer/IPAddressComparer.cs`
Comparer implementation for IP Address values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Basics/Comparer/IPAddressRangeComparer.cs`
Comparer implementation for IP Address Range values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Basics/Enums/AssessabilityIssue.cs`
Enum definitions for assessability issue. They provide shared constants across libraries.

### `roles/lib/files/FWO.Basics/Exceptions/ConfigException.cs`
Exception type for Config Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Basics/Exceptions/EnvironmentException.cs`
Exception type for Environment Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Basics/Exceptions/InternalException.cs`
Exception type for Internal Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Basics/Exceptions/LdapConnectionException.cs`
Exception type for Ldap Connection Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Basics/Exceptions/ProcessingFailedException.cs`
Exception type for Processing Failed Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Basics/GlobalConstants.cs`
Shared constants for global constants. Used to centralize magic values across modules.

### `roles/lib/files/FWO.Basics/GlobalFunctions.cs`
Core utility for global functions. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/ITreeItem.cs`
Core utility for i tree item. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/Icons.cs`
Core utility for icons. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/Interfaces/IComplianceViolation.cs`
Interface definition for i compliance violation. It standardizes contracts across components.

### `roles/lib/files/FWO.Basics/Interfaces/ILogger.cs`
Interface definition for i logger. It standardizes contracts across components.

### `roles/lib/files/FWO.Basics/Interfaces/IRuleViewData.cs`
Interface definition for i rule view data. It standardizes contracts across components.

### `roles/lib/files/FWO.Basics/IpOperations.cs`
Core utility for ip operations. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/JwtConstants.cs`
Shared constants for jwt constants. Used to centralize magic values across modules.

### `roles/lib/files/FWO.Basics/LocalSettings.cs`
Core utility for local settings. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/Module.cs`
Core utility for module. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/ReportType.cs`
Core utility for report type. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/Roles.cs`
Core utility for roles. It provides shared helpers and domain constants.

### `roles/lib/files/FWO.Basics/SCConstants.cs`
Shared constants for sc constants. Used to centralize magic values across modules.

### `roles/lib/files/FWO.Basics/StringExtensionsHtml.cs`
Extension methods for string extensions html. They add helper behavior to existing types.

### `roles/lib/files/FWO.Basics/StringExtensionsIp.cs`
Extension methods for string extensions ip. They add helper behavior to existing types.

### `roles/lib/files/FWO.Basics/StringExtensionsLdap.cs`
Extension methods for string extensions ldap. They add helper behavior to existing types.

### `roles/lib/files/FWO.Basics/StringExtensionsSanitizer.cs`
Extension methods for string extensions sanitizer. They add helper behavior to existing types.

### `roles/lib/files/FWO.Basics/TestDataGeneration/TestDataGenerationResult.cs`
Test data generation helper for test data generation result. It produces sample data for tests and demos.

### `roles/lib/files/FWO.Basics/TestDataGeneration/TestDataGenerator.cs`
Test data generation helper for test data generator. It produces sample data for tests and demos.

### `roles/lib/files/FWO.Basics/TreeItem.cs`
Core utility for tree item. It provides shared helpers and domain constants.

## FWO.Compliance

### `roles/lib/files/FWO.Compliance/ComplianceCheck.cs`
Compliance logic for compliance check. It evaluates compliance rules and records results.

### `roles/lib/files/FWO.Compliance/ComplianceCheckResult.cs`
Compliance logic for compliance check result. It evaluates compliance rules and records results.

## FWO.Config.Api

### `roles/lib/files/FWO.Config.Api/Config.cs`
API configuration helper for config. It loads and exposes configuration data from the backend.

### `roles/lib/files/FWO.Config.Api/Data/CommonArea.cs`
Configuration data model for common area. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/Data/ConfigData.cs`
Configuration data model for config data. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/Data/ConfigItem.cs`
Configuration data model for config item. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/Data/Language.cs`
Configuration data model for language. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/Data/RecertCheckParams.cs`
Configuration data model for recert check params. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/Data/UiText.cs`
Configuration data model for ui text. It represents API-provided settings and texts.

### `roles/lib/files/FWO.Config.Api/GlobalConfig.cs`
API configuration helper for global config. It loads and exposes configuration data from the backend.

### `roles/lib/files/FWO.Config.Api/UserConfig.cs`
API configuration helper for user config. It loads and exposes configuration data from the backend.

## FWO.Config.File

### `roles/lib/files/FWO.Config.File/ConfigFile.cs`
File-based configuration helper for config file. It loads settings and key material from local files.

### `roles/lib/files/FWO.Config.File/KeyImporter.cs`
File-based configuration helper for key importer. It loads settings and key material from local files.

## FWO.Data

### `roles/lib/files/FWO.Data/ActionItem.cs`
Data model for action item. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Alert.cs`
Data model for alert. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ApiCrudHelper.cs`
Data model for api crud helper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/CSVAppServerImportModel.cs`
Data model for csv app server import model. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/CSVFileUploadErrorModel.cs`
Data model for csv file upload error model. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ChangeImport.cs`
Data model for change import. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Cidr.cs`
Data model for cidr. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Color.cs`
Data model for color. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ComplianceCriterion.cs`
Data model for compliance criterion. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ComplianceNetworkZone.cs`
Data model for compliance network zone. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/CompliancePolicy.cs`
Data model for compliance policy. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ComplianceViolation.cs`
Data model for compliance violation. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ComplianceViolationType.cs`
Data model for compliance violation type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Device.cs`
Data model for device. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/DeviceType.cs`
Data model for device type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/DeviceWrapper.cs`
Data model for device wrapper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Direction.cs`
Data model for direction. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/DisplayBase.cs`
Data model for display base. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/DistName.cs`
Data model for dist name. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/EmailRecipientOption.cs`
Data model for email recipient option. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ErrorBaseModel.cs`
Data model for error base model. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ExtStates.cs`
Data model for ext states. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Extensions/ComplianceExtensions.cs`
Data model for compliance extensions. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ExternalRequest.cs`
Data model for external request. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ExternalTicketSystem.cs`
Data model for external ticket system. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/FwoNotification.cs`
Data model for fwo notification. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/FwoOwner.cs`
Data model for fwo owner. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/FwoOwnerBase.cs`
Data model for fwo owner base. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Group.cs`
Data model for group. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/GroupFlat.cs`
Data model for group flat. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Import.cs`
Data model for import. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ImportCredential.cs`
Data model for import credential. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ImportStatus.cs`
Data model for import status. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/IpProtocol.cs`
Data model for ip protocol. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/JsonCustomConverters.cs`
Data model for json custom converters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/LdapConnectionBase.cs`
Data model for ldap connection base. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/LinkType.cs`
Data model for link type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/LogEntry.cs`
Data model for log entry. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Management.cs`
Data model for management. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/MessageType.cs`
Data model for message type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/AuthenticationServerParameters.cs`
Data model for authentication server parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/AuthenticationTokenParameters.cs`
Data model for authentication token parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/ComplianceParameters.cs`
Data model for compliance parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/DebugConfig.cs`
Data model for debug config. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/ExternalRequestParameters.cs`
Data model for external request parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/GroupParameters.cs`
Data model for group parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/NormalizedConfigParameters.cs`
Data model for normalized config parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/ReportParameters.cs`
Data model for report parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/RoleParameters.cs`
Data model for role parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/TenantParameters.cs`
Data model for tenant parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Middleware/UserParameters.cs`
Data model for user parameters. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Modelling/ModellingAppRole.cs`
Modelling data model for modelling app role. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingAppServer.cs`
Modelling data model for modelling app server. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingAppZone.cs`
Modelling data model for modelling app zone. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingConnection.cs`
Modelling data model for modelling connection. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingDnDContainer.cs`
Modelling data model for modelling dn d container. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingExtraConfig.cs`
Modelling data model for modelling extra config. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingHistoryEntry.cs`
Modelling data model for modelling history entry. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingManagedIdString.cs`
Modelling data model for modelling managed id string. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingNamingConvention.cs`
Modelling data model for modelling naming convention. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingNetworkArea.cs`
Modelling data model for modelling network area. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingNwGroup.cs`
Modelling data model for modelling nw group. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingNwObject.cs`
Modelling data model for modelling nw object. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingObject.cs`
Modelling data model for modelling object. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingService.cs`
Modelling data model for modelling service. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingServiceGroup.cs`
Modelling data model for modelling service group. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingSvcObject.cs`
Modelling data model for modelling svc object. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingTypes.cs`
Modelling data model for modelling types. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/ModellingVarianceResult.cs`
Modelling data model for modelling variance result. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/Modelling/RuleRecognitionOption.cs`
Modelling data model for rule recognition option. It represents modelling entities for API and UI flows.

### `roles/lib/files/FWO.Data/NatData.cs`
Data model for nat data. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkLocation.cs`
Data model for network location. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkObject.cs`
Data model for network object. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkObjectType.cs`
Data model for network object type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkObjectWrapper.cs`
Data model for network object wrapper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkProtocol.cs`
Data model for network protocol. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkService.cs`
Data model for network service. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkServiceType.cs`
Data model for network service type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkUser.cs`
Data model for network user. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkUserType.cs`
Data model for network user type. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NetworkZone.cs`
Data model for network zone. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedConfig.cs`
Data model for normalized config. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedGateway.cs`
Data model for normalized gateway. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedNetworkObject.cs`
Data model for normalized network object. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedRule.cs`
Data model for normalized rule. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedRulebase.cs`
Data model for normalized rulebase. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedRulebaseLink.cs`
Data model for normalized rulebase link. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedServiceObject.cs`
Data model for normalized service object. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NormalizedZoneObject.cs`
Data model for normalized zone object. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/NotificationLayout.cs`
Data model for notification layout. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ObjectStatistics.cs`
Data model for object statistics. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/OwnerIdModel.cs`
Data model for owner id model. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/OwnerLifeCycleState.cs`
Data model for owner life cycle state. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/OwnerRecertification.cs`
Data model for owner recertification. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/OwnerRefresh.cs`
Data model for owner refresh. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/PaginationVariables.cs`
Data model for pagination variables. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Recertification.cs`
Data model for recertification. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RecertificationBase.cs`
Data model for recertification base. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RecertificationMode.cs`
Data model for recertification mode. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Report/ComplianceFilter.cs`
Report data model for compliance filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ConnectionReport.cs`
Report data model for connection report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/DeviceFilter.cs`
Report data model for device filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/DeviceReport.cs`
Report data model for device report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/FileFormat.cs`
Report data model for file format. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/GlobalCommonSvcReport.cs`
Report data model for global common svc report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ManagementReport.cs`
Report data model for management report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ModellingFilter.cs`
Report data model for modelling filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/OwnerConnectionReport.cs`
Report data model for owner connection report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/RecertFilter.cs`
Report data model for recert filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ReportData.cs`
Report data model for report data. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ReportFile.cs`
Report data model for report file. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ReportSchedule.cs`
Report data model for report schedule. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ReportTemplate.cs`
Report data model for report template. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/RulebaseReport.cs`
Report data model for rulebase report. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/ScheduledComplianceDiffReportConfig.cs`
Report data model for scheduled compliance diff report config. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/TenantFilter.cs`
Report data model for tenant filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/TimeFilter.cs`
Report data model for time filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Report/UnusedFilter.cs`
Report data model for unused filter. It carries report parameters and results between layers.

### `roles/lib/files/FWO.Data/Role.cs`
Data model for role. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Rule.cs`
Data model for rule. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RuleAction.cs`
Data model for rule action. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RuleChange.cs`
Data model for rule change. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RuleMetadata.cs`
Data model for rule metadata. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Rulebase.cs`
Data model for rulebase. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/RulebaseLink.cs`
Data model for rulebase link. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Sanitizer.cs`
Data model for sanitizer. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/SchedulerInterval.cs`
Data model for scheduler interval. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/ServiceWrapper.cs`
Data model for service wrapper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Tenant.cs`
Data model for tenant. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/TicketId.cs`
Data model for ticket id. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/TimeWrapper.cs`
Data model for time wrapper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Tracking.cs`
Data model for tracking. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/UiLdapConnection.cs`
Data model for ui ldap connection. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/UiUser.cs`
Data model for ui user. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/UserGroup.cs`
Data model for user group. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/UserWrapper.cs`
Data model for user wrapper. Used across services, API clients, and UI for serialization and transport.

### `roles/lib/files/FWO.Data/Workflow/NwObjectElement.cs`
Workflow data model for network object element. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/NwRuleElement.cs`
Workflow data model for network rule element. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/NwServiceElement.cs`
Workflow data model for network service element. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/OwnerTicket.cs`
Workflow data model for owner ticket. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfApproval.cs`
Workflow data model for workflow approval. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfApprovalBase.cs`
Workflow data model for workflow approval base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfApprovalWriter.cs`
Workflow data model for workflow approval writer. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfComment.cs`
Workflow data model for workflow comment. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfCommentBase.cs`
Workflow data model for workflow comment base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfElementBase.cs`
Workflow data model for workflow element base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfExtState.cs`
Workflow data model for workflow ext state. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfImplElement.cs`
Workflow data model for workflow impl element. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfImplTask.cs`
Workflow data model for workflow impl task. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfOwnerWriter.cs`
Workflow data model for workflow owner writer. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfPriority.cs`
Workflow data model for workflow priority. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfReqElement.cs`
Workflow data model for workflow req element. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfReqElementWriter.cs`
Workflow data model for workflow req element writer. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfReqTask.cs`
Workflow data model for workflow req task. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfReqTaskBase.cs`
Workflow data model for workflow req task base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfReqTaskWriter.cs`
Workflow data model for workflow req task writer. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfStateAction.cs`
Workflow data model for workflow state action. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfStatefulObject.cs`
Workflow data model for workflow stateful object. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfStates.cs`
Workflow data model for workflow states. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfTaskBase.cs`
Workflow data model for workflow task base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfTicket.cs`
Workflow data model for workflow ticket. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfTicketBase.cs`
Workflow data model for workflow ticket base. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/Workflow/WfTicketWriter.cs`
Workflow data model for workflow ticket writer. Used to serialize workflow tasks, states, and transitions.

### `roles/lib/files/FWO.Data/ZoneWrapper.cs`
Data model for zone wrapper. Used across services, API clients, and UI for serialization and transport.

## FWO.DeviceAutoDiscovery

### `roles/lib/files/FWO.DeviceAutoDiscovery/AutoDiscoveryBase.cs`
Device autodiscovery helper for auto discovery base. It connects to managers to discover devices and metadata.

### `roles/lib/files/FWO.DeviceAutoDiscovery/AutoDiscoveryCpMds.cs`
Device autodiscovery helper for auto discovery checkpoint mds. It connects to managers to discover devices and metadata.

### `roles/lib/files/FWO.DeviceAutoDiscovery/AutoDiscoveryFortiManager.cs`
Device autodiscovery helper for auto discovery forti manager. It connects to managers to discover devices and metadata.

### `roles/lib/files/FWO.DeviceAutoDiscovery/CheckPointAPI.cs`
Device autodiscovery helper for check point api. It connects to managers to discover devices and metadata.

### `roles/lib/files/FWO.DeviceAutoDiscovery/FortiManagerAPI.cs`
Device autodiscovery helper for forti manager api. It connects to managers to discover devices and metadata.

### `roles/lib/files/FWO.DeviceAutoDiscovery/FortiManagerData.cs`
Device autodiscovery helper for forti manager data. It connects to managers to discover devices and metadata.

## FWO.Encryption

### `roles/lib/files/FWO.Encryption/AesEnc.cs`
Encryption helper for aes enc. It provides AES-based encryption and decryption utilities.

## FWO.ExternalSystems

### `roles/lib/files/FWO.ExternalSystems/ExternalTicket.cs`
External system integration for external ticket. It defines ticket and task abstractions for external workflows.

### `roles/lib/files/FWO.ExternalSystems/ExternalTicketTask.cs`
External system integration for external ticket task. It defines ticket and task abstractions for external workflows.

### `roles/lib/files/FWO.ExternalSystems/Tufin.SecureChange/SCAccessRequestTicketTask.cs`
Tufin SecureChange integration for sc access request ticket task. It builds SecureChange requests and parses responses.

### `roles/lib/files/FWO.ExternalSystems/Tufin.SecureChange/SCClient.cs`
Tufin SecureChange integration for sc client. It builds SecureChange requests and parses responses.

### `roles/lib/files/FWO.ExternalSystems/Tufin.SecureChange/SCNetworkObjectModifyTicketTask.cs`
Tufin SecureChange integration for sc network object modify ticket task. It builds SecureChange requests and parses responses.

### `roles/lib/files/FWO.ExternalSystems/Tufin.SecureChange/SCTicket.cs`
Tufin SecureChange integration for sc ticket. It builds SecureChange requests and parses responses.

### `roles/lib/files/FWO.ExternalSystems/Tufin.SecureChange/SCTicketTask.cs`
Tufin SecureChange integration for sc ticket task. It builds SecureChange requests and parses responses.

## FWO.Logging

### `roles/lib/files/FWO.Logging/Log.cs`
Central logging facade for the platform. It exposes static helpers to write log entries with context.

### `roles/lib/files/FWO.Logging/LogType.cs`
Log type and category definitions. Used to classify log entries across services.

### `roles/lib/files/FWO.Logging/Logger.cs`
Logging backend implementation. It handles log sinks and formatting for log output.

## FWO.Mail

### `roles/lib/files/FWO.Mail/EmailConnection.cs`
Email helper for email connection. It encapsulates mail configuration and sending behavior.

### `roles/lib/files/FWO.Mail/EmailForm.cs`
Email helper for email form. It encapsulates mail configuration and sending behavior.

### `roles/lib/files/FWO.Mail/MailerMailKit.cs`
Email helper for mailer mail kit. It encapsulates mail configuration and sending behavior.

## FWO.Middleware.Client

### `roles/lib/files/FWO.Middleware.Client/JwtReader.cs`
JWT parser and validator for UI clients. It extracts claims and validates token lifetimes.

### `roles/lib/files/FWO.Middleware.Client/MiddlewareClient.cs`
Client for middleware REST endpoints. It wraps authentication and API calls from UI or services.

## FWO.Recert

### `roles/lib/files/FWO.Recert/RecertHandler.cs`
Recertification helper for recert handler. It recalculates or manages recertification data.

### `roles/lib/files/FWO.Recert/RecertRefresh.cs`
Recertification helper for recert refresh. It recalculates or manages recertification data.

## FWO.Report

### `roles/lib/files/FWO.Report/Data/ReportSchedulerConfig.cs`
Report generator for scheduler config outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/Data/ToCHeader.cs`
Report data model for to c header. It represents report metadata or table-of-contents structures.

### `roles/lib/files/FWO.Report/Data/ToCItem.cs`
Report data model for to c item. It represents report metadata or table-of-contents structures.

### `roles/lib/files/FWO.Report/Data/ViewData/RuleViewData.cs`
Report data model for rule view data. It represents report metadata or table-of-contents structures.

### `roles/lib/files/FWO.Report/Display/NatRuleDisplayHtml.cs`
Renderer for nat rule in HTML format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/NwObjDisplay.cs`
Renderer for nw obj in DISPLAY format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleChangeDisplayCsv.cs`
Renderer for rule change in CSV format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleChangeDisplayHtml.cs`
Renderer for rule change in HTML format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleChangeDisplayJson.cs`
Renderer for rule change in JSON format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleDifferenceDisplayHtml.cs`
Renderer for rule difference in HTML format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleDisplayBase.cs`
Renderer for rule base in DISPLAY format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleDisplayCsv.cs`
Renderer for rule in CSV format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleDisplayHtml.cs`
Renderer for rule in HTML format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/Display/RuleDisplayJson.cs`
Renderer for rule in JSON format. Used by report exports to format output data.

### `roles/lib/files/FWO.Report/NormalizedConfigGenerator.cs`
Generates normalized configuration snapshots for reporting and analysis. It assembles data from the API into normalized structures.

### `roles/lib/files/FWO.Report/PaperFormat.cs`
Report helper for paper format. It supports reporting output and formatting.

### `roles/lib/files/FWO.Report/RecertificateOwner.cs`
Recertification helper for owner-specific processing. It encapsulates recertification data for reports and workflows.

### `roles/lib/files/FWO.Report/ReportAppRules.cs`
Report generator for app rules outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportBase.cs`
Report generator for base outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportChanges.cs`
Report generator for changes outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportCompliance.cs`
Report generator for compliance outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportComplianceDiff.cs`
Report generator for compliance diff outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportConnections.cs`
Report generator for connections outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportDevicesBase.cs`
Report generator for devices base outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportGenerator.cs`
Report generator for generator outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportHtmlTemplate.html`
HTML template used by the report renderer. It provides the base markup that report exports fill with data.

### `roles/lib/files/FWO.Report/ReportNatRules.cs`
Report generator for nat rules outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportOwnerRecerts.cs`
Report generator for owner recerts outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportOwnersBase.cs`
Report generator for owners base outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportRecertEvent.cs`
Report generator for recert event outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportRules.cs`
Report generator for rules outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportStatistics.cs`
Report generator for statistics outputs. It builds report data and export formats for reporting workflows.

### `roles/lib/files/FWO.Report/ReportVariances.cs`
Report generator for variances outputs. It builds report data and export formats for reporting workflows.

## FWO.Report.Filter

### `roles/lib/files/FWO.Report.Filter/Ast/AstKind.cs`
AST node type for filter parsing (ast kind). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNode.cs`
AST node type for filter parsing (ast node). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeConnector.cs`
AST node type for filter parsing (ast node connector). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilter.cs`
AST node type for filter parsing (ast node filter). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterBool.cs`
AST node type for filter parsing (ast node filter bool). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterDateTimeRange.cs`
AST node type for filter parsing (ast node filter date time range). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterInt.cs`
AST node type for filter parsing (ast node filter int). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterNetwork.cs`
AST node type for filter parsing (ast node filter network). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterReportType.cs`
AST node type for filter parsing (ast node filter report type). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeFilterString.cs`
AST node type for filter parsing (ast node filter string). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Ast/AstNodeUnary.cs`
AST node type for filter parsing (ast node unary). Used by the filter compiler to represent parsed expressions.

### `roles/lib/files/FWO.Report.Filter/Compiler.cs`
Core compiler for the report filter language. It converts filter strings into parsed and executable structures.

### `roles/lib/files/FWO.Report.Filter/DynGraphqlQuery.cs`
Filter subsystem component for dyn graphql query. It supports parsing and validation of report filters.

### `roles/lib/files/FWO.Report.Filter/Exceptions/FilterException.cs`
Exception type for Filter Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Report.Filter/Exceptions/SemanticException.cs`
Exception type for Semantic Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Report.Filter/Exceptions/SyntaxException.cs`
Exception type for Syntax Exception errors. Used to signal and handle domain-specific failure cases.

### `roles/lib/files/FWO.Report.Filter/FilterTypes/DateTimeRange.cs`
Filter model for date time range values. It captures filter state for report generation.

### `roles/lib/files/FWO.Report.Filter/FilterTypes/ReportFilters.cs`
Filter model for report filters values. It captures filter state for report generation.

### `roles/lib/files/FWO.Report.Filter/Parser.cs`
Core parser for the report filter language. It converts filter strings into parsed and executable structures.

### `roles/lib/files/FWO.Report.Filter/Scanner.cs`
Core scanner for the report filter language. It converts filter strings into parsed and executable structures.

### `roles/lib/files/FWO.Report.Filter/Token.cs`
Token definition for the report filter language. It is used by the scanner and parser to classify input.

### `roles/lib/files/FWO.Report.Filter/TokenKind.cs`
Token definition for the report filter language. It is used by the scanner and parser to classify input.

### `roles/lib/files/FWO.Report.Filter/TokenSyntax.cs`
Token definition for the report filter language. It is used by the scanner and parser to classify input.

## FWO.Services

### `roles/lib/files/FWO.Services/ActionHandler.cs`
Service logic for action handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/AppRoleComparer.cs`
Comparer implementation for App Role values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Services/AppServerComparer.cs`
Comparer implementation for App Server values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Services/AppServerHelper.cs`
Service logic for app server helper. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/DefaultInit.cs`
Service logic for default init. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/EmailHelper.cs`
Service logic for email helper. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/EventMediator/EventMediator.cs`
Event mediator for event mediator. It dispatches events to interested subscribers.

### `roles/lib/files/FWO.Services/EventMediator/Events/AppServerImportEvent.cs`
Event payload for app server import event. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/AppServerImportEventArgs.cs`
Event payload for app server import event args. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/CollectionChangedEvent.cs`
Event payload for collection changed event. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/CollectionChangedEventArgs.cs`
Event payload for collection changed event args. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/FileUploadEvent.cs`
Event payload for file upload event. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/FileUploadEventArgs.cs`
Event payload for file upload event args. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/UserSessionClosedEvent.cs`
Event payload for user session closed event. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Events/UserSessionClosedEventArgs.cs`
Event payload for user session closed event args. It carries event data through the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Interfaces/IEvent.cs`
Event mediator interface for i event. It defines event contracts used by the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Interfaces/IEventArgs.cs`
Event mediator interface for i event args. It defines event contracts used by the mediator.

### `roles/lib/files/FWO.Services/EventMediator/Interfaces/IEventMediator.cs`
Event mediator interface for i event mediator. It defines event contracts used by the mediator.

### `roles/lib/files/FWO.Services/ExtStateHandler.cs`
Service logic for ext state handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/GroupAccess.cs`
Service logic for group access. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingAppRoleHandler.cs`
Service logic for modelling app role handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingAppServerHandler.cs`
Service logic for modelling app server handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingAppServerListHandler.cs`
Service logic for modelling app server list handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingAppZoneHandler.cs`
Service logic for modelling app zone handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingConnectionHandler.cs`
Service logic for modelling connection handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingHandlerBase.cs`
Service logic for modelling handler base. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingServiceGroupHandler.cs`
Service logic for modelling service group handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingServiceHandler.cs`
Service logic for modelling service handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingVarianceAnalysis.cs`
Service logic for modelling variance analysis. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingVarianceAnalysisGetProd.cs`
Service logic for modelling variance analysis get prod. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingVarianceAnalysisObjectsForRequest.cs`
Service logic for modelling variance analysis objects for request. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ModellingVarianceAnalysisRules.cs`
Service logic for modelling variance analysis rules. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/NetworkObjectComparer.cs`
Comparer implementation for Network Object values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Services/NetworkServiceComparer.cs`
Comparer implementation for Network Service values. Used for ordering or equality checks in collections.

### `roles/lib/files/FWO.Services/NetworkZoneService.cs`
Service logic for network zone service. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/ParallelProcessor.cs`
Service logic for parallel processor. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/PathAnalysis.cs`
Service logic for path analysis. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/RuleTreeBuilder/IRuleTreeBuilder.cs`
Rule tree builder component for i rule tree builder. It constructs hierarchical rule trees for reporting and UI.

### `roles/lib/files/FWO.Services/RuleTreeBuilder/RuleTreeBuilder.cs`
Rule tree builder component for rule tree builder. It constructs hierarchical rule trees for reporting and UI.

### `roles/lib/files/FWO.Services/RuleTreeBuilder/RuleTreeItem.cs`
Rule tree builder component for rule tree item. It constructs hierarchical rule trees for reporting and UI.

### `roles/lib/files/FWO.Services/ServiceProvider.cs`
Service logic for service provider. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/StateMatrix.cs`
Service logic for state matrix. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/TicketCreator.cs`
Service logic for ticket creator. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/WfDbAccess.cs`
Service logic for workflow db access. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/WfHandler.cs`
Service logic for workflow handler. It coordinates workflows, API calls, and domain rules.

### `roles/lib/files/FWO.Services/WfStateDict.cs`
Service logic for workflow state dict. It coordinates workflows, API calls, and domain rules.
