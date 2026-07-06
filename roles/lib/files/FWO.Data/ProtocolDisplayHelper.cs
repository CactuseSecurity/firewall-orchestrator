using System.Text.Json;

namespace FWO.Data
{
    public static class ProtocolDisplayHelper
    {
        public static List<IpProtocol> CustomSortProtocols(List<IpProtocol> listIn, IEnumerable<string>? reducedProtocolNames, bool reducedProtocolSet)
        {
            List<IpProtocol> remainingProtocols = [.. listIn];
            List<IpProtocol> result = [];

            List<string> preferredProtocolNames = reducedProtocolSet
                ? NormalizeProtocolNames(reducedProtocolNames)
                : [.. ProtocolNames.DefaultReducedProtocolNames];
            if (reducedProtocolSet && preferredProtocolNames.Count == 0)
            {
                preferredProtocolNames = [.. ProtocolNames.DefaultReducedProtocolNames];
            }

            foreach (string protocolName in preferredProtocolNames)
            {
                IpProtocol? protocol = remainingProtocols.Find(x => x.Name.Equals(protocolName, StringComparison.OrdinalIgnoreCase));
                if (protocol != null)
                {
                    result.Add(protocol);
                    remainingProtocols.Remove(protocol);
                }
            }

            if (!reducedProtocolSet)
            {
                foreach (IpProtocol protocol in remainingProtocols
                    .Where(protocol => !protocol.Name.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(protocol => protocol.Name))
                {
                    result.Add(protocol);
                }
            }

            return result;
        }

        public static List<string> ParseProtocolNames(string? configValue)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return [.. ProtocolNames.DefaultReducedProtocolNames];
            }

            try
            {
                List<string>? parsedNames = JsonSerializer.Deserialize<List<string>>(configValue);
                List<string> normalizedNames = NormalizeProtocolNames(parsedNames);
                return normalizedNames.Count > 0 ? normalizedNames : [.. ProtocolNames.DefaultReducedProtocolNames];
            }
            catch (JsonException)
            {
                return [.. ProtocolNames.DefaultReducedProtocolNames];
            }
        }

        public static string SerializeProtocolNames(IEnumerable<string> protocolNames)
        {
            return JsonSerializer.Serialize(NormalizeProtocolNames(protocolNames));
        }

        private static List<string> NormalizeProtocolNames(IEnumerable<string>? protocolNames)
        {
            return protocolNames == null
                ? []
                : [.. protocolNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
        }
    }
}
