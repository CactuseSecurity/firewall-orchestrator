namespace FWO.Data.Flow
{
    public static class FlowAdminHelper
    {
        /// <summary>
        /// Builds the list of unresolved duplicate flow object mappings.
        /// Duplicate groups are grouped by flow object and management.
        /// A group only qualifies if it has multiple object ids and all mappings are inactive.
        /// </summary>
        public static List<FlowNwObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowNwObject>? flowObjects)
        {
            List<FlowNwObjectDuplicateGroup> duplicateGroups = [];

            foreach (FlowNwObject flowObject in flowObjects ?? [])
            {
                IEnumerable<FlowNwObjectMapping> mappings = flowObject.NwObjectMappings ?? [];
                foreach (IGrouping<int, FlowNwObjectMapping> managementGroup in mappings.GroupBy(mapping => mapping.MgmId))
                {
                    List<FlowNwObjectMapping> duplicateMappings = [.. managementGroup
                        .OrderBy(mapping => mapping.Object?.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(mapping => mapping.ObjId)];

                    if (duplicateMappings.Select(mapping => mapping.ObjId).Distinct().Count() <= 1)
                    {
                        continue;
                    }

                    if (duplicateMappings.Any(mapping => mapping.ActiveOnMgm))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowNwObjectDuplicateGroup
                    {
                        FlowNwObjectId = flowObject.Id,
                        FlowNwObjectName = flowObject.Name ?? "",
                        ManagementId = managementGroup.Key,
                        ManagementName = duplicateMappings.FirstOrDefault()?.Management.Name ?? "",
                        Mappings = duplicateMappings
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowNwObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowNwObjectId)
                .ThenBy(group => group.ManagementId)];
        }
    }

    public class FlowNwObjectDuplicateGroup
    {
        public long FlowNwObjectId { get; set; }
        public string FlowNwObjectName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<FlowNwObjectMapping> Mappings { get; set; } = [];
    }
}
