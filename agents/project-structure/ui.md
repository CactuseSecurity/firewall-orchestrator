UI (FWO.UI) is the Blazor Server frontend that delivers workflows for reporting, monitoring, compliance, modelling, and change requests. It composes Razor pages, shared components, and service helpers into the application shell and layouts. The summaries below describe each code file under roles/ui/files/FWO.UI/ so agents can find the right entry points quickly.

## Startup and application shell

### `roles/ui/files/FWO.UI/Program.cs`
Configures DI, authentication, localization, and middleware for the Blazor Server app. It wires app settings, API clients, and server-side services used across the UI.

### `roles/ui/files/FWO.UI/App.razor`
Root component that defines routing and top-level layout selection. It controls navigation fallbacks and error boundaries for page rendering.

### `roles/ui/files/FWO.UI/_Imports.razor`
Project-wide Razor imports for common namespaces and components. It keeps shared using directives available to all Razor files.

### `roles/ui/files/FWO.UI/Pages/_Host.cshtml`
Server-side host page that bootstraps Blazor and renders the root component. It defines the HTML shell and loads scripts/styles for the UI.

### `roles/ui/files/FWO.UI/Pages/_Imports.razor`
Page-level Razor imports for the Pages folder. It keeps shared namespaces and components available to top-level pages.

## Authentication and services

### `roles/ui/files/FWO.UI/Auth/AuthStateProvider.cs`
Authentication state provider that logs in via middleware, validates JWTs, and builds user claims. It stores tokens in session storage and updates UI state on login/logout and password changes.

### `roles/ui/files/FWO.UI/Services/JwtEventService.cs`
Static event hub for JWT expiry and permission change notifications. It manages timers per user and emits events before and after token expiry.

### `roles/ui/files/FWO.UI/Services/DomEventService.cs`
JS interop service that wires global DOM events like scroll, click, resize, and navbar height changes. It exposes .NET events so components can react to browser interactions.

### `roles/ui/files/FWO.UI/Services/CircuitHandlerService.cs`
Circuit handler that tracks Blazor circuit closures. It logs audit information and publishes a user-session-closed event.

### `roles/ui/files/FWO.UI/Services/ActionFilter.cs`
Action filter that sanitizes URL arguments before controller actions run. It uses the URL sanitizer to strip or block unsafe values.

### `roles/ui/files/FWO.UI/Services/PasswordPolicy.cs`
Password policy validator for UI password changes. It checks configured length and character requirements and returns localized error messages.

### `roles/ui/files/FWO.UI/Services/FileUploadService.cs`
Service helper for handling file uploads from the UI. It coordinates client-side selection and server-side upload requests for import workflows.

### `roles/ui/files/FWO.UI/Services/ModellingAppHandler.cs`
Helper service for modelling application actions and related API calls. It centralizes modelling app workflow logic used across pages.

### `roles/ui/files/FWO.UI/Services/UrlSanizerMiddleware.cs`
HTTP middleware that sanitizes incoming request URLs. It blocks unsafe URLs early and returns a 400 response on invalid input.

### `roles/ui/files/FWO.UI/Services/RoleAccess.cs`
Centralizes role-based access checks for the UI. It helps components hide or disable actions based on the active user roles.

### `roles/ui/files/FWO.UI/Services/UrlSanitizer.cs`
Implements URL sanitization, decoding, and allowlist checks. It is used to validate navigation targets and help links.

### `roles/ui/files/FWO.UI/Services/IUrlSanitizer.cs`
Interface for URL sanitization services. It enables DI and testable sanitization behavior.

### `roles/ui/files/FWO.UI/Services/KeyboardInputService.cs`
Service for global keyboard shortcuts and key events. It lets components subscribe to key combinations and manages JS interop hooks.

### `roles/ui/files/FWO.UI/Services/PasswordChanger.cs`
Helper to validate and submit password changes. It coordinates policy checks and API calls for password updates.

### `roles/ui/files/FWO.UI/Services/DisplayService.cs`
Utility service for UI display formatting and shared presentation helpers. It provides small formatting utilities used across components.

## Data models

### `roles/ui/files/FWO.UI/Data/PopupSize.cs`
Defines popup sizing options for dialogs. Used by shared popup components to standardize dimensions.

### `roles/ui/files/FWO.UI/Data/NavItem.cs`
Represents a navigation entry with title, route, and optional children. Used by navigation menus and sidebars.

### `roles/ui/files/FWO.UI/Data/FileUploadCase.cs`
Enum or model describing upload contexts. Used to branch UI behavior for different upload scenarios.

### `roles/ui/files/FWO.UI/Data/UIMessage.cs`
Represents a UI message or notification payload. Used by monitoring and alert displays to render messages consistently.

### `roles/ui/files/FWO.UI/Data/OrderMode.cs`
Enum describing ordering direction or mode. Used by sorting controls and table components.

### `roles/ui/files/FWO.UI/Data/CollapseState.cs`
Enum describing collapse or expand UI state. Used by expandable components to track visibility.

