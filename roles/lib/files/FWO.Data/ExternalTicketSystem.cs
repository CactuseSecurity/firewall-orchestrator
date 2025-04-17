﻿using Newtonsoft.Json;
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

		public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Url = Sanitizer.SanitizePasswMand(Url, ref shortened);
			TicketTemplate = Sanitizer.SanitizeJsonMand(TicketTemplate, ref shortened);
			TasksTemplate = Sanitizer.SanitizeJsonMand(TasksTemplate, ref shortened);
			foreach(var template in Templates)
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


		public bool Sanitize()
        {
            bool shortened = false;
            TaskType = Sanitizer.SanitizeMand(TaskType, ref shortened);
			TicketTemplate = Sanitizer.SanitizeJsonMand(TicketTemplate, ref shortened);
			TasksTemplate = Sanitizer.SanitizeJsonMand(TasksTemplate, ref shortened);
			ObjectTemplate = Sanitizer.SanitizeJsonMand(ObjectTemplate, ref shortened);
			ObjectTemplateShort = Sanitizer.SanitizeJsonMand(ObjectTemplateShort, ref shortened);
			IpTemplate = Sanitizer.SanitizeJsonMand(IpTemplate, ref shortened);
			NwObjGroupTemplate = Sanitizer.SanitizeJsonMand(NwObjGroupTemplate, ref shortened);
			ServiceTemplate = Sanitizer.SanitizeJsonMand(ServiceTemplate, ref shortened);
			IcmpTemplate = Sanitizer.SanitizeJsonMand(IcmpTemplate, ref shortened);
            return shortened;
        }
	}
}
