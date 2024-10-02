using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{

	public abstract class SCTicketTask : ExternalTicketTask
	{
		private readonly string IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}";
		private readonly string HostTemplate = "{\"@type\": \"host\", \"name\": \"@@HOSTNAME@@\", \"object_UID\": \"@@OBJECT_UID@@\", \"object_type\": \"host\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"added\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"new\"}";

		private readonly string GroupTemplate = "{\"@type\": \"IP-Group\", \"group_name\": \"@@GROUPNAME@@\"}";
		private readonly string ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"{@@PROTOCOLID@@}\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"";

		public SCTicketTask(WfReqTask reqTask) : base(reqTask)
		{}

		protected string FillIpTemplate(string ipString)
		{
			return IpTemplate.Replace("@@IP@@", ipString);
		}

		protected string FillHostTemplate(string hostname, string objUid, string objDetails, string mgmtId, string comment)
		{
			bool shortened = false;
			return HostTemplate
				.Replace("@@HOSTNAME@@", Sanitizer.SanitizeJsonFieldMand(hostname, ref shortened))
				.Replace("@@OBJECT_UID@@", objUid)
				.Replace("@@OBJECT_DETAILS@@", objDetails)
				.Replace("@@MANAGEMENT_ID@@", mgmtId)
				.Replace("@@COMMENT@@", comment);
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
