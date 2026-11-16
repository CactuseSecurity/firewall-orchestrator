using FWO.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.Tufin.SecureChange
{
    /// <summary>
    /// Ticket templates shipped for Tufin SecureChange. They apply wherever the device specific settings leave a template
    /// part empty, so a later release can correct them. Only the access request ticket and its task have a shipped template.
    /// </summary>
    public static class SCTemplateDefaults
    {
        private const string kAccessRequestTicket = """{"ticket":{"subject":"@@TICKET_SUBJECT@@","priority":"@@PRIORITY@@","requester":"@@ONBEHALF@@","domain_name":"","workflow":{"name":"@@WORKFLOW_NAME@@"},"steps":{"step":[{"name":"Erfassung des Antrags","tasks":{"task":{"fields":{"field":[@@TASKS@@]}}}}]}}}""";

        private const string kAccessRequestTasks = """{"@xsi.type":"multi_access_request","name":"GewünschterZugang","read_only":false,"access_request":{"order":"AR1","verifier_result":{"status":"notrun"},"use_topology":true,"targets":{"target":{"@type":"ANY"}},"users":{"user":@@USERS@@},"sources":{"source":@@SOURCES@@},"destinations":{"destination":@@DESTINATIONS@@},"services":{"service":@@SERVICES@@},"action":"@@ACTION@@","labels":""}},{"@xsi.type":"text_area","name":"Grund für den Antrag","read_only":false,"text":"@@REASON@@"},{"@xsi.type":"drop_down_list","name":"Regel Log aktivieren?","selection":"@@LOGGING@@"},{"@xsi.type":"date","name":"Regel befristen bis:"},{"@xsi.type":"text_field","name":"Anwendungs-ID","text":"@@APPID@@"},{"@xsi.type":"checkbox","name":"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt","value":@@COM_DOCUMENTED@@},{"@xsi.type":"drop_down_list","name":"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)","selection":"Nein"}""";

        /// <summary>Returns the shipped template part of a task type, or null if none is shipped.</summary>
        public static string? Get(SCTaskType taskType, SecureChangeTemplatePart part)
        {
            return (taskType, part) switch
            {
                (SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket) => kAccessRequestTicket,
                (SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks) => kAccessRequestTasks,
                _ => null
            };
        }
    }
}
