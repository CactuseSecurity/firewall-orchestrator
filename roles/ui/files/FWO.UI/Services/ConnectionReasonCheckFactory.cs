using System.Text.RegularExpressions;
using FWO.Ai.Client;
using FWO.Ai.Client.ReasonChecks;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Builds the AI check of the functional reason of the connection, interface or interface usage edited in the
    /// connection editor.
    /// </summary>
    public static class ConnectionReasonCheckFactory
    {
        /// <summary>Separator of the elements of the source, destination and service lists.</summary>
        private const string kListSeparator = ", ";

        /// <summary>
        /// Name of a connection using an interface, given to the AI as context data: name of the connection and of the
        /// interface used. Not displayed in the UI.
        /// </summary>
        private const string kInterfaceUsageNameFormat = "{0} (uses interface {1})";

        private const int kRegexTimeoutMilliseconds = 100;

        /// <summary>
        /// A plain language name, as accepted by the reason check prompt for the language of the answer. Keep in step with
        /// the language pattern of FWO.Ai.Client.ReasonChecks.
        /// </summary>
        private static readonly Regex kLanguagePattern = new(@"\A\p{L}[\p{L} \-]*\z", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(kRegexTimeoutMilliseconds));

        /// <summary>
        /// Creates the prompt request checking the reason of the connection of the connection handler.
        /// </summary>
        /// <param name="connHandler">Handler of the edited connection</param>
        /// <param name="language">Language of the answer, e.g. the language of the user; left out if not a plain language name</param>
        /// <returns>The prompt request</returns>
        /// <exception cref="ArgumentException">The reason is blank or too long.</exception>
        public static AgentPromptRequest CreatePromptRequest(ModellingConnectionHandler connHandler, string? language)
        {
            return Create(connHandler, language).ToPromptRequest();
        }

        /// <summary>
        /// Creates the reason check of the connection of the connection handler: its reason (passed as entered, the prompt
        /// treats it as data), kind, name, app, and the sources, destinations and services as comma separated names. The
        /// elements are taken as the editor shows them, i.e. including the changes not saved yet.
        /// </summary>
        /// <param name="connHandler">Handler of the edited connection</param>
        /// <param name="language">Language of the answer, e.g. the language of the user; left out if not a plain language name</param>
        /// <returns>The reason check</returns>
        public static ConnectionReasonCheck Create(ModellingConnectionHandler connHandler, string? language)
        {
            ModellingConnection connection = connHandler.ActConn;
            ConnectionReasonKind kind = GetKind(connection);
            return new ConnectionReasonCheck
            {
                Reason = connection.Reason ?? "",
                Kind = kind,
                Name = GetName(connection, kind, connHandler.InterfaceName),
                App = connHandler.Application.Display(""),
                Source = JoinSources(connHandler),
                Destination = JoinDestinations(connHandler),
                Service = JoinServices(connHandler),
                Language = ToPlainLanguageName(language)
            };
        }

        /// <summary>
        /// Returns the language name trimmed if the reason check prompt accepts it: letters, blanks and hyphens, starting
        /// with a letter, and not too long. Any other value is left out, so that the AI answers in the language of the reason.
        /// </summary>
        /// <param name="language">Language name to check</param>
        /// <returns>The trimmed language name, or null if it is not accepted</returns>
        public static string? ToPlainLanguageName(string? language)
        {
            string trimmedLanguage = language?.Trim() ?? "";
            bool accepted = trimmedLanguage.Length > 0
                && trimmedLanguage.Length <= ReasonCheckLimits.kMaxLanguageLength
                && kLanguagePattern.IsMatch(trimmedLanguage);
            return accepted ? trimmedLanguage : null;
        }

        /// <summary>
        /// Returns the kind of the connection: interface, usage of an interface or regular connection.
        /// </summary>
        private static ConnectionReasonKind GetKind(ModellingConnection connection)
        {
            if (connection.IsInterface)
            {
                return ConnectionReasonKind.Interface;
            }
            return connection.UsedInterfaceId != null ? ConnectionReasonKind.InterfaceUsage : ConnectionReasonKind.Connection;
        }

        /// <summary>
        /// Returns the name of the connection, for an interface usage together with the name of the interface used.
        /// </summary>
        private static string GetName(ModellingConnection connection, ConnectionReasonKind kind, string interfaceName)
        {
            string name = connection.Name ?? "";
            if (kind == ConnectionReasonKind.InterfaceUsage && !string.IsNullOrWhiteSpace(interfaceName))
            {
                return string.Format(kInterfaceUsageNameFormat, name, interfaceName);
            }
            return name;
        }

        /// <summary>
        /// Joins the names of the sources as shown in the editor.
        /// </summary>
        private static string JoinSources(ModellingConnectionHandler connHandler)
        {
            ModellingConnection connection = connHandler.ActConn;
            List<ModellingAppServer> appServers = GetElementsInWork(connection.SourceAppServers.Select(wrapper => wrapper.Content),
                connHandler.SrcAppServerToDelete, connHandler.SrcAppServerToAdd, appServer => appServer.Id);
            List<ModellingAppRole> appRoles = GetElementsInWork(connection.SourceAppRoles.Select(wrapper => wrapper.Content),
                connHandler.SrcAppRolesToDelete, connHandler.SrcAppRolesToAdd, appRole => appRole.Id);
            List<ModellingNetworkArea> areas = GetElementsInWork(connection.SourceAreas.Select(wrapper => wrapper.Content),
                connHandler.SrcAreasToDelete, connHandler.SrcAreasToAdd, area => area.Id);
            List<ModellingNwGroup> otherGroups = GetElementsInWork(connection.SourceOtherGroups.Select(wrapper => wrapper.Content),
                connHandler.SrcNwGroupsToDelete, connHandler.SrcNwGroupsToAdd, nwGroup => nwGroup.Id);
            return JoinNetworkElements(appServers, appRoles, areas, otherGroups, connHandler.DummyAppRole.Id);
        }

        /// <summary>
        /// Joins the names of the destinations as shown in the editor.
        /// </summary>
        private static string JoinDestinations(ModellingConnectionHandler connHandler)
        {
            ModellingConnection connection = connHandler.ActConn;
            List<ModellingAppServer> appServers = GetElementsInWork(connection.DestinationAppServers.Select(wrapper => wrapper.Content),
                connHandler.DstAppServerToDelete, connHandler.DstAppServerToAdd, appServer => appServer.Id);
            List<ModellingAppRole> appRoles = GetElementsInWork(connection.DestinationAppRoles.Select(wrapper => wrapper.Content),
                connHandler.DstAppRolesToDelete, connHandler.DstAppRolesToAdd, appRole => appRole.Id);
            List<ModellingNetworkArea> areas = GetElementsInWork(connection.DestinationAreas.Select(wrapper => wrapper.Content),
                connHandler.DstAreasToDelete, connHandler.DstAreasToAdd, area => area.Id);
            List<ModellingNwGroup> otherGroups = GetElementsInWork(connection.DestinationOtherGroups.Select(wrapper => wrapper.Content),
                connHandler.DstNwGroupsToDelete, connHandler.DstNwGroupsToAdd, nwGroup => nwGroup.Id);
            return JoinNetworkElements(appServers, appRoles, areas, otherGroups, connHandler.DummyAppRole.Id);
        }

        /// <summary>
        /// Joins the names of the service groups and services as shown in the editor.
        /// </summary>
        private static string JoinServices(ModellingConnectionHandler connHandler)
        {
            ModellingConnection connection = connHandler.ActConn;
            List<ModellingServiceGroup> serviceGroups = GetElementsInWork(connection.ServiceGroups.Select(wrapper => wrapper.Content),
                connHandler.SvcGrpToDelete, connHandler.SvcGrpToAdd, serviceGroup => serviceGroup.Id);
            List<ModellingService> services = GetElementsInWork(connection.Services.Select(wrapper => wrapper.Content),
                connHandler.SvcToDelete, connHandler.SvcToAdd, service => service.Id);
            List<string> names = [];
            names.AddRange(serviceGroups.Select(serviceGroup => serviceGroup.Name));
            names.AddRange(services.Select(service => service.Display()));
            return string.Join(kListSeparator, names);
        }

        /// <summary>
        /// Returns the elements of one list of the editor: the saved elements without those marked for removal (matched by
        /// id, as on saving), followed by the elements added but not saved yet.
        /// </summary>
        /// <param name="savedElements">Elements of the connection</param>
        /// <param name="elementsToDelete">Elements marked for removal</param>
        /// <param name="elementsToAdd">Elements added but not saved yet</param>
        /// <param name="getId">Returns the id of an element</param>
        /// <returns>The elements as shown in the editor</returns>
        private static List<ElementType> GetElementsInWork<ElementType>(IEnumerable<ElementType> savedElements,
            List<ElementType> elementsToDelete, List<ElementType> elementsToAdd, Func<ElementType, long> getId)
        {
            HashSet<long> idsToDelete = elementsToDelete.Select(getId).ToHashSet();
            List<ElementType> elements = savedElements.Where(element => !idsToDelete.Contains(getId(element))).ToList();
            elements.AddRange(elementsToAdd);
            return elements;
        }

        /// <summary>
        /// Joins the names of the app servers, app roles (except the dummy app role), network areas and other groups.
        /// </summary>
        private static string JoinNetworkElements(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles,
            List<ModellingNetworkArea> areas, List<ModellingNwGroup> otherGroups, long dummyAppRoleId)
        {
            List<string> names = [];
            names.AddRange(appServers.Select(DisplayAppServer));
            names.AddRange(appRoles.Where(appRole => appRole.Id != dummyAppRoleId).Select(DisplayNwGroup));
            names.AddRange(areas.Select(DisplayNwGroup));
            names.AddRange(otherGroups.Select(DisplayNwGroup));
            return string.Join(kListSeparator, names);
        }

        /// <summary>
        /// Name and ip of an app server, without the markers of the UI for deleted or unused app servers.
        /// </summary>
        private static string DisplayAppServer(ModellingAppServer appServer)
        {
            return DisplayBase.DisplayIpWithName(ModellingAppServer.ToNetworkObject(appServer));
        }

        /// <summary>
        /// Name and id of an app role, network area or other group, without the marker of the UI for deleted groups.
        /// </summary>
        private static string DisplayNwGroup(ModellingNwGroup nwGroup)
        {
            string idString = nwGroup.IdString;
            return string.IsNullOrEmpty(idString) || idString == nwGroup.Name ? nwGroup.Name : $"{nwGroup.Name} ({idString})";
        }
    }
}
