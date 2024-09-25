using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Tufin.SecureChange
{
	abstract public class ExternalTicket //: WfTicket
	{
        [JsonProperty("tasks"), JsonPropertyName("tasks")]
		public List<ExternalAccessRequestTicketTask> TicketTasks = [];

		protected string OnBehalfOfUser = "";
		// protected string OnBehalfOfUser = """"requester_id": 55,"""";

		public string TicketTemplate = """
{
	"ticket": {
		"subject": "@@TICKET_SUBJECT@@",
		"priority": "@@PRIORITY@@",
		"requester": "@@ONBEHALF@@",
		"domain_name": "",
		"workflow": {
			"name": "@@WORKFLOW_NAME@@"
		},
		"steps": {
			"step": [
				{
					"name": "Erfassung des Antrags",
					"tasks": {
						"task": {
							"fields": {
								"field": [
										@@TASKS@@
								]
							}
						}
					}
				}
			]
		}
	}
}
""";
		public string TasksTemplate = """
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
												"user": @@USERS@@
											},
											"sources": {
												"source": @@SOURCES@@
											},
											"destinations": {
												"destination": @@DESTINATIONS@@
											},
											"services": {
												"service": @@SERVICES@@
											},
											"action": "@@ACTION@@",
											"labels": ""
										}
									},
									{
										"@xsi.type": "text_area",
										"name": "Grund für den Antrag",
										"read_only": false,
										"text": "@@REASON@@"
									},
									{
										"@xsi.type": "drop_down_list",
										"name": "Regel Log aktivieren?",
										"selection": "@@LOGGING@@"
									},
									{
										"@xsi.type": "date",
										"name": "Regel befristen bis:"
									},
									{
										"@xsi.type": "text_field",
										"name": "Anwendungs-ID",
										"text": "@@APPID@@"
									},
									{
										"@xsi.type": "checkbox",
										"name": "Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt",
										"value":  @@COM_DOCUMENTED@@
									},
									{
										"@xsi.type": "drop_down_list",
										"name": "Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)",
										"selection": "Nein"
									}
		""";
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

