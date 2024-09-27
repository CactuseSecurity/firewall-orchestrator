using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public abstract class ExternalTicketTask
	{
		public readonly WfReqTask ReqTask = new();
		public string TaskText = "";


		public ExternalTicketTask(WfReqTask reqTask)
		{
			ReqTask = reqTask;
		}

		public abstract void FillTaskText(string tasksTemplate);
	}
}
