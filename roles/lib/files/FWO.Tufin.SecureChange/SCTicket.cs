using FWO.Api.Data;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Net;
using RestSharp;
using FWO.Logging;

namespace FWO.Tufin.SecureChange
{
	public enum SCTaskType
	{
		AccessRequest = 0,
		NetworkObjectModify = 10,
		NetworkServiceCreate = 20,
		NetworkServiceUpdate = 21
	}

	public enum SCTicketPriority
	{
		Low,
		Normal,
		High,
		Critical
	}

	public struct SCChangeAction
	{
		public const string Create = "CREATE";
		public const string Update = "UPDATE";
	}

	public class SCTicket : ExternalTicket
	{
		public string Subject { get; set; } = "";
		public string Priority { get; set; } = SCTicketPriority.Normal.ToString();
		public string Requester { get; set; } = "";
		public string DefaultReason = "Kommunikationsprofil der Anwendung";

		private string actTicketTemplate;
		private SCTaskType actTaskType;


		// {
		// 	"ticket": {
		// 		"id": 2,
		// 		"subject": "Clone Server Policy Ticket",
		// 		"requester": "a",
		// 		"requester_id": 12,
		// 		"priority": "Normal",
		// 		"status": "In Progress",...
		// Todo: move to template settings?
		private class SCPollTicketResponseStatus
		{
			[JsonProperty("status"), JsonPropertyName("status")]
			public string Status { get; set; } = "";
		}

		private class SCPollTicketResponse
		{
			[JsonProperty("ticket"), JsonPropertyName("ticket")]
			public SCPollTicketResponseStatus Ticket { get; set; } = new();
		}

		// {
		// "users": {
		// 	"user": [
		// 	{
		// 		"@xsi.type": "user",
		// 		"id": 55,
		// 		"type": "user",
		// 		"name": "Userxyz",
		private class SCLookupUserResponseUser
		{
			[JsonProperty("id"), JsonPropertyName("id")]
			public int Id { get; set; }
		}

		private class SCLookupUserResponseUsers
		{
			[JsonProperty("user"), JsonPropertyName("user")]
			public List<SCLookupUserResponseUser> Users { get; set; } = [];
		}

		private class SCLookupUsersResponse
		{
			[JsonProperty("users"), JsonPropertyName("users")]
			public SCLookupUserResponseUsers User { get; set; } = new();
		}


		public SCTicket(ExternalTicketSystem tufinSystem)
		{
			TicketSystem = tufinSystem;
			actTicketTemplate = TicketSystem.Templates.FirstOrDefault()?.TicketTemplate ?? "";
		}