### `roles/ui/files/FWO.UI/Data/SpinnerSize.cs`
Enum describing spinner size variants. Used by loading indicators and progress components.

## Shared layouts and components

### `roles/ui/files/FWO.UI/Shared/_Imports.razor`
Shared imports for reusable components. Keeps common namespaces and component references available within Shared.

### `roles/ui/files/FWO.UI/Shared/MainLayout.razor`
Primary application layout that hosts navigation and content areas. It wires global UI structure used by most pages.

### `roles/ui/files/FWO.UI/Shared/EmptyLayout.razor`
Minimal layout for pages that require no navigation shell. Used for standalone screens like login or errors.

### `roles/ui/files/FWO.UI/Shared/SettingsLayout.razor`
Layout wrapper for settings pages. It provides consistent structure and navigation within the settings area.

### `roles/ui/files/FWO.UI/Shared/RequestLayout.razor`
Layout wrapper for workflow request pages. It standardizes header/side panels for request flows.

### `roles/ui/files/FWO.UI/Shared/ReportLayout.razor`
Layout wrapper for reporting pages. It organizes report-specific navigation, filters, and content areas.

### `roles/ui/files/FWO.UI/Shared/MonitoringLayout.razor`
Layout wrapper for monitoring pages. It provides a consistent shell for monitoring views and navigation.

### `roles/ui/files/FWO.UI/Shared/NavigationMenu.razor`
Main navigation menu component. It renders the primary app navigation structure and links.

### `roles/ui/files/FWO.UI/Shared/Sidebar.razor`
Left sidebar component used across layouts. It hosts navigation lists and section links.

### `roles/ui/files/FWO.UI/Shared/RightSidebar.razor`
Right sidebar container for contextual panels. It renders expandable panels used by reporting and modelling pages.

### `roles/ui/files/FWO.UI/Shared/AnchorNavToRSB.razor`
Anchor navigation component that integrates with the right sidebar. It provides quick in-page jumps to sidebar content.

### `roles/ui/files/FWO.UI/Shared/HelpLink.razor`
Shared help-link component for contextual help access. It builds links into the help system for the current page.

### `roles/ui/files/FWO.UI/Shared/ContentSwap.razor`
Utility component for swapping visible content panels. It handles conditional rendering of alternative UI sections.

### `roles/ui/files/FWO.UI/Shared/TabSet.razor`
Tab container component for multi-tab views. It manages active tab state and renders tab content.

### `roles/ui/files/FWO.UI/Shared/Tab.razor`
Single tab component used within TabSet. It provides tab headers and selection handling.

### `roles/ui/files/FWO.UI/Shared/ReportTabset.razor`
Tabbed layout specialized for report views. It coordinates report sections and report-specific tabs.

### `roles/ui/files/FWO.UI/Shared/Detail.razor`
Detail panel component for displaying object details. It standardizes presentation for object metadata and fields.

### `roles/ui/files/FWO.UI/Shared/Loading.razor`
Loading indicator component for async operations. It provides consistent spinners and optional messages.

### `roles/ui/files/FWO.UI/Shared/InProgress.razor`
Inline progress indicator for ongoing tasks. It complements Loading with lighter-weight progress UI.

### `roles/ui/files/FWO.UI/Shared/Exporting.razor`
Export state indicator for report or data exports. It shows export progress and completion state.

### `roles/ui/files/FWO.UI/Shared/PopUp.razor`
Reusable popup dialog component. It provides modal framing and supports custom body content.

### `roles/ui/files/FWO.UI/Shared/Confirm.razor`
Confirmation dialog component for generic confirmations. It captures user consent before critical actions.

### `roles/ui/files/FWO.UI/Shared/ConfirmDelete.razor`
Specialized confirmation dialog for destructive delete actions. It provides delete messaging and confirmation controls.

### `roles/ui/files/FWO.UI/Shared/Collapse.razor`
Collapsible panel component. It manages expand/collapse state for grouped content.

### `roles/ui/files/FWO.UI/Shared/ExpandableList.razor`
Expandable list component for grouped items. It supports nested list items and toggled visibility.

### `roles/ui/files/FWO.UI/Shared/ExpandableList2.razor`
Variant expandable list component with alternate layout behavior. It provides a second implementation for list expansion needs.

### `roles/ui/files/FWO.UI/Shared/DraggableList.razor`
Reusable list component that supports drag-and-drop reordering. It surfaces item movement events for parent components.

### `roles/ui/files/FWO.UI/Shared/EditList.razor`
Editable list component for adding, removing, and reordering items. It provides inline editing affordances for list data.

### `roles/ui/files/FWO.UI/Shared/Dropdown.razor`
Generic dropdown selector component. It renders selectable options with a consistent UI style.

### `roles/ui/files/FWO.UI/Shared/OrderByDropdown.razor`
Dropdown component for choosing sort order. It pairs with OrderMode to control list or table sorting.

### `roles/ui/files/FWO.UI/Shared/PageSizeComponent.razor`
Page-size selector component for paginated lists. It exposes page size choices and emits selection events.

