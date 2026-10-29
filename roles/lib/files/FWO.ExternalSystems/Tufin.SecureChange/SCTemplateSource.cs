using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using System.Text.Json;

namespace FWO.ExternalSystems.Tufin.SecureChange
{
    /// <summary>
    /// The SecureChange ticket templates in effect for the global level and for managements, one template per task type.
    /// </summary>
    public sealed class SCTemplateLookup
    {
        private readonly List<ExternalTicketTemplate> globalTemplates;
        private readonly Dictionary<int, List<ExternalTicketTemplate>> templatesByManagement;

        /// <summary>Creates a lookup from resolved templates.</summary>
        /// <param name="globalTemplates">Templates of the global level, used for managements without own entry.</param>
        /// <param name="templatesByManagement">Templates per management id.</param>
        public SCTemplateLookup(List<ExternalTicketTemplate> globalTemplates, Dictionary<int, List<ExternalTicketTemplate>>? templatesByManagement = null)
        {
            this.globalTemplates = globalTemplates;
            this.templatesByManagement = templatesByManagement ?? [];
        }

        /// <summary>Returns the templates of a management; the global templates if the management is unknown.</summary>
        public List<ExternalTicketTemplate> For(int? managementId)
        {
            return managementId is int id && templatesByManagement.TryGetValue(id, out List<ExternalTicketTemplate>? templates)
                ? templates : globalTemplates;
        }

        /// <summary>Whether two managements use the same templates, so their request tasks can share one ticket.</summary>
        public bool HaveSameTemplates(int? managementId, int? otherManagementId)
        {
            List<ExternalTicketTemplate> templates = For(managementId);
            List<ExternalTicketTemplate> otherTemplates = For(otherManagementId);
            return ReferenceEquals(templates, otherTemplates) || JsonSerializer.Serialize(templates) == JsonSerializer.Serialize(otherTemplates);
        }

        /// <summary>
        /// Builds the templates of all task types from resolved settings. An empty template part takes the shipped default,
        /// or stays empty if none is shipped.
        /// </summary>
        public static List<ExternalTicketTemplate> FromSettings(GlobalDeviceSpecificSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            List<ExternalTicketTemplate> templates = [];
            foreach (SCTaskType taskType in Enum.GetValues<SCTaskType>())
            {
                ExternalTicketTemplate template = new() { TaskType = taskType.ToString() };
                foreach (SecureChangeTemplatePart part in SecureChangeTemplateSettingKeys.PartsOf(taskType))
                {
                    string value = (string)SecureChangeTemplateSettingKeys.Keys[(taskType, part)].DtoGetter!(settings);
                    SecureChangeTemplateSettingKeys.SetPart(template, part,
                        value.Length > 0 ? value : SCTemplateDefaults.Get(taskType, part) ?? "");
                }
                templates.Add(template);
            }
            return templates;
        }
    }

    /// <summary>Provides the SecureChange ticket templates in effect for managements.</summary>
    public interface ISCTemplateSource
    {
        /// <summary>Resolves the templates of the global level and of the given managements.</summary>
        /// <param name="managementIds">Managements of the request tasks; unknown ids get the global templates.</param>
        Task<SCTemplateLookup> Load(IReadOnlyCollection<int> managementIds);
    }

    /// <summary>
    /// Resolves the SecureChange ticket templates from the device specific settings along Global, device type and management.
    /// </summary>
    /// <remarks>
    /// Reads the device specific settings tables, so it needs the rights of the middleware server.
    /// </remarks>
    public sealed class ApiSCTemplateSource(ApiConnection apiConnection) : ISCTemplateSource
    {
        /// <inheritdoc/>
        public async Task<SCTemplateLookup> Load(IReadOnlyCollection<int> managementIds)
        {
            ArgumentNullException.ThrowIfNull(managementIds);
            List<Management> managements = await LoadManagements(managementIds);
            List<DeviceSpecificScopePath> paths = [DeviceSpecificScopePath.Global(), .. managements.Select(DeviceSpecificScopePath.ForManagement)];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);

            Dictionary<int, List<ExternalTicketTemplate>> templatesByManagement = [];
            for (int index = 0; index < managements.Count; index++)
            {
                templatesByManagement[managements[index].Id] = SCTemplateLookup.FromSettings(settings[index + 1]);
            }
            return new SCTemplateLookup(SCTemplateLookup.FromSettings(settings[0]), templatesByManagement);
        }

        /// <summary>Loads the requested managements with their device type; no query without requested managements.</summary>
        private async Task<List<Management>> LoadManagements(IReadOnlyCollection<int> managementIds)
        {
            if (managementIds.Count == 0)
            {
                return [];
            }
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy) ?? [];
            return [.. managements.Where(management => managementIds.Contains(management.Id))];
        }
    }
}
