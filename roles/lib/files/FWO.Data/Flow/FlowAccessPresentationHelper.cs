namespace FWO.Data.Flow
{
    public static class FlowAccessPresentationHelper
    {
        /// <summary>
        /// Builds a compact human-readable summary of one access flow.
        /// </summary>
        public static string BuildAccessSummary(FlowAccess access)
        {
            List<string> parts = [];

            string sourceSummary = BuildNameList(
                access.Sources,
                source => source.NwObject?.Name);
            if (sourceSummary != "")
            {
                parts.Add($"S: {sourceSummary}");
            }

            string destinationSummary = BuildNameList(
                access.Destinations,
                destination => destination.NwObject?.Name);
            if (destinationSummary != "")
            {
                parts.Add($"D: {destinationSummary}");
            }

            string serviceSummary = BuildNameList(
                access.Services,
                service => service.SvcObject?.Name);
            if (serviceSummary != "")
            {
                parts.Add($"V: {serviceSummary}");
            }

            string timeSummary = BuildNameList(
                access.TimeObjects,
                timeObject => timeObject.TimeObject?.Name);
            if (timeSummary != "")
            {
                parts.Add($"T: {timeSummary}");
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Builds searchable text for one access flow row.
        /// </summary>
        public static string BuildSearchText(FlowAccess access)
        {
            List<string> parts = [access.Id.ToString(), access.Hash, access.State];

            AddNameList(parts, access.Sources, source => source.NwObject?.Name);
            AddNameList(parts, access.SourceGroups, sourceGroup => sourceGroup.NwGroup?.Name);
            AddNameList(parts, access.Destinations, destination => destination.NwObject?.Name);
            AddNameList(parts, access.DestinationGroups, destinationGroup => destinationGroup.NwGroup?.Name);
            AddNameList(parts, access.Services, service => service.SvcObject?.Name);
            AddNameList(parts, access.ServiceGroups, serviceGroup => serviceGroup.SvcGroup?.Name);
            AddNameList(parts, access.TimeObjects, timeObject => timeObject.TimeObject?.Name);

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).ToLowerInvariant();
        }

        private static void AddNameList<T>(List<string> parts, IEnumerable<T>? items, Func<T, string?> selector)
        {
            string value = BuildNameList(items, selector);
            if (value != "")
            {
                parts.Add(value);
            }
        }

        private static string BuildNameList<T>(IEnumerable<T>? items, Func<T, string?> selector)
        {
            if (items == null)
            {
                return "";
            }

            return string.Join(", ",
                items
                    .Select(selector)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!.Trim()));
        }
    }
}
