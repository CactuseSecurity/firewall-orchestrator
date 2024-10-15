using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{

	public abstract class SCTicketTask : ExternalTicketTask
	{
		// To use a service object in a new request you must use two parameters to specify the object. The parameter options are:
		// 1. Both object_UID and management_id
		// 2. Both object_name and management_name

		// To use a network object in a new request you must use two parameters to specify the object. The parameter options are:
		// 1. Both object_UID and management_id (for Access Request and Group Change workflow)
		// 2. Both object_name and management_name (only for Access Request workflow)
		// 3. Both object_name and management_id (only for Group Change workflow)
		// **Note: If both object_UID and object_name are defined, object_UID takes precedence over object_name

		// * Network object types:
    	// SecureTrack API element <class_name> includes one of the following types:
    	// gateway_ckp, host_ckp, connectra, interspect, gateway_cluster, cluster_member,
		// sofaware_gateway, sofaware_gateway_profile, vsx_box, vs_cluster_member, vs_cluster_netobj, 
		// vsx_cluster_member, vsx_cluster_netobj, vs_netobj, mygw_EVR, vsx_netobj, embedded_device, 
		// host_plain, interface, network, network_object_group, group_with_exception, gsn_handover_group, address_range

		// Service object types:
    	// SecureTrack API element <class_name> includes one of the following types:
    	// icmp_service, service_group, sctp_service, other_service, tcp_service, udp_service



		// todo: move to template settings?
		private readonly string IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}";
		private readonly string ObjectTemplate = "{\"@type\": \"object\", \"name\": \"@@OBJECTNAME@@\", \"object_UID\": \"@@OBJECT_UID@@\", \"object_type\": \"network\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}";
		private readonly string HostTemplateWithId = "{\"@type\": \"host\", \"name\": \"@@HOSTNAME@@\", \"object_UID\": \"@@OBJECT_UID@@\", \"object_type\": \"host\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}";
		private readonly string HostTemplateWithoutId = "{\"@type\": \"host\", \"name\": \"@@HOSTNAME@@\", \"object_type\": \"host\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}";

		private readonly string ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"{@@PROTOCOLID@@}\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"";

		private readonly string NwObjGroupTemplate = "{\"@type\": \"network_object_group\", \"group_name\": \"@@GROUPNAME@@\"}";
		private readonly string SvcGroupTemplate = "{\"@type\": \"service_group\", \"group_name\": \"@@GROUPNAME@@\"}";

		// @type (string, optional): The data type, in cases where object_updated_status is EXISTING_NOT_EDITED or EXISTING_EDITED, use type="Object",
		// protected enum MemberTypes
		// {
		// 	ANY,
		// 	IP,
		// 	DNS,
		// 	Object, // Device's existing object
		// 	INTERNET,
		// 	LDAP,
		// 	host, // ?? not in swagger
		// 	network // ?? not in swagger
		// }

		protected enum StatusValue
		{
			NOT_CHANGED,
			ADDED,
			DELETED
		}

		protected enum ObjStatusValue
		{
			EXISTING_NOT_EDITED,
			EXISTING_EDITED,
			NEW
		}

		public SCTicketTask(WfReqTask reqTask) : base(reqTask)
		{}

		protected string FillIpTemplate(string ipString)
		{
			return IpTemplate.Replace("@@IP@@", ipString);
		}

		protected string FillHostTemplate(string hostname, string objUid, string objDetails, string mgmtId, string comment, string status, string objUpdStatus, bool withId)
		{
			bool shortened = false;
			if(withId)
			{
				return HostTemplateWithId
					.Replace("@@HOSTNAME@@", Sanitizer.SanitizeJsonFieldMand(hostname, ref shortened))
					.Replace("@@OBJECT_UID@@", objUid)
					.Replace("@@OBJECT_DETAILS@@", objDetails)
					.Replace("@@MANAGEMENT_ID@@", mgmtId)
					.Replace("@@COMMENT@@", comment)
					.Replace("@@STATUS@@", status)
					.Replace("@@OBJUPDSTATUS@@", objUpdStatus);
			}
			return HostTemplateWithoutId
				.Replace("@@HOSTNAME@@", Sanitizer.SanitizeJsonFieldMand(hostname, ref shortened))
				.Replace("@@OBJECT_DETAILS@@", objDetails)
				.Replace("@@MANAGEMENT_ID@@", mgmtId)
				.Replace("@@COMMENT@@", comment)
				.Replace("@@STATUS@@", status)
				.Replace("@@OBJUPDSTATUS@@", objUpdStatus);
		}

		protected string FillObjectTemplate(string objName, string objUid, string objDetails, string mgmtId, string comment, string status, string objUpdStatus)
		{
			bool shortened = false;
			return ObjectTemplate
				.Replace("@@OBJECTNAME@@", Sanitizer.SanitizeJsonFieldMand(objName, ref shortened))
				.Replace("@@OBJECT_UID@@", objUid)
				.Replace("@@OBJECT_DETAILS@@", objDetails)
				.Replace("@@MANAGEMENT_ID@@", mgmtId)
				.Replace("@@COMMENT@@", comment)
				.Replace("@@STATUS@@", status)
				.Replace("@@OBJUPDSTATUS@@", objUpdStatus);
		}

		protected string FillServiceTemplate(string protoclId, string port, string serviceName)
		{
			return ServiceTemplate.Replace("@@PROTOCOLID@@", protoclId).Replace("@@PORT@@", port).Replace("@@SERVICENAME@@", serviceName);
		}

		protected string FillNwObjGroupTemplate(string groupName)
		{
			return NwObjGroupTemplate.Replace("@@GROUPNAME@@", groupName);
		}

		protected string FillSvcGroupTemplate(string groupName)
		{
			return SvcGroupTemplate.Replace("@@GROUPNAME@@", groupName);
		}

		protected string ConvertNetworkObjects(string? mgmId)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(ElemFieldType.source);
			List<string> convertedobjects = [];
			foreach(var nwObj in nwObjects)
			{
				convertedobjects.Add(FillHostTemplate(ConstructHostName(nwObj), ConstructHostUid(nwObj),
					nwObj.IpString, mgmId ?? "0", nwObj.Comment ?? "", ObjStatus(nwObj.RequestAction),
					ObjUpdStatus(nwObj.RequestAction, nwObj.NetworkId), nwObj.RequestAction != RequestAction.create.ToString()));
			}
			return "[" + string.Join(",", convertedobjects) + "]";
		}

		private static string ConstructHostName(NwObjectElement nwObj)
		{
			return nwObj.Name ?? "net_" + nwObj.IpString;
		}

		private static string ConstructHostUid(NwObjectElement nwObj)
		{
			return nwObj.NetworkId?.ToString() ?? "host_" + nwObj.IpString;
		}

		private static string ObjStatus(string action)
		{
            return action switch
            {
                nameof(RequestAction.create) => StatusValue.ADDED.ToString(),
                nameof(RequestAction.delete) => StatusValue.DELETED.ToString(),
                nameof(RequestAction.unchanged) => StatusValue.NOT_CHANGED.ToString(),
                _ => "",
            };
        }

		private static string ObjUpdStatus(string action, long? nwObjId)
		{
			return action == nameof(RequestAction.create) && nwObjId == null ? ObjStatusValue.NEW.ToString() : ObjStatusValue.EXISTING_NOT_EDITED.ToString();
            // return action switch
            // {
            //     nameof(RequestAction.create) => ObjStatusValue.NEW.ToString(),
            //     nameof(RequestAction.delete) => ObjStatusValue.EXISTING_NOT_EDITED.ToString(),
            //     nameof(RequestAction.unchanged) => ObjStatusValue.EXISTING_NOT_EDITED.ToString(),
            //     _ => "",
            // };
        }
	}
}