### `roles/ui/files/FWO.UI/Shared/ConnectionTable.razor`
Table component for displaying connection data. It renders connection rows with consistent formatting and actions.

### `roles/ui/files/FWO.UI/Shared/VarianceStatisticsTable.razor`
Table component for variance statistics. It provides a standardized layout for variance metrics and summaries.

### `roles/ui/files/FWO.UI/Shared/AppRoleTable.razor`
Table component for application role assignments. It displays app-role relationships and related metadata.

### `roles/ui/files/FWO.UI/Shared/ObjectGroup.razor`
Component for rendering a group of objects. It encapsulates display and interaction for grouped items.

### `roles/ui/files/FWO.UI/Shared/ObjectGroupCollection.razor`
Component for managing multiple object groups. It handles grouping UI and renders nested group lists.

### `roles/ui/files/FWO.UI/Shared/RuleSelector.razor`
Selector component for choosing firewall rules. It provides filtering and selection UI for rule lists.

### `roles/ui/files/FWO.UI/Shared/ServiceSelector.razor`
Selector component for choosing services. It provides service lookup and selection UI for report filters.

### `roles/ui/files/FWO.UI/Shared/IpSelector.razor`
Selector component for entering IP values. It supports add/remove interactions and validation for IP inputs.

### `roles/ui/files/FWO.UI/Shared/IpAddressInput.razor`
Input component for single IP address entry. It enforces IP formatting and validation rules.

### `roles/ui/files/FWO.UI/Shared/PortRangeInput.razor`
Input component for port range values. It validates port ranges and integrates with rule filters.

### `roles/ui/files/FWO.UI/Shared/DeviceSelection.razor`
Component for selecting devices. It presents device lists and captures selection state for filters.

### `roles/ui/files/FWO.UI/Shared/DeviceSelectionTenants.razor`
Device selection component scoped to tenants. It filters device lists based on tenant visibility rules.

### `roles/ui/files/FWO.UI/Shared/ManagementSelection.razor`
Component for selecting managements. It provides consistent selection UI for management filters.

### `roles/ui/files/FWO.UI/Shared/SelectOwner.razor`
Owner selection component for modelling and recertification flows. It handles owner lookup and selection state.

### `roles/ui/files/FWO.UI/Shared/KeyboardInput.razor`
Component wrapper for registering keyboard shortcuts. It integrates with KeyboardInputService to handle key events.

### `roles/ui/files/FWO.UI/Shared/Tooltip.razor`
Reusable tooltip component. It provides hover or focus hints across the UI.

### `roles/ui/files/FWO.UI/Shared/AutoDiscovery.razor`
Component for triggering or displaying autodiscovery actions. It exposes UI hooks for discovery workflows.

### `roles/ui/files/FWO.UI/Shared/ImportFileUpload.razor`
File upload component for import workflows. It handles file selection and passes upload data to services.

### `roles/ui/files/FWO.UI/Shared/ImportDetails.razor`
Component for displaying import details and status. It renders import metadata and results in a consistent format.

### `roles/ui/files/FWO.UI/Shared/ImportRollback.razor`
Component for managing import rollback actions. It provides UI controls and status for rollback operations.

### `roles/ui/files/FWO.UI/Shared/CustomLogoUpload.razor`
Component for uploading a custom logo. It handles file selection and updates UI branding preview.

### `roles/ui/files/FWO.UI/Shared/EditNotifications.razor`
Component for editing notification settings. It provides UI for recipients, timing, and notification configuration.

## Top-level pages

### `roles/ui/files/FWO.UI/Pages/Start.razor`
Landing page for authenticated users. It provides the initial navigation and overview entry point.

### `roles/ui/files/FWO.UI/Pages/Login.razor`
Login page for user authentication. It captures credentials, handles errors, and triggers authentication flow.

### `roles/ui/files/FWO.UI/Pages/Logout.razor`
Logout page that clears authentication state. It resets session data and redirects users after sign-out.

### `roles/ui/files/FWO.UI/Pages/Error.razor`
Error page shown on unhandled failures. It renders a user-friendly error message and recovery guidance.

### `roles/ui/files/FWO.UI/Pages/NetworkAnalysis.razor`
Page for network analysis workflows. It hosts UI for exploring network paths or analysis results.

### `roles/ui/files/FWO.UI/Pages/Certification.razor`
Recertification overview page. It provides entry points to recertification tasks and reports.

## Reporting pages

### `roles/ui/files/FWO.UI/Pages/Reporting/_Imports.razor`
Reporting-area imports for shared namespaces and components. It keeps common using directives available to reporting pages.

### `roles/ui/files/FWO.UI/Pages/Reporting/Report.razor`
Main reporting page wrapper. It coordinates report selection, filters, and content rendering.

### `roles/ui/files/FWO.UI/Pages/Reporting/Archive.razor`
Report archive page for previously generated reports. It lists stored reports and supports download actions.

