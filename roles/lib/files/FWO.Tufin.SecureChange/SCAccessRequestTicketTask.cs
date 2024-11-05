using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCAccessRequestTicketTask : SCTicketTask
	{
		public SCAccessRequestTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention = null) : base(reqTask, ipProtos, namingConvention)
		{}

		public override void FillTaskText(string tasksTemplate)
		{
			ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
				System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
			TaskText = tasksTemplate
				.Replace("@@ORDERNAME@@", "AR"+ ReqTask.TaskNumber.ToString())
				.Replace("@@TASKCOMMENT@@", ReqTask.GetFirstCommentText())
				.Replace("@@SOURCES@@", ConvertNetworkElems(ElemFieldType.source, extMgt.ExtName))
				.Replace("@@DESTINATIONS@@", ConvertNetworkElems(ElemFieldType.destination, extMgt.ExtName))
				.Replace("@@SERVICES@@", ConvertServiceElems());
		}

		private string ConvertNetworkElems(ElemFieldType fieldType, string? mgtName)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(fieldType);
			List<string> convertedObjects = [];
			foreach(var nwObj in nwObjects)
			{
				if(nwObj.GroupName != "")
				{
					if(convertedObjects.FirstOrDefault(o => o == nwObj.GroupName) == null)
					{
						convertedObjects.Add(FillNwObjGroupTemplate(nwObj.GroupName, mgtName ?? ""));
					}
				}
				else
				{
					convertedObjects.Add(FillIpTemplate(nwObj.IpString));
				}
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

		private string ConvertServiceElems()
		{
			List<NwServiceElement> nwServiceElements = ReqTask.GetServiceElements();
			List<string> convertedObjects = [];
			foreach(var svc in nwServiceElements)
			{
				convertedObjects.Add(FillServiceTemplate(IpProtos.FirstOrDefault(x => x.Id == svc.ProtoId)?.Name ?? svc.ProtoId.ToString(), DisplayPortRange(svc.Port, svc.PortEnd), svc.Name ?? ""));
			}
			return "[" + string.Join(",", convertedObjects) + "]";
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
