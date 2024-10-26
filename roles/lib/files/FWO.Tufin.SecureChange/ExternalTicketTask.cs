using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public abstract class ExternalTicketTask
	{
		public readonly WfReqTask ReqTask = new();
		public string TaskText = "";
		public ModellingNamingConvention? NamingConvention;


		public ExternalTicketTask(WfReqTask reqTask, ModellingNamingConvention? namingConvention)
		{
			ReqTask = reqTask;
			NamingConvention = namingConvention;
		}

		public abstract void FillTaskText(string tasksTemplate);
	}
}
