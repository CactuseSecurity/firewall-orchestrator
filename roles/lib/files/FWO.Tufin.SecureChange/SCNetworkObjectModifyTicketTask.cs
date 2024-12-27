using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCNetworkObjectModifyTicketTask : SCTicketTask
	{
		private string ChangeAction;
		public SCNetworkObjectModifyTicketTask(WfReqTask reqTask, string changeAction, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention) : base(reqTask, ipProtos, namingConvention)
		{
			ChangeAction = changeAction;
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
		// 				"change_action": "@@CHANGEACTION@@"
		// 			}
		// 		}
		// 	}
		// }
		public override void FillTaskText(ExternalTicketTemplate template)
		{
			ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
				System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
			bool shortened = false;
			TaskText = template.TasksTemplate
				.Replace("@@GROUPNAME@@", Sanitizer.SanitizeJsonFieldMand(ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName), ref shortened))
				.Replace("@@MANAGEMENT_ID@@", extMgt.ExtId ?? "0")
				.Replace("@@MANAGEMENT_NAME@@", extMgt.ExtName)
				.Replace("@@CHANGEACTION@@", ChangeAction)
				.Replace("@@MEMBERS@@", ConvertNetworkObjects(template, extMgt.ExtId, NamingConvention));
		}
	}
}
