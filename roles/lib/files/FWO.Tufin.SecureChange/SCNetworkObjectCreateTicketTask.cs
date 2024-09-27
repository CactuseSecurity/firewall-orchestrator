using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class SCNetworkObjectCreateTicketTask : SCTicketTask
	{
		public SCNetworkObjectCreateTicketTask(WfReqTask reqTask) : base(reqTask)
		{}

		public override void FillTaskText(string tasksTemplate)
		{			
			TaskText = tasksTemplate
				.Replace("@@GROUPNAME@@", ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName))
				.Replace("@@MANAGEMENT_ID@@", "1") // todo
				.Replace("@@MANAGEMENT_NAME@@", "Managementname") // todo
				.Replace("@@MEMBERS@@", ConvertNetworkObjects());
		}

		private string ConvertNetworkObjects()
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(ElemFieldType.source);
			List<string> convertedobjects = [];
			foreach(var nwObj in nwObjects)
			{
				convertedobjects.Add(FillIpTemplate(nwObj.IpString));
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}
	}
}

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