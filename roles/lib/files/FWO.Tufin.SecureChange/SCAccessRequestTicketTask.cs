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
				convertedobjects.Add(FillServiceTemplate(svc.ProtoId.ToString(), svc.Port.ToString(), svc.Name ?? ""));
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}


		// static private string ConvertNetworkObjectWrapperssToTufinJsonString(List<ModellingAppServerWrapper> nwObjects)
		// {
		// 	// TODO: this is just a mock-up, needs to handle app roles, ...
		// 	string result = "[";
		// 	foreach (ModellingAppServerWrapper srv in nwObjects)
		// 	{
		// 		result += $@"{{
		// 							""@type"": ""IP"",
		// 							""ip_address"": ""{srv.Content.Ip}"",
		// 							""netmask"": ""255.255.255.255"",
		// 							""cidr"": 32
		// 					}},";
		// 	}
		// 	result = result.TrimEnd(',');
		// 	result += "]";
		// 	return result;
		// }

		// static private string ConvertNetworkServiceWrapperssToTufinJsonString(List<ModellingServiceWrapper> services)
		// {
		// 	string result = "[";
		// 	foreach (ModellingServiceWrapper svc in services)
		// 	{
		// 		result += $@"
		// 		{{
		// 			""@type"": ""PROTOCOL"", 
		// 			""protocol"": ""{svc.Content.ProtoId}"", 
		// 			""port"": {svc.Content.Port},
		// 			""name"": ""{svc.Content.Name}""
		// 		}},";
		// 	}
		// 	result = result.TrimEnd(',');
		// 	result += "]";
		// 	return result;
		// }
	}
}
