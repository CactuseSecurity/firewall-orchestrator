using FWO.Basics;
using NetTools;
using System.Net;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Rules of the object_create and object_modify request tasks, which order exactly one network object
    /// (host, network, address range) or service standing alone without a group.
    /// An object_create task holds one element with request action create. An object_modify task holds the
    /// state of the referenced imported object as unchanged element and the requested state as modify element,
    /// both referencing the same object, as rule_modify keeps the rule content as unchanged elements.
    /// </summary>
    public static class WfObjectTaskHelper
    {
        /// <summary>
        /// Minimum length of a search text before the object search is sent to the api.
        /// </summary>
        public const int kMinSearchLength = 3;

        /// <summary>
        /// Maximum number of search hits, matching the row limit of the requester permission.
        /// </summary>
        public const int kSearchLimit = 50;

        /// <summary>
        /// Object types offered by the search: network, host, machines_range and ip_range (see stm_obj_typ).
        /// </summary>
        public static readonly List<int> NetworkObjectTypeIds = [1, 3, 4, 12];

        /// <summary>
        /// Service types offered by the search: simple services only (see stm_svc_typ).
        /// </summary>
        public static readonly List<int> ServiceTypeIds = [1];

        /// <summary>
        /// Determines whether the task type orders a single object.
        /// </summary>
        public static bool IsObjectTask(string? taskType)
        {
            return taskType == WfTaskType.object_create.ToString() || taskType == WfTaskType.object_modify.ToString();
        }

        /// <summary>
        /// Determines whether the task type orders a single object.
        /// </summary>
        public static bool IsObjectTask(WfTaskType taskType)
        {
            return taskType == WfTaskType.object_create || taskType == WfTaskType.object_modify;
        }

        /// <summary>
        /// Converts an imported network object into a request element referencing it.
        /// </summary>
        public static NwObjectElement ToNwObjectElement(NetworkObject networkObject, string requestAction, long taskId)
        {
            NwObjectElement element = new(networkObject.IP ?? "", taskId)
            {
                NetworkId = networkObject.Id,
                Name = networkObject.Name,
                RequestAction = requestAction
            };
            if (!string.IsNullOrWhiteSpace(networkObject.IpEnd) && networkObject.IpEnd != networkObject.IP)
            {
                element.IpEndString = networkObject.IpEnd;
            }
            return element;
        }

        /// <summary>
        /// Converts an imported service into a request element referencing it.
        /// </summary>
        public static NwServiceElement ToNwServiceElement(NetworkService service, string requestAction, long taskId)
        {
            return new()
            {
                TaskId = taskId,
                ServiceId = service.Id,
                Name = service.Name,
                ProtoId = service.ProtoId ?? 0,
                HasProtocol = service.ProtoId != null,
                Port = service.DestinationPort,
                PortEnd = service.DestinationPortEnd,
                RequestAction = requestAction
            };
        }

        /// <summary>
        /// Checks that the network element carries a valid address, network or address range.
        /// </summary>
        public static bool HasNetworkMinimum(NwObjectElement element)
        {
            return element.Cidr.Valid && (string.IsNullOrEmpty(element.IpEndString) || element.CidrEnd.Valid);
        }

        /// <summary>
        /// Checks that the service element carries a protocol and, for protocols with ports, a valid port or port range.
        /// </summary>
        public static bool HasServiceMinimum(NwServiceElement element)
        {
            if (!element.HasProtocol || element.ProtoId < 0)
            {
                return false;
            }
            if (!IpProtocol.HasPorts(element.ProtoId))
            {
                return true;
            }
            int portEnd = element.PortEnd ?? element.Port ?? 0;
            return element.Port is >= 1 and <= GlobalConst.kMaxPortNumber
                && portEnd >= element.Port && portEnd <= GlobalConst.kMaxPortNumber;
        }

        /// <summary>
        /// Determines whether the requested network element equals the referenced object, comparing the
        /// covered address range independently of its notation and the name.
        /// </summary>
        public static bool IsUnchanged(NwObjectElement original, NwObjectElement requested)
        {
            return string.Equals(original.Name ?? "", requested.Name ?? "", StringComparison.Ordinal)
                && GetAddressRange(original.IpString, original.IpEndString).Equals(GetAddressRange(requested.IpString, requested.IpEndString));
        }

        /// <summary>
        /// Determines whether the requested service element equals the referenced service.
        /// </summary>
        public static bool IsUnchanged(NwServiceElement original, NwServiceElement requested)
        {
            return string.Equals(original.Name ?? "", requested.Name ?? "", StringComparison.Ordinal)
                && original.HasProtocol == requested.HasProtocol
                && original.ProtoId == requested.ProtoId
                && original.Port == requested.Port
                && (original.PortEnd ?? original.Port) == (requested.PortEnd ?? requested.Port);
        }

        /// <summary>
        /// Returns the element holding the state of the referenced object of an object_modify task.
        /// </summary>
        public static WfReqElement? GetOriginalElement(WfReqTask task)
        {
            return task.Elements.FirstOrDefault(element => element.RequestAction == RequestAction.unchanged.ToString());
        }

        /// <summary>
        /// Returns the element holding the requested state of an object task.
        /// </summary>
        public static WfReqElement? GetRequestedElement(WfReqTask task)
        {
            return task.Elements.FirstOrDefault(element => element.RequestAction == RequestAction.create.ToString()
                || element.RequestAction == RequestAction.modify.ToString());
        }

        /// <summary>
        /// Checks the element structure of an object task: one create element for object_create, one unchanged
        /// and one modify element of the same field referencing the same imported object for object_modify.
        /// </summary>
        public static bool HasValidElementStructure(WfReqTask task)
        {
            if (task.TaskType == WfTaskType.object_create.ToString())
            {
                return task.Elements.Count == 1 && task.Elements[0].RequestAction == RequestAction.create.ToString();
            }
            if (task.TaskType != WfTaskType.object_modify.ToString() || task.Elements.Count != 2)
            {
                return false;
            }
            WfReqElement? original = GetOriginalElement(task);
            WfReqElement? requested = task.Elements.FirstOrDefault(element => element.RequestAction == RequestAction.modify.ToString());
            return original != null && requested != null
                && original.Field == requested.Field
                && ReferencesSameObject(original, requested);
        }

        /// <summary>
        /// Parses the typed address, network (cidr) or address range ("start-end") into a network element.
        /// </summary>
        public static bool TryParseNetworkInput(string input, long taskId, out NwObjectElement element)
        {
            element = new();
            if (!IPAddressRange.TryParse(input.Trim(), out IPAddressRange range))
            {
                return false;
            }
            element = new NwObjectElement(range, taskId);
            if (!element.CidrEnd.Valid)
            {
                element.CidrEnd = element.Cidr;
            }
            return element.Cidr.Valid;
        }

        /// <summary>
        /// Formats a network element the way TryParseNetworkInput reads it back: a single address, a network in
        /// cidr notation or an address range.
        /// </summary>
        public static string FormatNetworkInput(NwObjectElement element)
        {
            (IPAddress? start, IPAddress? end) = GetAddressRange(element.IpString, element.IpEndString);
            if (start == null || end == null)
            {
                return "";
            }
            if (start.Equals(end))
            {
                return start.ToString();
            }
            string range = $"{start}-{end}";
            return IpOperations.GetObjectType(start.ToString(), end.ToString()) == ObjectType.Network
                ? IPAddressRange.Parse(range).ToCidrString()
                : range;
        }

        /// <summary>
        /// Parses a typed port ("443") or port range ("8000-8080").
        /// </summary>
        public static bool TryParsePortInput(string input, out int? port, out int? portEnd)
        {
            port = null;
            portEnd = null;
            string[] parts = input.Trim().Split('-', 2, StringSplitOptions.TrimEntries);
            if (!int.TryParse(parts[0], out int start))
            {
                return false;
            }
            port = start;
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], out int end))
                {
                    port = null;
                    return false;
                }
                portEnd = end;
            }
            return true;
        }

        /// <summary>
        /// Formats a port or port range the way TryParsePortInput reads it back.
        /// </summary>
        public static string FormatPortInput(int? port, int? portEnd)
        {
            if (port == null)
            {
                return "";
            }
            return portEnd != null && portEnd != port ? $"{port}-{portEnd}" : port.ToString() ?? "";
        }

        /// <summary>
        /// Replaces the elements of an object task by the given ones. An existing element keeps its id when a
        /// new element takes over its request action, so that saving updates it in place; all other existing
        /// elements are marked for deletion.
        /// </summary>
        public static void ReplaceElements(WfReqTask task, List<WfReqElement> newElements)
        {
            List<WfReqElement> oldElements = [.. task.Elements];
            foreach (WfReqElement newElement in newElements)
            {
                WfReqElement? reused = oldElements.FirstOrDefault(element => element.Id > 0 && element.RequestAction == newElement.RequestAction);
                if (reused != null)
                {
                    newElement.Id = reused.Id;
                    oldElements.Remove(reused);
                }
                newElement.TaskId = task.Id;
            }
            task.RemovedElements.AddRange(oldElements.Where(element => element.Id > 0));
            task.Elements = newElements;
        }

        private static bool ReferencesSameObject(WfReqElement original, WfReqElement requested)
        {
            return original.Field == ElemFieldType.service.ToString()
                ? original.ServiceId is > 0 && original.ServiceId == requested.ServiceId
                : original.NetworkId is > 0 && original.NetworkId == requested.NetworkId;
        }

        private static (IPAddress? Start, IPAddress? End) GetAddressRange(string ip, string ipEnd)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return (null, null);
            }
            (string start, string end) = IpOperations.SplitIpToRange(ip);
            if (!string.IsNullOrWhiteSpace(ipEnd))
            {
                end = IpOperations.SplitIpToRange(ipEnd).Item2;
            }
            return (ParseAddress(start), ParseAddress(end));
        }

        private static IPAddress? ParseAddress(string address)
        {
            return IPAddress.TryParse(address.StripOffNetmask(), out IPAddress? parsed) ? parsed : null;
        }
    }
}
