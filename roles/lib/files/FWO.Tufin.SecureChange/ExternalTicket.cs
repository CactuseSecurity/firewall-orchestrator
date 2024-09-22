using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	abstract public class ExternalTicket : WfTicket
	{
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
