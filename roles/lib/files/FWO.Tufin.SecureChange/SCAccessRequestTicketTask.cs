using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCAccessRequestTicketTask : SCTicketTask
	{

		// mockup:
		private readonly string Action = "Accept";
		private readonly string Logging = "Ja";
		private readonly string EndDate = "";
		private readonly string AppId = "APP-4711";
		//private string Reason = "der Grund ..."
		private readonly string ComDocumented = "false";
				

		public SCAccessRequestTicketTask(WfReqTask reqTask) : base(reqTask)
		{}


		public override void FillTaskText(string tasksTemplate)
		{			
			TaskText = tasksTemplate
				.Replace("@@USERS@@", "[\"Any\"]") // data not provided yet
				.Replace("@@SOURCES@@", ConvertNetworkObjects(ElemFieldType.source))
				.Replace("@@DESTINATIONS@@", ConvertNetworkObjects(ElemFieldType.destination))
				.Replace("@@SERVICES@@", ConvertServiceObjects())
				.Replace("@@ACTION@@", Action) // -> ReqTask.RuleAction
				.Replace("@@REASON@@", ReqTask.Reason)
				.Replace("@@LOGGING@@", Logging) // -> ReqTask.Tracking
				.Replace("@@ENDDATE@@", EndDate) // woher?
				.Replace("@@APPID@@", AppId) // ExtAppId -> AdditionalInfo ?
				.Replace("@@COM_DOCUMENTED@@", ComDocumented); // ??
		}

		private string ConvertNetworkObjects(ElemFieldType fieldType)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(fieldType);
			List<string> convertedobjects = [];
			foreach(var nwObj in nwObjects)
			{
				if(nwObj.GroupName != "" && convertedobjects.FirstOrDefault(o => o == nwObj.GroupName) == null)
				{
					convertedobjects.Add(FillGroupTemplate(nwObj.GroupName));
				}
				else
				{
					convertedobjects.Add(FillIpTemplate(nwObj.IpString));
				}
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}

		private string ConvertServiceObjects()
		{
			List<NwServiceElement> nwServiceElements = ReqTask.GetServiceElements();
			List<string> convertedobjects = [];
			foreach(var svc in nwServiceElements)
			{
				convertedobjects.Add(FillServiceTemplate(svc.ProtoId.ToString(), DisplayPortRange(svc.Port, svc.PortEnd), svc.Name ?? ""));
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}

		private static string DisplayPortRange(int port, int? portEnd)
		{
			return portEnd == null || portEnd == 0 || port == portEnd ? $"{port}" : $"{port}-{portEnd}";
		}
	}
}

// {
// 	"@xsi.type": "multi_access_request",
// 	"name": "Gewünschter Zugang",
// 	"read_only": false,
// 	"access_request": {
// 		"order": "AR1",
// 		"verifier_result": {
// 			"status": "not run"
// 		},
// 		"use_topology": true,
// 		"targets": {
// 			"target": {
// 				"@type": "ANY"
// 			}
// 		},
// 		"users": {
// 			"user": @@USERS@@
// 		},
// 		"sources": {
// 			"source": @@SOURCES@@
// 		},
// 		"destinations": {
// 			"destination": @@DESTINATIONS@@
// 		},
// 		"services": {
// 			"service": @@SERVICES@@
// 		},
// 		"action": "@@ACTION@@",
// 		"labels": ""
// 	}
// },
// {
// 	"@xsi.type": "text_area",
// 	"name": "Grund für den Antrag",
// 	"read_only": false,
// 	"text": "@@REASON@@"
// },
// {
// 	"@xsi.type": "drop_down_list",
// 	"name": "Regel Log aktivieren?",
// 	"selection": "@@LOGGING@@"
// },
// {
// 	"@xsi.type": "date",
// 	"name": "Regel befristen bis:"
// },
// {
// 	"@xsi.type": "text_field",
// 	"name": "Anwendungs-ID",
// 	"text": "@@APPID@@"
// },
// {
// 	"@xsi.type": "checkbox",
// 	"name": "Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt",
// 	"value":  @@COM_DOCUMENTED@@
// },
// {
// 	"@xsi.type": "drop_down_list",
// 	"name": "Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)",
// 	"selection": "Nein"
// }
