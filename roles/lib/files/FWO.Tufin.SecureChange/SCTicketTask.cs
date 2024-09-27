using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{

	public abstract class SCTicketTask : ExternalTicketTask
	{
		private readonly string IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}";
		private readonly string GroupTemplate = "{\"@type\": \"IP-Group\", \"group_name\": \"@@GROUPNAME@@\"}";
		private readonly string ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"{@@PROTOCOLID@@}\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"";

		public SCTicketTask(WfReqTask reqTask) : base(reqTask)
		{}

		protected string FillIpTemplate(string ipString)
		{
			return IpTemplate.Replace("@@IP@@", ipString);
		}

		protected string FillGroupTemplate(string groupName)
		{
			return GroupTemplate.Replace("@@GROUPNAME@@", groupName);
		}

		protected string FillServiceTemplate(string protoclId, string port, string serviceName)
		{
			return ServiceTemplate.Replace("@@PROTOCOLID@@", protoclId).Replace("@@PORT@@", port).Replace("@@SERVICENAME@@", serviceName);
		}
	}
}
