using FWO.Basics;
using FWO.Data.Workflow;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
	public enum ExternalTicketSystemType
	{
		Generic,
		TufinSecureChange,
		AlgoSec,
		ServiceNow
	}

	public class ExternalTicketSystem
	{
		[JsonProperty(nameof(Id)), JsonPropertyName(nameof(Id))]
		public int Id { get; set; } = 0;
		
		[JsonProperty(nameof(ExternalTicketSystemType)), JsonPropertyName(nameof(ExternalTicketSystemType))]
		public ExternalTicketSystemType Type { get; set; } = ExternalTicketSystemType.Generic;
				
		[JsonProperty(nameof(Authorization)), JsonPropertyName(nameof(Authorization))]
		public string Authorization { get; set; } = "Basic xyz"; // replace xyz with b64encode(username:password)

		[JsonProperty(nameof(Name)), JsonPropertyName(nameof(Name))]
		public string Name { get; set; } = "";
		
		[JsonProperty(nameof(Url)), JsonPropertyName(nameof(Url))]
		public string Url { get; set; } = "";

		[JsonProperty(nameof(LookupRequesterId)), JsonPropertyName(nameof(LookupRequesterId))]
		public bool LookupRequesterId { get; set; } = false;

		[JsonProperty(nameof(Templates)), JsonPropertyName(nameof(Templates))]
		public List<ExternalTicketTemplate> Templates { get; set; } = [];

		// just for backward compatibility
		[JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
		public string TasksTemplate { get; set; } = "";

		[JsonProperty(nameof(ResponseTimeout)), JsonPropertyName(nameof(ResponseTimeout))]
		public int ResponseTimeout { get; set; } = 300;

		[JsonProperty(nameof(MaxAttempts)), JsonPropertyName(nameof(MaxAttempts))]
		public int MaxAttempts { get; set; } = 3;

		[JsonProperty(nameof(CyclesBetweenAttempts)), JsonPropertyName(nameof(CyclesBetweenAttempts))]
		public int CyclesBetweenAttempts { get; set; } = 5;

		public int MaxBundledTasks()
		{
			return Type switch
			{
				ExternalTicketSystemType.TufinSecureChange => SCConstants.SCMaxBundledTasks,
                _ => 1
			};
		}

		public bool BundleGateways()
		{
			return Type switch
			{
				ExternalTicketSystemType.TufinSecureChange => SCConstants.SCBundleGateways,
                _ => false
			};
		}
		
		public List<string> TaskTypesToBundleGateways()
		{
			return Type switch
			{
				ExternalTicketSystemType.TufinSecureChange => [WfTaskType.rule_modify.ToString(), WfTaskType.rule_delete.ToString()],
                _ => []
			};
		}

		public bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeMand(ref shortened);
            Url = Url.SanitizeMand(ref shortened);
            TicketTemplate = TicketTemplate.SanitizeJsonMand(ref shortened);
			TasksTemplate = TasksTemplate.SanitizeJsonMand(ref shortened);
            foreach (var template in Templates)
			{
				shortened = template.Sanitize();
			}
            return shortened;
        }
	}

	public class ExternalTicketTemplate
	{
		[JsonProperty(nameof(TaskType)), JsonPropertyName(nameof(TaskType))]
		public string TaskType { get; set; } = "";

		[JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
		public string TasksTemplate { get; set; } = "";

		[JsonProperty(nameof(ObjectTemplate)), JsonPropertyName(nameof(ObjectTemplate))]
		public string ObjectTemplate { get; set; } = "";

		[JsonProperty(nameof(ObjectTemplateShort)), JsonPropertyName(nameof(ObjectTemplateShort))]
		public string ObjectTemplateShort { get; set; } = "";

		[JsonProperty(nameof(IpTemplate)), JsonPropertyName(nameof(IpTemplate))]
		public string IpTemplate { get; set; } = "";

		[JsonProperty(nameof(NwObjGroupTemplate)), JsonPropertyName(nameof(NwObjGroupTemplate))]
		public string NwObjGroupTemplate { get; set; } = "";

		[JsonProperty(nameof(ServiceTemplate)), JsonPropertyName(nameof(ServiceTemplate))]
		public string ServiceTemplate { get; set; } = "";

		[JsonProperty(nameof(IcmpTemplate)), JsonPropertyName(nameof(IcmpTemplate))]
		public string IcmpTemplate { get; set; } = "";

		[JsonProperty(nameof(IpProtocolTemplate)), JsonPropertyName(nameof(IpProtocolTemplate))]
		public string IpProtocolTemplate { get; set; } = "";


		public bool Sanitize()
		{
			bool shortened = false;
			TaskType = TaskType.SanitizeMand(ref shortened);
            TicketTemplate = TicketTemplate.SanitizeJsonMand(ref shortened);
			TasksTemplate = TasksTemplate.SanitizeJsonMand(ref shortened);
			ObjectTemplate = ObjectTemplate.SanitizeJsonMand(ref shortened);
			ObjectTemplateShort = ObjectTemplateShort.SanitizeJsonMand(ref shortened);
			IpTemplate = IpTemplate.SanitizeJsonMand(ref shortened);
			NwObjGroupTemplate = NwObjGroupTemplate.SanitizeJsonMand(ref shortened);
			ServiceTemplate = ServiceTemplate.SanitizeJsonMand(ref shortened);
			IcmpTemplate = IcmpTemplate.SanitizeJsonMand(ref shortened);
			IpProtocolTemplate = IpProtocolTemplate.SanitizeJsonMand(ref shortened);
			return shortened;
		}
	}
}
