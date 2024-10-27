using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCNetworkObjectUpdateTicketTask : SCTicketTask
	{
		public SCNetworkObjectUpdateTicketTask(WfReqTask reqTask, ModellingNamingConvention? namingConvention) : base(reqTask, namingConvention)
		{}

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
		// 				"change_action": "UPDATE"
		// 			}
		// 		}
		// 	}
		// }
		// public override void FillTaskText(string tasksTemplate)
		// {
		// 	ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
		// 		System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
		// 	bool shortened = false;
		// 	TaskText = tasksTemplate
		// 		.Replace("@@GROUPNAME@@", Sanitizer.SanitizeJsonFieldMand(ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName), ref shortened))
		// 		// .Replace("@@OBJECT_UID@@", ReqTask.NetworkGroupId.ToString())
		// 		.Replace("@@MANAGEMENT_ID@@", extMgt.ExtId ?? "0")
		// 		.Replace("@@MANAGEMENT_NAME@@", extMgt.ExtName)
		// 		.Replace("@@MEMBERS@@", ConvertNetworkObjects(extMgt.ExtId, NamingConvention));
		// }
	}
}
