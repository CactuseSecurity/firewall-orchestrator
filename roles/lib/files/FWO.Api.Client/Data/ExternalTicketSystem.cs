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
		[JsonProperty("Id"), JsonPropertyName("Id")]
		public int Id { get; set; } = 0;
		
		[JsonProperty("TicketSystemType"), JsonPropertyName("TicketSystemType")]
		public TicketSystemType Type { get; set; } = TicketSystemType.Generic;
				
		[JsonProperty("Authorization"), JsonPropertyName("Authorization")]
		public string Authorization { get; set; } = "Basic xyz"; // replace xyz with b64encode(username:password)

		[JsonProperty("Name"), JsonPropertyName("Name")]
		public string Name { get; set; } = "";
		
		[JsonProperty("Url"), JsonPropertyName("Url")]
		public string Url { get; set; } = "";
		
		[JsonProperty("TicketTemplate"), JsonPropertyName("TicketTemplate")]
		public string TicketTemplate { get; set; } = "";

		[JsonProperty("TasksTemplate"), JsonPropertyName("TasksTemplate")]
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
