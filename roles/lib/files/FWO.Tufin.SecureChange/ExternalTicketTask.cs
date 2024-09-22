using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public class ExternalAccessRequestTicketTask //: WfReqTask
	{
		//private ModellingConnection Connection = new();
		private readonly WfReqTask ReqTask = new();

		// mockup:
		private readonly string Action = "Accept";
		private readonly string Logging = "Ja";
		private readonly string EndDate = "";
		private readonly string AppId = "APP-4711";
		//private string Reason = "der Grund ..."
		private readonly string ComDocumented = "false";
				
		// private TicketTaskType TaskType = TicketTaskType.AccessRequest;

		public ExternalAccessRequestTicketTask()
		{
		}

		// public ExternalAccessRequestTicketTask(ModellingConnection modellingConnection)
		// {
		// 	Connection = modellingConnection;
		// }

		public ExternalAccessRequestTicketTask(WfReqTask reqTask)
		{
			ReqTask = reqTask;
		}


		public string FillTaskTemplate(string tasksTemplate)
		{			
			return tasksTemplate
				.Replace("@@USERS@@", "[\"Any\"]") // data not provided yet
				.Replace("@@SOURCES@@", ConvertNetworkObjects(ReqTask.GetNwObjectElements(ElemFieldType.source)))
				.Replace("@@DESTINATIONS@@", ConvertNetworkObjects(ReqTask.GetNwObjectElements(ElemFieldType.destination)))
				.Replace("@@SERVICES@@", ConvertServiceObjects(ReqTask.GetServiceElements()))
				.Replace("@@ACTION@@", Action) // -> ReqTask.RuleAction
				.Replace("@@REASON@@", ReqTask.Reason)
				.Replace("@@LOGGING@@", Logging) // -> ReqTask.Tracking
				.Replace("@@ENDDATE@@", EndDate) // woher?
				.Replace("@@APPID@@", AppId) // ExtAppId -> AdditionalInfo ?
				.Replace("@@COM_DOCUMENTED@@", ComDocumented); // ??
		}

		private string ConvertNetworkObjects(List<NwObjectElement> nwObjectElements)
		{
			return "";
		}

		private string ConvertServiceObjects(List<NwServiceElement> nwServiceElements)
		{
			return "";
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