### `roles/ui/files/FWO.UI/Pages/Reporting/Schedule.razor`
Report scheduling page for periodic report generation. It manages schedule definitions and output options.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportDownloadPopUp.razor`
Popup dialog for downloading report files. It exposes format options and triggers file retrieval.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportExport.razor`
Export page for report output selection. It provides format and delivery controls for reports.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportSelectTime.razor`
Time selection step for report generation. It captures date ranges and time filters for reports.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportTemplateComponent.razor`
Reusable report template component. It renders a selected template and wires report parameter inputs.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportTemplateSelectDevice.razor`
Device selection step for report templates. It captures device filters and selection scopes.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportTenantSelection.razor`
Tenant selection step for reports. It filters report scope by selected tenants.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportModellingParamSelection.razor`
Parameter selection page for modelling reports. It captures modelling-specific filters and owners.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportRecertParamSelection.razor`
Parameter selection page for recertification reports. It captures recertification filters and timing.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportCreateTicket.razor`
UI flow for creating tickets from report findings. It captures ticket metadata and triggers workflow actions.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportedRules.razor`
Report view for rule listings. It renders rules and related metadata for reporting output.

### `roles/ui/files/FWO.UI/Pages/Reporting/ReportedRulesForDiff.razor`
Report view for rule diffs. It compares rule sets and highlights differences in report output.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/_Imports.razor`
Imports for report-rendering components. It keeps shared namespaces available for report views.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/RuleBaseReport.razor`
Base component for rule-based report rendering. It defines common table structure and shared actions.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/RulesReport.razor`
Rules report view implementation. It renders rule lists with filters, counts, and export actions.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/ChangesReport.razor`
Changes report view implementation. It renders change history data and supports export formats.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/StatisticsReport.razor`
Statistics report view implementation. It presents aggregated metrics and summary tables.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/RecertEventReport.razor`
Recertification events report view. It lists recert events and associated metadata.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/ConnectionsReport.razor`
Connections report view implementation. It renders connection analysis data with filters and export tools.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/OwnerRecertReport.razor`
Owner recertification report view. It displays owner-specific recertification data and actions.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/OwnerRecertTable.razor`
Table component for owner recertification details. It provides a focused table view of owner recert data.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/VariancesReport.razor`
Variance analysis report view. It renders variance findings and related metrics.

### `roles/ui/files/FWO.UI/Pages/Reporting/Reports/ComplianceReport.razor`
Compliance report view implementation. It displays compliance check results and export options.

## Compliance pages

### `roles/ui/files/FWO.UI/Pages/Compliance/_Imports.razor`
Imports for the compliance area. It provides shared namespaces and components to compliance pages.

### `roles/ui/files/FWO.UI/Pages/Compliance/ComplianceLayout.razor`
Layout wrapper for compliance pages. It provides navigation and shared layout elements for compliance workflows.

### `roles/ui/files/FWO.UI/Pages/Compliance/ComplianceMatrix.razor`
Compliance matrix overview page. It lists available matrices and provides navigation to matrix details.

### `roles/ui/files/FWO.UI/Pages/Compliance/AddMatrix.razor`
Page for adding a compliance matrix. It captures matrix metadata and upload/import data.

### `roles/ui/files/FWO.UI/Pages/Compliance/CompliancePolicies.razor`
Compliance policy list page. It presents policies and provides navigation to edit or review them.

### `roles/ui/files/FWO.UI/Pages/Compliance/EditPolicy.razor`
Policy edit page for compliance rules. It provides editors for policy fields and assignments.

### `roles/ui/files/FWO.UI/Pages/Compliance/ComplianceChecks.razor`
Compliance checks page for running or viewing checks. It shows check status and related results.

### `roles/ui/files/FWO.UI/Pages/Compliance/ZonesConfiguration.razor`
Network zone configuration page. It manages zone definitions and related settings.

### `roles/ui/files/FWO.UI/Pages/Compliance/ZonesMatrix.razor`
Matrix editor for network zones. It renders the zone-to-zone matrix and editing actions.

### `roles/ui/files/FWO.UI/Pages/Compliance/ZonesChecks.razor`
Zone checks page for compliance validation. It displays zone-related check results and filters.

### `roles/ui/files/FWO.UI/Pages/Compliance/ZoneTable.razor`
Table component for network zones. It renders zone metadata and related actions in a grid view.

## Monitoring pages

### `roles/ui/files/FWO.UI/Pages/Monitoring/_Imports.razor`
Imports for the monitoring area. It provides shared namespaces and components to monitoring pages.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitoringMain.razor`
Main monitoring landing page. It links to the various monitoring dashboards.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorAll.razor`
Unified monitoring view for multiple subsystems. It aggregates key monitoring panels in one page.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorAlerts.razor`
Monitoring view for alerts. It lists active and historical alerts with filters.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorAutodiscoveryLog.razor`
Monitoring view for autodiscovery logs. It displays autodiscovery events and results.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorDailyChecks.razor`
Monitoring view for daily check results. It lists daily check outcomes and related logs.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorExternalRequests.razor`
Monitoring view for external request processing. It displays request statuses and workflow details.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorExternalRequestTickets.razor`
Monitoring view for external request tickets. It focuses on ticket-level status and metadata.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorImportLog.razor`
Monitoring view for import log entries. It renders import log history and filters.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorImportStatus.razor`
Monitoring view for import status. It provides current status and last-run information per import.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorAreaIpDataImportLog.razor`
Monitoring view for area IP data import logs. It displays import results and errors for subnet data.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorAppDataImportLog.razor`
Monitoring view for app data import logs. It shows application import outcomes and errors.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorModelling.razor`
Monitoring view for modelling jobs. It surfaces modelling-related status and log entries.

