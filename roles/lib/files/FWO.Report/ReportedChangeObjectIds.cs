using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    /// <summary>
    /// Ids of the network objects, services and users a change report shows for one management:
    /// the objects referenced by the old and new versions of the changed rules (including NAT translations)
    /// and the old and new versions of the changed objects themselves.
    /// Object ids identify one version of an object, so both versions of a modified object are contained.
    /// </summary>
    public sealed class ReportedChangeObjectIds
    {
        /// <summary>
        /// Ids of the reported network objects.
        /// </summary>
        public HashSet<long> NetworkObjectIds { get; } = [];

        /// <summary>
        /// Ids of the reported services.
        /// </summary>
        public HashSet<long> ServiceIds { get; } = [];

        /// <summary>
        /// Ids of the reported users.
        /// </summary>
        public HashSet<long> UserIds { get; } = [];

        /// <summary>
        /// True if the report shows no object at all.
        /// </summary>
        public bool IsEmpty => NetworkObjectIds.Count == 0 && ServiceIds.Count == 0 && UserIds.Count == 0;

        /// <summary>
        /// Collects the ids of all objects shown in the change report of a management.
        /// </summary>
        /// <param name="management">Management with the reported changes.</param>
        /// <returns>The collected ids.</returns>
        public static ReportedChangeObjectIds Collect(ManagementReport management)
        {
            ReportedChangeObjectIds ids = new();
            foreach (RuleChange ruleChange in management.RuleChanges ?? [])
            {
                ids.AddRule(ruleChange.OldRule);
                ids.AddRule(ruleChange.NewRule);
            }
            foreach (ObjectChange objectChange in management.ObjectChanges ?? [])
            {
                ids.AddNetworkObject(objectChange.OldObject);
                ids.AddNetworkObject(objectChange.NewObject);
            }
            foreach (ServiceChange serviceChange in management.ServiceChanges ?? [])
            {
                ids.AddService(serviceChange.OldService);
                ids.AddService(serviceChange.NewService);
            }
            foreach (UserChange userChange in management.UserChanges ?? [])
            {
                ids.AddUser(userChange.OldUser);
                ids.AddUser(userChange.NewUser);
            }
            return ids;
        }

        /// <summary>
        /// Collects the ids of the group members of fetched objects which are not contained in the given ids yet.
        /// Direct members and the flattened members (all nesting levels) are collected, so the member links shown
        /// in the group details point to objects of the report tab.
        /// </summary>
        /// <param name="fetchedObjects">Management with the fetched report objects, services and users.</param>
        /// <param name="knownIds">Ids already fetched or to be fetched.</param>
        /// <returns>The ids of the missing members.</returns>
        public static ReportedChangeObjectIds CollectMissingGroupMembers(ManagementReport fetchedObjects, ReportedChangeObjectIds knownIds)
        {
            ReportedChangeObjectIds memberIds = new();
            foreach (NetworkObject networkObject in fetchedObjects.ReportObjects)
            {
                AddMembers(networkObject.ObjectGroups.Select(m => m.Object?.Id), networkObject.ObjectGroupFlats.Select(m => m.Object?.Id),
                    memberIds.NetworkObjectIds, knownIds.NetworkObjectIds);
            }
            foreach (NetworkService service in fetchedObjects.ReportServices)
            {
                AddMembers(service.ServiceGroups.Select(m => m.Object?.Id), service.ServiceGroupFlats.Select(m => m.Object?.Id),
                    memberIds.ServiceIds, knownIds.ServiceIds);
            }
            foreach (NetworkUser user in fetchedObjects.ReportUsers)
            {
                AddMembers(user.UserGroups.Select(m => m.Object?.Id), user.UserGroupFlats.Select(m => m.Object?.Id),
                    memberIds.UserIds, knownIds.UserIds);
            }
            return memberIds;
        }

        private static void AddMembers(IEnumerable<long?> directMemberIds, IEnumerable<long?> flatMemberIds, HashSet<long> target, HashSet<long> known)
        {
            foreach (long? memberId in directMemberIds.Concat(flatMemberIds))
            {
                if (memberId is long id && id > 0 && !known.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        /// <summary>
        /// Network object ids to fetch for the requested object category, empty if the category is not requested.
        /// </summary>
        /// <param name="category">Requested object category.</param>
        /// <returns>The sorted ids.</returns>
        public List<long> GetNetworkObjectIds(ObjCategory category)
        {
            return Includes(category, ObjCategory.nobj) ? [.. NetworkObjectIds.Order()] : new List<long>();
        }

        /// <summary>
        /// Service ids to fetch for the requested object category, empty if the category is not requested.
        /// </summary>
        /// <param name="category">Requested object category.</param>
        /// <returns>The sorted ids.</returns>
        public List<long> GetServiceIds(ObjCategory category)
        {
            return Includes(category, ObjCategory.nsrv) ? [.. ServiceIds.Order()] : new List<long>();
        }

        /// <summary>
        /// User ids to fetch for the requested object category, empty if the category is not requested.
        /// </summary>
        /// <param name="category">Requested object category.</param>
        /// <returns>The sorted ids.</returns>
        public List<long> GetUserIds(ObjCategory category)
        {
            return Includes(category, ObjCategory.user) ? [.. UserIds.Order()] : new List<long>();
        }

        private static bool Includes(ObjCategory requested, ObjCategory category)
        {
            return requested == ObjCategory.all || requested == category;
        }

        private void AddRule(Rule? rule)
        {
            if (rule == null)
            {
                return;
            }
            AddNetworkLocations(rule.Froms);
            AddNetworkLocations(rule.Tos);
            AddServices(rule.Services);
            if (rule.NatData != null)
            {
                AddNetworkLocations(rule.NatData.TranslatedFroms);
                AddNetworkLocations(rule.NatData.TranslatedTos);
                AddServices(rule.NatData.TranslatedServices);
            }
        }

        private void AddNetworkLocations(NetworkLocation[]? locations)
        {
            foreach (NetworkLocation location in locations ?? [])
            {
                AddNetworkObject(location.Object);
                AddUser(location.User);
            }
        }

        private void AddServices(ServiceWrapper[]? services)
        {
            foreach (ServiceWrapper service in services ?? [])
            {
                AddService(service.Content);
            }
        }

        private void AddNetworkObject(NetworkObject? networkObject)
        {
            if (networkObject != null && networkObject.Id > 0)
            {
                NetworkObjectIds.Add(networkObject.Id);
            }
        }

        private void AddService(NetworkService? service)
        {
            if (service != null && service.Id > 0)
            {
                ServiceIds.Add(service.Id);
            }
        }

        private void AddUser(NetworkUser? user)
        {
            if (user != null && user.Id > 0)
            {
                UserIds.Add(user.Id);
            }
        }
    }
}