		public override async Task CreateRequestString(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
		{
			CreateTicketTasks(tasks, ipProtos, namingConvention);
			await CreateTicketText(tasks.First());
		}

		public override string GetTaskTypeAsString(WfReqTask task)
		{
			return GetTaskType(task).ToString();
		}

		public override async Task<(string, string?)> GetNewState(string oldState)
		{
			RestResponse<int> restResponse = await PollExternalTicket();
			if (restResponse.StatusCode == HttpStatusCode.OK && restResponse.Content != null)
			{
				Log.WriteDebug("Poll external ticket status OK", "Content: " + restResponse.Content);
				SCPollTicketResponse? scResponse = System.Text.Json.JsonSerializer.Deserialize<SCPollTicketResponse?>(restResponse.Content);
				if(scResponse != null)
				{
					return (GetInternalState(scResponse.Ticket.Status.ToUpper()), restResponse.Content);
				}
			}
			Log.WriteDebug("Poll external ticket status failed", "Content: " + restResponse.Content + ", Error Message: " + restResponse.ErrorMessage);
			return (oldState, restResponse.ErrorMessage);
		}

		// IN_PROGRESS, REJECTED, CLOSED, CANCELLED, RESOLVED
		private static string GetInternalState(string externalState)
		{
			if(externalState.Contains("REJECTED") || externalState.Contains("CANCELLED"))
			{
				return ExtStates.ExtReqRejected.ToString();
			}
			else if(externalState.Contains("RESOLVED") || externalState.Contains("CLOSED"))
			{
				return ExtStates.ExtReqDone.ToString();
			}
			else
			{
				return ExtStates.ExtReqInProgress.ToString();
			}
		}

		private static (SCTaskType, string) GetTaskType(WfReqTask task)
		{
			switch(task.TaskType)
			{
				case nameof(WfTaskType.access):
					return (SCTaskType.AccessRequest, SCChangeAction.Create);
				case nameof(WfTaskType.group_create):
					if(task.IsNetworkFlavor())
					{
						return (SCTaskType.NetworkObjectModify, SCChangeAction.Create);
					}
					else
					{
						return (SCTaskType.NetworkServiceCreate, SCChangeAction.Create);
					}
				case nameof(WfTaskType.group_modify):
					if(task.IsNetworkFlavor())
					{
						return (SCTaskType.NetworkObjectModify, SCChangeAction.Update);
					}
					else
					{
						return (SCTaskType.NetworkServiceUpdate, SCChangeAction.Update);
					}
				default: return (SCTaskType.AccessRequest, SCChangeAction.Create);
			}
		}

		private void CreateTicketTasks(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
		{
			foreach (var task in tasks)
			{
				SCTicketTask? ticketTask = null;
				string changeAction;
				(actTaskType, changeAction) = GetTaskType(task);
				switch(actTaskType)
				{
					case SCTaskType.AccessRequest:
						ticketTask = new SCAccessRequestTicketTask(task, ipProtos);
						break;
					case SCTaskType.NetworkObjectModify:
						ticketTask = new SCNetworkObjectModifyTicketTask(task, changeAction, ipProtos, namingConvention);
						break;
				}
				if(ticketTask != null)
				{
					ExternalTicketTemplate? template = TicketSystem.Templates.FirstOrDefault(t => t.TaskType == actTaskType.ToString());
					if(template == null)
					{
						Log.WriteDebug("Create Ticket Tasks", $"No Template found for task type {actTaskType}.");
					}
					else
					{
						ticketTask.FillTaskText(template);
						actTicketTemplate = template.TicketTemplate;
					}
					TicketTasks.Add(ticketTask.TaskText);
				}
			}
		}

		// {
		// 	"ticket": {
		// 		"subject": "@@TICKET_SUBJECT@@",
		// 		"priority": "@@PRIORITY@@",
		// 		"requester": "@@ONBEHALF@@",
		// 		"domain_name": "",
		// 		"workflow": {"name": "Standard Firewall Request"},
		// 		"steps": {
		// 			"step": [
		// 				{
		// 					"name": "Erfassung des Antrags",
		// 					"tasks": {
		// 						"task": {
		// 							"fields": {
		// 								"field": [
		// 										@@TASKS@@
		// 								]
		// 							},
		// 							{
		// 								"@xsi.type": "text_area",
		// 								"name": "Grund für den Antrag",
		// 								"read_only": false,
		// 								"text": "@@REASON@@"
		// 							},
		// 							{
		// 								"@xsi.type": "text_field",
		// 								"name": "Anwendungs-ID",
		// 								"text": "@@APPID@@"
		// 							}]}}}

		private async Task CreateTicketText(WfReqTask? reqTask)
		{
			string appId = reqTask != null && reqTask?.Owners.Count > 0 ? reqTask?.Owners.First()?.Owner.ExtAppId ?? "" : "";
			string onBehalf = TicketSystem.LookupRequesterId ? (await LookupRequesterId(Requester)).ToString() : Requester;
			TicketText = actTicketTemplate
				.Replace("@@TICKET_SUBJECT@@", Subject)
				.Replace("@@PRIORITY@@", Priority)
				.Replace("@@ONBEHALF@@", onBehalf)
				.Replace("@@REASON@@", reqTask?.Reason ?? DefaultReason)
				.Replace("@@APPID@@", appId)
				.Replace("@@TASKS@@", string.Join(",", TicketTasks));
			bool shortened = false;
			TicketText = Sanitizer.SanitizeEolMand(TicketText, ref shortened);
			CheckForProperJson(TicketText);
		}

		private async Task<int> LookupRequesterId(string requesterName)
		{
			if(!string.IsNullOrEmpty(requesterName))
			{
				string restEndPoint = "users.json?user_name=" + requesterName;
				RestRequest request = new(restEndPoint, Method.Get);
				RestResponse<int> restResponse = await RestCall(request, restEndPoint);
				if (restResponse.StatusCode == HttpStatusCode.OK && restResponse.Content != null)
				{
					Log.WriteDebug("Lookup external requester id OK", "Content: " + restResponse.Content);
					SCLookupUsersResponse? scResponse = System.Text.Json.JsonSerializer.Deserialize<SCLookupUsersResponse?>(restResponse.Content);
					if(scResponse != null)
					{
						return scResponse.User.Users.First()?.Id ?? 0;
					}
				}
			}
			return 0;
		}
	}
}


/*
	Create Ticket for creating network groups

	parameters:
	- management_id: we need to get all management ids from tufin st?
	- 

	workflow:
	- get all management ids (from sc or do we need to access sc as well?)
	- loop over all managements
		- create group modify ticket with all groups of the app (first: adds only)
		- store ticket ids for checking status
		- check status and wait for status "closed"

	curl --request POST \
		--insecure \
		--url https://tufin-stest.xxx.de/securechangeworkflow/api/securechange/tickets.json \
		--header 'Authorization: Basic xxx' \
		--header 'Content-Type: application/json' \
		--data '{
		"ticket": {
			"subject": "Neue automatische Gruppenerstellung",
			"priority": "Normal",
			"domain_name": "",
			"workflow": {
				"name": "Automatische Gruppenerstellung"
			},
			"steps": {
				"step": [
					{
						"name": "Submit Request",
						"tasks": {
							"task": {
								"fields": {
									"field": {
										"@xsi.type": "multi_group_change",
										"name": "Modify network object group",
										"group_change": {
											"name": "test-group-change-ticket-1",
											"management_id": 1,
											"management_name": mgmt_name,
											"members": {
												"member": []
											},
											"change_action": "CREATE"
										}
									}
								}
							}
						}
					}
				]
			}
		}
	}'


	Create Ticket for access rule

	curl --request POST \
		--insecure \
		--url https://tufin-stest.xxx.de/securechangeworkflow/api/securechange/tickets.json \
		--header 'Authorization: Basic xxx' \
		--header 'Content-Type: application/json' \
		--data '{
			"ticket": {
				"subject": "NeMo-Testing",
				"priority": "Normal",
				"domain_name": "",
				"workflow": {
					"name": "1. xxx Standard Firewall Request"
				},
				"steps": {
					"step": [
						{
							"name": "Erfassung des Antrags",
							"tasks": {
								"task": {
									"fields": {
										"field": [
											{
												"@xsi.type": "multi_access_request",
												"name": "Gewünschter Zugang",
												"read_only": false,
												"access_request": {
													"order": "AR1",
													"verifier_result": {
														"status": "not run"
													},
													"use_topology": true,
													"targets": {
														"target": {
															"@type": "ANY"
														}
													},
													"users": {
														"user": [
															"Any"
														]
													},
													"sources": {
														"source": [
															{
																"@type": "IP",
																"ip_address": "10.10.100.10",
																"netmask": "255.255.255.255",
																"cidr": 32
															}
														]
													},
													"destinations": {
														"destination": [
															{
																"@type": "IP",
																"ip_address": "10.20.200.0",
																"netmask": "255.255.255.128",
																"cidr": 25
															}
														]
													},
													"services": {
														"service": [
															{
																"@type": "PREDEFINED",
																"protocol": "TCP",
																"port": 23,
																"predefined_name": "telnet"
															},
															{
																"@type": "PREDEFINED",
																"protocol": "TCP",
																"port": 79,
																"predefined_name": "finger"
															},
															{
																"@type": "PROTOCOL",
																"protocol": "TCP",
																"port": 90
															}
														]
													},
													"action": "Accept",
													"labels": ""
												}
											},
											{
												"@xsi.type": "text_area",
												"name": "Grund für den Antrag",
												"read_only": false,
												"text": "dsadsa"
											},
											{
												"@xsi.type": "drop_down_list",
												"name": "Regel Log aktivieren?",
												"selection": "Ja"
											},
											{
												"@xsi.type": "date",
												"name": "Regel befristen bis:"
											},
											{
												"@xsi.type": "text_field",
												"name": "Anwendungs-ID",
												"text": "APP-3838"
											},
											{
												"@xsi.type": "checkbox",
												"name": "Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt",
												"value": false
											},
											{
												"@xsi.type": "drop_down_list",
												"name": "Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)",
												"selection": "Nein"
											}
										]
									}
								}
							}
						}
					]
				}
			}
		}'
*/
