using FWO.Basics;
using FWO.Data;

namespace FWO.Services.Triviality
{
    internal static class RuleTrivialityNormalization
    {
        internal static List<NetworkObject> FlattenRuleNetworkObjects(IEnumerable<NetworkObject> objects)
        {
            return objects
                .SelectMany(obj =>
                    new[] { obj }
                        .Concat(obj.ObjectGroupFlats.Select(groupFlat => groupFlat.Object)))
                .OfType<NetworkObject>()
                .ToList();
        }

        internal static List<NetworkService> FlattenRuleServices(IEnumerable<NetworkService> services)
        {
            return services
                .SelectMany(service =>
                    service.Type.Name == ServiceType.Group
                        ? service.ServiceGroupFlats.Select(groupFlat => groupFlat.Object)
                        : new[] { service })
                .OfType<NetworkService>()
                .Where(service => service.Type.Name != ServiceType.Group)
                .ToList();
        }
    }
}
