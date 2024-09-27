using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
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
		
		[JsonProperty(nameof(Templates)), JsonPropertyName(nameof(Templates))]
		public List<ExternalTicketTemplate> Templates { get; set; } = [];

		// just for backward compatibility
		[JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
		public string TasksTemplate { get; set; } = "";
	}

	public class ExternalTicketTemplate
	{
		[JsonProperty(nameof(TaskType)), JsonPropertyName(nameof(TaskType))]
		public string TaskType { get; set; } = "";

		[JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
		public string TasksTemplate { get; set; } = "";
	}
}