### `roles/ui/files/FWO.UI/Pages/Monitoring/MonitorUiLog.razor`
Monitoring view for UI log entries. It displays client-side or UI-originated log messages.

## Request workflow pages

### `roles/ui/files/FWO.UI/Pages/Request/_Imports.razor`
Imports for request workflow pages. It keeps shared namespaces and components available in the request area.

### `roles/ui/files/FWO.UI/Pages/Request/RequestTickets.razor`
Page listing request tickets. It provides filters and navigation to ticket detail views.

### `roles/ui/files/FWO.UI/Pages/Request/RequestTicketsOverview.razor`
Overview page for request tickets. It summarizes ticket state across workflow phases.

### `roles/ui/files/FWO.UI/Pages/Request/RequestApprovals.razor`
Approval queue page for request workflows. It presents tasks awaiting approval and actions.

### `roles/ui/files/FWO.UI/Pages/Request/RequestPlannings.razor`
Planning queue page for request workflows. It lists planning tasks and related ticket context.

### `roles/ui/files/FWO.UI/Pages/Request/RequestImplementations.razor`
Implementation queue page for request workflows. It surfaces implementation tasks and their status.

### `roles/ui/files/FWO.UI/Pages/Request/RequestReviews.razor`
Review queue page for request workflows. It displays review tasks and associated ticket context.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayTicket.razor`
Ticket detail page. It shows ticket metadata, tasks, and workflow state.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayTicketTable.razor`
Table component for ticket lists. It renders ticket rows with sortable columns and actions.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayRequestTask.razor`
Task detail view for request tasks. It displays task data and supports task-level actions.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayReqTaskTable.razor`
Table component for request tasks. It lists tasks with status, ownership, and actions.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayImplementationTask.razor`
Task detail view for implementation tasks. It shows implementation-specific fields and actions.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayImplTaskTable.razor`
Table component for implementation tasks. It lists implementation tasks and related ticket context.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayApprovals.razor`
Page for displaying approval decisions. It shows approval details and decision history.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayRules.razor`
View for showing rule changes tied to a request. It renders rule lists and comparison details.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayAccessElements.razor`
View for access elements within a request. It displays source/destination/service elements tied to a workflow.

### `roles/ui/files/FWO.UI/Pages/Request/DisplayPathAnalysis.razor`
View for path analysis results in a request context. It renders analysis output and related metadata.

### `roles/ui/files/FWO.UI/Pages/Request/AssignObject.razor`
UI for assigning objects within a workflow. It captures assignment choices and applies them to tasks.

### `roles/ui/files/FWO.UI/Pages/Request/PromoteObject.razor`
UI for promoting objects within a workflow. It guides the promote action and captures confirmation.

### `roles/ui/files/FWO.UI/Pages/Request/DeleteObject.razor`
UI for deleting objects within a workflow. It confirms deletion and triggers removal actions.

### `roles/ui/files/FWO.UI/Pages/Request/CommentObject.razor`
UI for adding comments to workflow objects. It captures comment text and persists it.

### `roles/ui/files/FWO.UI/Pages/Request/ImplOptSelection.razor`
UI for selecting implementation options. It captures implementation choices for request handling.

## Network modelling pages

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/_Imports.razor`
Imports for network modelling pages. It keeps shared namespaces and components available to modelling views.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/NetworkModelling.razor`
Main network modelling workspace. It coordinates the modelling UI, filters, and action panels.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/SearchNwObject.razor`
Search UI for network objects. It provides lookup and selection of modelling objects.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/SearchInterface.razor`
Search UI for interfaces. It provides lookup and selection for interface objects.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/PredefServices.razor`
Page for predefined service definitions. It displays and manages predefined service lists.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditService.razor`
Service editor page for modelling services. It captures service properties and saves updates.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditServiceGroup.razor`
Service group editor page. It manages group membership and group metadata.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditServiceGroupLeftSide.razor`
Left-side panel for service group editing. It provides navigation and context for service group edits.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditAppRole.razor`
Editor page for application roles. It manages role metadata and assigned users/groups.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditAppRoleLeftSide.razor`
Left-side panel for app role editing. It provides context and navigation for app role changes.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditAppServer.razor`
Editor page for application servers. It manages app server details and IP information.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/ManualAppServer.razor`
Manual app server entry UI. It captures app server data when auto-import is not used.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditConn.razor`
Editor page for modelling connections. It handles connection properties and related rule logic.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditConnLeftSide.razor`
Left-side panel for connection editing. It provides contextual navigation for connection details.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/EditConnPopup.razor`
Popup editor for connection updates. It supports quick edits without leaving the main view.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/AddExtraConfig.razor`
UI for adding extra configuration to modelling objects. It captures additional metadata and options.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/RequestInterfacePopup.razor`
Popup UI for requesting interfaces. It collects request details and triggers workflow actions.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/RequestFwChangePopup.razor`
Popup UI for requesting firewall changes. It captures change details and initiates requests.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/RequestRecertPopup.razor`
Popup UI for requesting recertification. It captures request details and triggers recert workflows.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/RejectInterfacePopup.razor`
Popup UI for rejecting interface requests. It captures rejection reasons and applies status updates.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/DecommissionInterfacePopup.razor`
Popup UI for decommissioning interfaces. It confirms decommission actions and updates status.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/InterfaceUsersPopup.razor`
Popup UI for viewing interface users. It displays users and related assignments for an interface.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/ShareLink.razor`
UI for sharing modelling links. It generates shareable URLs and provides copy actions.

