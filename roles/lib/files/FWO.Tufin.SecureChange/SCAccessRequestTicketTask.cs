using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCAccessRequestTicketTask : SCTicketTask
	{
		public SCAccessRequestTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention = null) : base(reqTask, ipProtos, namingConvention)
		{}

		// {
		// 	"order": "@@ORDERNAME@@",
		// 	"verifier_result": {
		// 		"status": "not run"
		// 	},
		// 	"use_topology": true,
		// 	"targets": {
		// 		"target": {
		// 			"@type": "ANY"
		// 		}
		// 	},
		// 	"sources": {
		// 		"source": @@SOURCES@@
		// 	},
		// 	"destinations": {
		// 		"destination": @@DESTINATIONS@@
		// 	},
		// 	"services": {
		// 		"service": @@SERVICES@@
		// 	},
		// 	"labels": "",
		//  "comment": "@@TASKCOMMENT@@
		// }
		public override void FillTaskText(ExternalTicketTemplate template)
		{
			ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
				System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
			TaskText = template.TasksTemplate
				.Replace("@@ORDERNAME@@", "AR"+ ReqTask.TaskNumber.ToString())
				.Replace("@@TASKCOMMENT@@", ReqTask.GetFirstCommentText())
				.Replace("@@SOURCES@@", ConvertNetworkElems(template, ElemFieldType.source, extMgt.ExtName))
				.Replace("@@DESTINATIONS@@", ConvertNetworkElems(template, ElemFieldType.destination, extMgt.ExtName))
				.Replace("@@SERVICES@@", ConvertServiceElems(template));
		}

		private string ConvertNetworkElems(ExternalTicketTemplate template, ElemFieldType fieldType, string? mgtName)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(fieldType);
			List<string> convertedObjects = [];
			foreach(var nwObj in nwObjects)
			{
				if(nwObj.GroupName != "")
				{
					if(convertedObjects.FirstOrDefault(o => o == nwObj.GroupName) == null)
					{
						convertedObjects.Add(FillNwObjGroupTemplate(template, nwObj.GroupName, mgtName ?? ""));
					}
				}
				else
				{
					convertedObjects.Add(FillIpTemplate(template, nwObj.IpString));
				}
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

		private string ConvertServiceElems(ExternalTicketTemplate template)
		{
			List<NwServiceElement> nwServiceElements = ReqTask.GetServiceElements();
			List<string> convertedObjects = [];
			foreach(var svc in nwServiceElements)
			{
				convertedObjects.Add(FillServiceTemplate(template, IpProtos.FirstOrDefault(x => x.Id == svc.ProtoId)?.Name ?? svc.ProtoId.ToString(), DisplayPortRange(svc.Port, svc.PortEnd), svc.Name ?? ""));
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

		private static string DisplayPortRange(int port, int? portEnd)
		{
			return portEnd == null || portEnd == 0 || port == portEnd ? $"{port}" : $"{port}-{portEnd}";
		}
	}
}
