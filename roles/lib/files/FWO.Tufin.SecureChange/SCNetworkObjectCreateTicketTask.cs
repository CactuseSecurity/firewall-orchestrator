using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCNetworkObjectCreateTicketTask : SCTicketTask
	{
		public SCNetworkObjectCreateTicketTask(WfReqTask reqTask) : base(reqTask)
		{}

		public override void FillTaskText(string tasksTemplate)
		{
			ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
				System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
			TaskText = tasksTemplate
				.Replace("@@GROUPNAME@@", ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName))
				.Replace("@@MANAGEMENT_ID@@", extMgt.ExtId ?? "0")
				.Replace("@@MANAGEMENT_NAME@@", extMgt.ExtName)
				.Replace("@@MEMBERS@@", ConvertNetworkObjects(extMgt.ExtId));
		}

		private string ConvertNetworkObjects(string? mgmId)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(ElemFieldType.source);
			List<string> convertedobjects = [];
			foreach(var nwObj in nwObjects)
			{
				convertedobjects.Add(FillHostTemplate(nwObj.Name ?? "net_" + nwObj.IpString, 
					nwObj.NetworkId?.ToString() ?? "host_" + nwObj.IpString, nwObj.IpString, mgmId ?? "0", nwObj.Comment ?? ""));
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}
	}
}

// {
// "task": {
// 	"fields": {
// 		"field": {
// 			"@xsi.type": "multi_group_change",
// 			"name": "Modify network object group",
// 			"group_change": {
// 				"name": "@@GROUPNAME@@",
// 				"management_id": @@MANAGEMENT_ID@@,
// 				"management_name": "@@MANAGEMENT_NAME@@",
// 				"members": {
// 					"member": @@MEMBERS@@
// 				},
// 				"change_action": "CREATE"
// 			}
// 		}
// 	}
// }