### `roles/ui/files/FWO.UI/Pages/NetworkModelling/ShowHistory.razor`
History view for modelling objects. It displays change history and audit information.

## Settings pages

### `roles/ui/files/FWO.UI/Pages/Settings/_Imports.razor`
Imports for settings pages. It provides shared namespaces and components to the settings area.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsMain.razor`
Main settings landing page. It links to the various settings sections.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsDefaults.razor`
Settings page for default values. It manages system defaults and global options.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsCustomTexts.razor`
Settings page for custom UI texts. It allows editing localized or customized strings.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsCustomizing.razor`
Settings page for customization options. It manages UI branding and customization settings.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsLanguage.razor`
Settings page for language preferences. It lets admins configure available UI languages.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsPassword.razor`
Settings page for password configuration. It exposes password rules and related options.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsPasswordPolicy.razor`
Settings page for password policy rules. It manages complexity requirements and validation settings.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsEmail.razor`
Settings page for email configuration. It captures mail server settings and test options.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsLdap.razor`
Settings page for LDAP configuration. It manages LDAP connections and synchronization options.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsGroups.razor`
Settings page for group management. It manages LDAP or internal groups and assignments.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsUsers.razor`
Settings page for user management. It lists users and supports create/update/remove workflows.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsUser.razor`
User detail editor page. It edits individual user metadata and settings.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsRoles.razor`
Settings page for role management. It assigns roles and configures role behavior.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsTenants.razor`
Settings page for tenant management. It manages tenants and tenant-specific visibility rules.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsOwner.razor`
Settings page for owner management. It manages owners and owner-related configuration.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsManagements.razor`
Settings page for management configuration. It manages firewall manager connections and metadata.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsGateways.razor`
Settings page for gateway configuration. It lists gateways and manages gateway metadata.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsCredentials.razor`
Settings page for credentials. It manages import or device credentials and secret handling.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsImport.razor`
Settings page for import configuration. It controls import scheduling and paths.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsCompliance.razor`
Settings page for compliance configuration. It manages compliance-related options and thresholds.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsReport.razor`
Settings page for report configuration. It manages report templates and report defaults.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsReportGeneral.razor`
Settings page for general report options. It adjusts global report behavior and defaults.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsRecertificationGen.razor`
Settings page for recertification generation. It configures recertification schedules and parameters.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsRecertificationPers.razor`
Settings page for recertification personalization. It manages per-owner recert settings.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsModelling.razor`
Settings page for modelling defaults. It configures modelling behavior and conventions.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsModellingPers.razor`
Settings page for modelling personalization. It configures user-specific modelling preferences.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsExternalWorkflow.razor`
Settings page for external workflow integration. It configures external ticketing and workflow options.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsActions.razor`
Settings page for workflow actions. It manages available actions and related configuration.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsStateMatrix.razor`
Settings page for workflow state matrices. It manages state transitions and permissions.

### `roles/ui/files/FWO.UI/Pages/Settings/SettingsStates.razor`
Settings page for workflow states. It manages state definitions and metadata.

### `roles/ui/files/FWO.UI/Pages/Settings/ComplianceFixCriteria.razor`
Settings page for compliance fix criteria. It manages criteria used for compliance recommendations.

### `roles/ui/files/FWO.UI/Pages/Settings/EditFixCriterion.razor`
Editor page for a single compliance fix criterion. It edits criterion fields and conditions.

### `roles/ui/files/FWO.UI/Pages/Settings/VarianceOptionsSelection.razor`
Settings page for variance analysis options. It configures variance calculation and display settings.

### `roles/ui/files/FWO.UI/Pages/Settings/ExtTicketTemplates.razor`
Settings page for external ticket templates. It manages templates used for external system tickets.

### `roles/ui/files/FWO.UI/Pages/Settings/SelectFromLdap.razor`
UI for selecting users or groups from LDAP. It provides search and selection for LDAP entities.

### `roles/ui/files/FWO.UI/Pages/Settings/SearchUser.razor`
User search page for settings workflows. It provides lookup and selection for user records.

### `roles/ui/files/FWO.UI/Pages/Settings/RemoveUser.razor`
UI for removing users. It confirms deletion and handles removal actions.

### `roles/ui/files/FWO.UI/Pages/Settings/EditExtStates.razor`
Editor page for external workflow states. It manages mapping and metadata for external states.

### `roles/ui/files/FWO.UI/Pages/Settings/CommonAreaSelection.razor`
Shared selection UI for common areas in settings. It provides area selection for related configuration.

## Help pages

### `roles/ui/files/FWO.UI/Pages/Help/HelpLayout.cshtml`
Layout page for the help section. It provides the shared help shell and content placeholders.

### `roles/ui/files/FWO.UI/Pages/Help/Index.cshtml`
Help index page. It provides the entry point to help topics and navigation.

### `roles/ui/files/FWO.UI/Pages/Help/Index.cshtml.cs`
Code-behind for the help index page. It handles server-side logic or model binding for help landing content.

### `roles/ui/files/FWO.UI/Pages/Help/HelpArchitechture.cshtml`
Help page describing system architecture. It provides guidance content for the architecture overview.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApi.cshtml`
Help page introducing the API. It provides guidance content for API usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiLogin.cshtml`
Help page for API login. It provides guidance content for authenticating API users.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiLogout.cshtml`
Help page for API logout. It provides guidance content for ending API sessions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiUserAuth.cshtml`
Help page for API user authentication. It provides guidance content on auth flows and credentials.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiSecurity.cshtml`
Help page for API security guidance. It outlines security considerations and best practices.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiReporting.cshtml`
Help page for API reporting usage. It explains report-related API endpoints and usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiFwoQuery.cshtml`
Help page for FWO query endpoints. It documents query usage and parameters.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiFwoMutation.cshtml`
Help page for FWO mutation endpoints. It documents mutation usage and payloads.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiFwoLinks.cshtml`
Help page for FWO API links. It lists reference links and integration resources.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiFwoGraphql.cshtml`
Help page for FWO GraphQL usage. It explains GraphQL entry points and usage patterns.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiFwoHasura.cshtml`
Help page for Hasura-related API integration. It outlines Hasura-specific access details.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiSidebar.cshtml`
Help page for API sidebar navigation. It explains the API help navigation structure.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiAppDataImport.cshtml`
Help page for app data import via API. It provides guidance content for import operations.

