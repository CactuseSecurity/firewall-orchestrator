using System.Collections.ObjectModel;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>Part of a Tufin SecureChange ticket template, one property of <see cref="ExternalTicketTemplate"/>.</summary>
public enum SecureChangeTemplatePart
{
    Ticket,
    Tasks,
    Object,
    ObjectShort,
    Ip,
    NwObjGroup,
    Service,
    Icmp,
    IpProtocol
}

/// <summary>
/// Device specific setting keys of the Tufin SecureChange ticket templates: one key per task type and template part the
/// task type uses. The templates apply to the management of the request task, so they can be overridden down to the
/// management level. An empty template means that the default shipped for the task type is used.
/// </summary>
public static class SecureChangeTemplateSettingKeys
{
    private const string kKeyPrefix = "scTemplate";

    private static readonly List<DeviceSpecificScopeType> ManagerScopeTypes =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management];

    private static readonly List<SecureChangeTemplatePart> AccessRequestParts =
    [
        SecureChangeTemplatePart.Ticket, SecureChangeTemplatePart.Tasks, SecureChangeTemplatePart.Ip, SecureChangeTemplatePart.NwObjGroup,
        SecureChangeTemplatePart.Service, SecureChangeTemplatePart.Icmp, SecureChangeTemplatePart.IpProtocol
    ];

    private static readonly List<SecureChangeTemplatePart> NetworkObjectModifyParts =
    [
        SecureChangeTemplatePart.Ticket, SecureChangeTemplatePart.Tasks, SecureChangeTemplatePart.Object, SecureChangeTemplatePart.ObjectShort,
        SecureChangeTemplatePart.Ip, SecureChangeTemplatePart.NwObjGroup
    ];

    /// <summary>
    /// Task types SecureChange tickets are created for. SecureChange only modifies groups of network objects, so service
    /// group tasks (NetworkServiceCreate, NetworkServiceUpdate) are refused and have no templates. Declared before
    /// <see cref="Keys"/>, which uses it during static initialization.
    /// </summary>
    public static IReadOnlyList<SCTaskType> TaskTypes { get; } = new List<SCTaskType> { SCTaskType.AccessRequest, SCTaskType.NetworkObjectModify }.AsReadOnly();

    /// <summary>Template setting per task type and template part, only for the parts the task type uses.</summary>
    public static IReadOnlyDictionary<(SCTaskType TaskType, SecureChangeTemplatePart Part), DeviceSpecificSettingKey<string>> Keys { get; } =
        CreateKeys();

    /// <summary>All SecureChange template keys, ordered by task type and template part.</summary>
    public static IReadOnlyList<DeviceSpecificSettingKey> All { get; } = Keys.Values.ToList<DeviceSpecificSettingKey>().AsReadOnly();

    /// <summary>The template parts a task type of <see cref="TaskTypes"/> uses, in the order they are shown.</summary>
    public static IReadOnlyList<SecureChangeTemplatePart> PartsOf(SCTaskType taskType)
    {
        return taskType switch
        {
            SCTaskType.AccessRequest => AccessRequestParts,
            SCTaskType.NetworkObjectModify => NetworkObjectModifyParts,
            _ => throw new ArgumentOutOfRangeException(nameof(taskType), taskType, "SecureChange tickets have no templates for this task type.")
        };
    }

    /// <summary>Reads one part of a ticket template.</summary>
    public static string GetPart(ExternalTicketTemplate template, SecureChangeTemplatePart part)
    {
        ArgumentNullException.ThrowIfNull(template);
        return part switch
        {
            SecureChangeTemplatePart.Ticket => template.TicketTemplate,
            SecureChangeTemplatePart.Tasks => template.TasksTemplate,
            SecureChangeTemplatePart.Object => template.ObjectTemplate,
            SecureChangeTemplatePart.ObjectShort => template.ObjectTemplateShort,
            SecureChangeTemplatePart.Ip => template.IpTemplate,
            SecureChangeTemplatePart.NwObjGroup => template.NwObjGroupTemplate,
            SecureChangeTemplatePart.Service => template.ServiceTemplate,
            SecureChangeTemplatePart.Icmp => template.IcmpTemplate,
            SecureChangeTemplatePart.IpProtocol => template.IpProtocolTemplate,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown SecureChange template part.")
        };
    }

    /// <summary>Writes one part of a ticket template.</summary>
    public static void SetPart(ExternalTicketTemplate template, SecureChangeTemplatePart part, string value)
    {
        ArgumentNullException.ThrowIfNull(template);
        switch (part)
        {
            case SecureChangeTemplatePart.Ticket: template.TicketTemplate = value; break;
            case SecureChangeTemplatePart.Tasks: template.TasksTemplate = value; break;
            case SecureChangeTemplatePart.Object: template.ObjectTemplate = value; break;
            case SecureChangeTemplatePart.ObjectShort: template.ObjectTemplateShort = value; break;
            case SecureChangeTemplatePart.Ip: template.IpTemplate = value; break;
            case SecureChangeTemplatePart.NwObjGroup: template.NwObjGroupTemplate = value; break;
            case SecureChangeTemplatePart.Service: template.ServiceTemplate = value; break;
            case SecureChangeTemplatePart.Icmp: template.IcmpTemplate = value; break;
            case SecureChangeTemplatePart.IpProtocol: template.IpProtocolTemplate = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown SecureChange template part.");
        }
    }

    private static ReadOnlyDictionary<(SCTaskType, SecureChangeTemplatePart), DeviceSpecificSettingKey<string>> CreateKeys()
    {
        Dictionary<(SCTaskType, SecureChangeTemplatePart), DeviceSpecificSettingKey<string>> keys = [];
        foreach (SCTaskType taskType in TaskTypes)
        {
            foreach (SecureChangeTemplatePart part in PartsOf(taskType))
            {
                keys[(taskType, part)] = new DeviceSpecificSettingKey<string>(
                    $"{kKeyPrefix}{taskType}{part}",
                    ManagerScopeTypes,
                    settings => ReadTemplate(settings, taskType, part),
                    (settings, value) => WriteTemplate(settings, taskType, part, value));
            }
        }
        return new ReadOnlyDictionary<(SCTaskType, SecureChangeTemplatePart), DeviceSpecificSettingKey<string>>(keys);
    }

    private static string ReadTemplate(GlobalDeviceSpecificSettings settings, SCTaskType taskType, SecureChangeTemplatePart part)
    {
        return settings.SecureChangeTemplates.TryGetValue(taskType, out ExternalTicketTemplate? template) ? GetPart(template, part) : "";
    }

    private static void WriteTemplate(GlobalDeviceSpecificSettings settings, SCTaskType taskType, SecureChangeTemplatePart part, string value)
    {
        if (!settings.SecureChangeTemplates.TryGetValue(taskType, out ExternalTicketTemplate? template))
        {
            template = new ExternalTicketTemplate { TaskType = taskType.ToString() };
            settings.SecureChangeTemplates[taskType] = template;
        }
        SetPart(template, part, value);
    }
}
