using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
	
	public enum TicketSystemType
	{
		Generic,
		TufinSecureChange,
		AlgoSec,
		ServiceNow
	}
	
	public enum TicketPriority
	{
		Low,
		Normal,
		High,
		Critical
	}

	public enum TicketTaskType
	{
		AccessRequest,
		NetworkObjectCreate,
		NetworkServiceCreate
	}

	public class ExternalTicketSystem
	{
		[JsonProperty(nameof(Id)), JsonPropertyName(nameof(Id))]
		public int Id { get; set; } = 0;
		
		[JsonProperty(nameof(TicketSystemType)), JsonPropertyName(nameof(TicketSystemType))]
		public TicketSystemType Type { get; set; } = TicketSystemType.Generic;
				
		[JsonProperty(nameof(Authorization)), JsonPropertyName(nameof(Authorization))]
		public string Authorization { get; set; } = "Basic xyz"; // replace xyz with b64encode(username:password)

		[JsonProperty(nameof(Name)), JsonPropertyName(nameof(Name))]
		public string Name { get; set; } = "";
		
		[JsonProperty(nameof(Url)), JsonPropertyName(nameof(Url))]
		public string Url { get; set; } = "";
		
		[JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
		public string TasksTemplate { get; set; } = "";
		
		public ExternalTicketSystem()
		
		{
		}
		
		public ExternalTicketSystem(string url, string ticketTemplate, string tasksTemplate, string auth)
		
		{
			Url = url;
			TicketTemplate = ticketTemplate;
			TasksTemplate = tasksTemplate;
			Authorization = auth;
		}
	}
}