### `roles/ui/files/FWO.UI/Pages/Help/HelpApiSubnetDataImport.cshtml`
Help page for subnet data import via API. It explains subnet import steps and parameters.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReporting.cshtml`
Help page for reporting features. It provides guidance content for report workflows.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingTemplates.cshtml`
Help page for report templates. It explains template creation and usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingArchive.cshtml`
Help page for report archive features. It provides guidance on browsing and downloading archived reports.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingTypes.cshtml`
Help page for report types. It describes available report categories and when to use them.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingFilter.cshtml`
Help page for report filtering. It explains filter options and search behavior.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingExport.cshtml`
Help page for report export options. It explains export formats and delivery settings.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingScheduling.cshtml`
Help page for report scheduling. It describes scheduling options and recurrence rules.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingDataOutput.cshtml`
Help page for report data output. It explains data output formats and content structure.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingLeftSidebar.cshtml`
Help page for the reporting left sidebar. It documents the navigation and controls in that sidebar.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingRightSidebar.cshtml`
Help page for the reporting right sidebar. It documents right sidebar tools and panels.

### `roles/ui/files/FWO.UI/Pages/Help/HelpReportingSidebar.cshtml`
Help page for reporting sidebar navigation. It explains sidebar sections and links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoring.cshtml`
Help page for monitoring features. It provides guidance for monitoring dashboards and logs.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringAllAlerts.cshtml`
Help page for monitoring alerts. It describes alert lists and alert handling workflows.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringOpenAlerts.cshtml`
Help page for open alerts. It explains how to interpret and resolve open alerts.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringDailyChecks.cshtml`
Help page for daily check monitoring. It explains the daily check dashboard and logs.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringImportlogs.cshtml`
Help page for import logs. It describes import log entries and troubleshooting tips.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringImportStatus.cshtml`
Help page for import status monitoring. It explains status indicators and expected states.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringAutodiscovery.cshtml`
Help page for autodiscovery monitoring. It describes autodiscovery results and alerts.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringExternalRequests.cshtml`
Help page for monitoring external requests. It explains request state tracking and resolution.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringExternalRequestTickets.cshtml`
Help page for external request tickets. It explains ticket details and statuses.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringUiMessages.cshtml`
Help page for UI messages monitoring. It explains UI message types and visibility.

