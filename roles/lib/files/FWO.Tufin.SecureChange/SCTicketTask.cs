using FWO.Api.Data;
using FWO.Basics;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

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

		// private readonly string HostTemplateWithId = "{\"@type\": \"host\", \"name\": \"@@HOSTNAME@@\", \"object_UID\": \"@@OBJECT_UID@@\", \"object_type\": \"host\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}";
		// private readonly string HostTemplateWithoutId = "{\"@type\": \"host\", \"name\": \"@@HOSTNAME@@\", \"object_type\": \"host\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"management_id\": @@MANAGEMENT_ID@@, \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}";
		// private readonly string SvcGroupTemplate = "{\"@type\": \"service_group\", \"group_name\": \"@@GROUPNAME@@\"}";


		protected enum SCStatusValue
		{
			NOT_CHANGED,
			ADDED,
			DELETED
		}

		protected enum SCObjStatusValue
		{
			EXISTING_NOT_EDITED,
			EXISTING_EDITED,
			NEW
		}

		protected struct SCObjectType
		{
			public const string Host = "host";
			public const string Network = "network";
		}

		public SCTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention) : base(reqTask, ipProtos, namingConvention)
		{}

		// 	{
		// 		"@type": "Object",
		// 		"name": "xyz1234.xxx.de",
		// 		"object_type": "host",
		// 		"object_details": "1.2.3.4/32",
		// 		"management_id": 1,
		// 		"status": "ADDED",
		// 		"comment": "",
		// 		"object_updated_status": "EXISTING_NOT_EDITED"
		// 	}
		protected static string FillObjectTemplate(ExternalTicketTemplate template, string type, string objName, string ObjType, string objDetails, string comment, 
			string status, string objUpdStatus, string mgmId)
		{
			bool shortened = false;
			return template.ObjectTemplate
				.Replace("@@TYPE@@", type)
				.Replace("@@OBJECTNAME@@", Sanitizer.SanitizeJsonFieldMand(objName, ref shortened))
				.Replace("@@OBJECT_TYPE@@", ObjType)
				.Replace("@@OBJECT_DETAILS@@", objDetails)
				.Replace("@@COMMENT@@", comment)
				.Replace("@@STATUS@@", status)
				.Replace("@@OBJUPDSTATUS@@", objUpdStatus)
				.Replace("@@MANAGEMENT_ID@@", mgmId);
		}

		// 	{
		// 		"@type": "Object",
		// 		"name": "ip_1.2.3.4",
		// 		"management_id": 1,
		// 		"status": "NOT_CHANGED",
		// 		"object_updated_status": "EXISTING_NOT_EDITED"
		// 	}
		protected static string FillObjectTemplateShort(ExternalTicketTemplate template, string objName, string status, string objUpdStatus, string mgmId)
		{
			bool shortened = false;
			return template.ObjectTemplateShort
				.Replace("@@OBJECTNAME@@", Sanitizer.SanitizeJsonFieldMand(objName, ref shortened))
				.Replace("@@STATUS@@", status)
				.Replace("@@OBJUPDSTATUS@@", objUpdStatus)
				.Replace("@@MANAGEMENT_ID@@", mgmId);
		}

		// protected string FillHostTemplate(string hostname, string objUid, string objDetails, string mgmtId, string comment, string status, string objUpdStatus, bool withId)
		// {
		// 	bool shortened = false;
		// 	if(withId)
		// 	{
		// 		return HostTemplateWithId
		// 			.Replace("@@HOSTNAME@@", Sanitizer.SanitizeJsonFieldMand(hostname, ref shortened))
		// 			.Replace("@@OBJECT_UID@@", objUid)
		// 			.Replace("@@OBJECT_DETAILS@@", objDetails)
		// 			.Replace("@@MANAGEMENT_ID@@", mgmtId)
		// 			.Replace("@@COMMENT@@", comment)
		// 			.Replace("@@STATUS@@", status)
		// 			.Replace("@@OBJUPDSTATUS@@", objUpdStatus);
		// 	}
		// 	return HostTemplateWithoutId
		// 		.Replace("@@HOSTNAME@@", Sanitizer.SanitizeJsonFieldMand(hostname, ref shortened))
		// 		.Replace("@@OBJECT_DETAILS@@", objDetails)
		// 		.Replace("@@MANAGEMENT_ID@@", mgmtId)
		// 		.Replace("@@COMMENT@@", comment)
		// 		.Replace("@@STATUS@@", status)
		// 		.Replace("@@OBJUPDSTATUS@@", objUpdStatus);
		// }

		protected static string FillIpTemplate(ExternalTicketTemplate template, string ipString)
		{
			return template.IpTemplate.Replace("@@IP@@", ipString);
		}

		protected static string FillServiceTemplate(ExternalTicketTemplate template, string protocolName, string port, string serviceName)
		{
			return template.ServiceTemplate.Replace("@@PROTOCOLNAME@@", protocolName).Replace("@@PORT@@", port).Replace("@@SERVICENAME@@", serviceName);
		}

		protected static string FillNwObjGroupTemplate(ExternalTicketTemplate template, string groupName, string mgtName)
		{
			return template.NwObjGroupTemplate.Replace("@@GROUPNAME@@", groupName).Replace("@@MANAGEMENT_NAME@@", mgtName);
		}

		// protected string FillSvcGroupTemplate(string groupName)
		// {
		// 	return SvcGroupTemplate.Replace("@@GROUPNAME@@", groupName);
		// }

		protected string ConvertNetworkObjects(ExternalTicketTemplate template, string? mgmId, ModellingNamingConvention? namingConvention)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(ElemFieldType.source);
			List<string> convertedObjects = [];
			foreach(var nwObj in nwObjects)
			{
				if(nwObj.RequestAction == RequestAction.create.ToString() || nwObj.RequestAction == RequestAction.addAfterCreation.ToString())
				{
					string scObjType = GetSCObjectType(DisplayBase.AutoDetectType(nwObj.IpString, nwObj.IpEndString));
					string objUpdStatus = ObjUpdStatus(nwObj.RequestAction);
					convertedObjects.Add(FillObjectTemplate(template, objUpdStatus == SCObjStatusValue.NEW.ToString() ? scObjType : "Object",
						ConstructObjectName(nwObj, namingConvention), scObjType,
                        ConstructObjectIp(nwObj, scObjType), nwObj.Comment ?? "", ObjStatus(nwObj.RequestAction), objUpdStatus, mgmId ?? "0"));
				}
				else
				{
					convertedObjects.Add(FillObjectTemplateShort(template, ConstructObjectName(nwObj, namingConvention),
						ObjStatus(nwObj.RequestAction), ObjUpdStatus(nwObj.RequestAction), mgmId ?? "0"));
				}
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

        private static string ConstructObjectName(NwObjectElement nwObj, ModellingNamingConvention? namingConvention)
        {
        	return string.IsNullOrEmpty(nwObj.Name) ? namingConvention?.AppServerPrefix + nwObj.IpString :
                char.IsLetter(nwObj.Name[0]) ? nwObj.Name : namingConvention?.AppServerPrefix + nwObj.Name;
		}

		private static string ConstructObjectIp(NwObjectElement nwObj, string scObjType)
		{
            return scObjType switch
            {
                "network" => IpOperations.ToDotNotation(nwObj.IpString, nwObj.IpEndString),
                "range" => $"{nwObj.IpString}-{nwObj.IpEndString}",// TODO: not really implemented yet
                _ => nwObj.IpString, // single host
            };
        }

        private static string GetSCObjectType(string fwoObjType)
		{
            return fwoObjType switch
            {
                ObjectType.Host => SCObjectType.Host.ToString(),
                ObjectType.Network => SCObjectType.Network.ToString(),
                ObjectType.IPRange => SCObjectType.Network.ToString(),  // ??? Todo
                _ => "",
            };
        }

		private static string ObjStatus(string action)
		{
            return action switch
            {
                nameof(RequestAction.create) => SCStatusValue.ADDED.ToString(),
				nameof(RequestAction.addAfterCreation) => SCStatusValue.ADDED.ToString(),
                nameof(RequestAction.delete) => SCStatusValue.DELETED.ToString(),
                nameof(RequestAction.unchanged) => SCStatusValue.NOT_CHANGED.ToString(),
                _ => "",
            };
        }

		private static string ObjUpdStatus(string action)
		{
			return action == nameof(RequestAction.create) ? SCObjStatusValue.NEW.ToString() : SCObjStatusValue.EXISTING_NOT_EDITED.ToString();
        }
	}
}