### `roles/ui/files/FWO.UI/Pages/Help/HelpMonitoringSidebar.cshtml`
Help page for monitoring sidebar navigation. It documents sidebar sections and links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettings.cshtml`
Help page for settings overview. It provides guidance for the settings area.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsSidebar.cshtml`
Help page for settings sidebar navigation. It documents the settings navigation structure.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsDefaults.cshtml`
Help page for settings defaults. It describes default configuration options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsCustomizing.cshtml`
Help page for customization settings. It describes branding and customization options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsCustomTexts.cshtml`
Help page for custom text settings. It explains where and how to configure custom texts.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsLanguage.cshtml`
Help page for language settings. It explains language selection and localization behavior.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsPassword.cshtml`
Help page for password settings. It explains password options and requirements.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsPasswordPolicy.cshtml`
Help page for password policy settings. It describes complexity rules and policy enforcement.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsEmail.cshtml`
Help page for email settings. It documents email server configuration and test options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsLdap.cshtml`
Help page for LDAP settings. It explains LDAP configuration and connection management.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsUsers.cshtml`
Help page for user settings. It describes user management workflows.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsOwners.cshtml`
Help page for owner settings. It explains owner management and ownership links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsGroups.cshtml`
Help page for group settings. It covers group management and assignments.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsRoles.cshtml`
Help page for role settings. It describes role management and permissions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsTenants.cshtml`
Help page for tenant settings. It covers tenant management and visibility.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsManagements.cshtml`
Help page for management settings. It explains management connections and metadata.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsGateways.cshtml`
Help page for gateway settings. It documents gateway configuration and monitoring.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsCredentials.cshtml`
Help page for credential settings. It describes credential management and usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsImporter.cshtml`
Help page for importer settings. It explains importer configuration and schedules.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsReport.cshtml`
Help page for report settings. It describes report configuration and templates.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsReportGen.cshtml`
Help page for report generation settings. It explains report generation options and defaults.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsCompliance.cshtml`
Help page for compliance settings. It explains compliance configuration options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsStateDefinitions.cshtml`
Help page for state definitions. It explains workflow states and definitions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsStateMatrix.cshtml`
Help page for state matrix settings. It explains state transitions and matrix configuration.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsStateActions.cshtml`
Help page for state actions. It describes actions tied to workflow states.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsExternalWorkflow.cshtml`
Help page for external workflow settings. It explains external system integration options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsRecertificationGen.cshtml`
Help page for recertification generation settings. It explains schedule and parameter options.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsRecertificationPers.cshtml`
Help page for recertification personalization settings. It describes per-owner recert configuration.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsModelling.cshtml`
Help page for modelling settings. It describes modelling configuration and conventions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsModellingPers.cshtml`
Help page for modelling personalization settings. It explains per-user modelling preferences.

### `roles/ui/files/FWO.UI/Pages/Help/HelpSettingsOwnerLifecylces.cshtml`
Help page for owner lifecycles. It explains lifecycle settings and maintenance.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflow.cshtml`
Help page for workflow overview. It explains workflow concepts and the main phases.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowSidebar.cshtml`
Help page for workflow sidebar navigation. It documents workflow sidebar sections and links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowPhasesRoles.cshtml`
Help page for workflow phases and roles. It explains role responsibilities by phase.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowStates.cshtml`
Help page for workflow states. It explains state meanings and transitions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowTaskTypes.cshtml`
Help page for workflow task types. It documents task categories and their usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowActions.cshtml`
Help page for workflow actions. It explains available actions and their effects.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowObjects.cshtml`
Help page for workflow objects. It describes objects used across workflow tasks.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowChecklist.cshtml`
Help page for workflow checklists. It explains checklist items and usage.

### `roles/ui/files/FWO.UI/Pages/Help/HelpWorkflowExamples.cshtml`
Help page providing workflow examples. It walks through example workflows and outcomes.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModelling.cshtml`
Help page for modelling overview. It describes modelling goals and key workflows.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingSidebar.cshtml`
Help page for modelling sidebar navigation. It documents modelling sidebar sections and links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingApplications.cshtml`
Help page for modelling applications. It describes application objects and modelling actions.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingNetworkObjects.cshtml`
Help page for modelling network objects. It explains network object types and editing.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingConnections.cshtml`
Help page for modelling connections. It describes connection modelling and evaluation.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingServices.cshtml`
Help page for modelling services. It describes service modelling and grouping.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingWorkflow.cshtml`
Help page for modelling workflow integration. It describes how modelling ties into workflows.

### `roles/ui/files/FWO.UI/Pages/Help/HelpModellingRollout.cshtml`
Help page for modelling rollout options. It explains rollout strategies and settings.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertification.cshtml`
Help page for recertification overview. It explains recertification goals and timing.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertificationSidebar.cshtml`
Help page for the recertification sidebar. It documents recertification navigation links.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertLogic.cshtml`
Help page for recertification logic. It explains how recertification schedules are calculated.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertWorkflow.cshtml`
Help page for recertification workflow. It describes recertification workflow steps and roles.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertOwnerImport.cshtml`
Help page for recertification owner import. It explains how owner data is imported for recerts.

### `roles/ui/files/FWO.UI/Pages/Help/HelpRecertRequire.cshtml`
Help page for recertification requirements. It describes required inputs and prerequisites.

### `roles/ui/files/FWO.UI/Pages/Help/HelpEmptySidebar.cshtml`
Help page for empty sidebar states. It explains why a sidebar might be empty and what to do.
