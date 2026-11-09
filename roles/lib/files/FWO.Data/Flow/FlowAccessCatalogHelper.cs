namespace FWO.Data.Flow
{
    public static class FlowAccessCatalogHelper
    {
        public static List<FlowNwObject> BuildNwObjectCatalog(IEnumerable<FlowAccess>? accesses)
        {
            Dictionary<long, FlowNwObject> uniqueObjects = [];

            foreach (FlowAccess access in accesses ?? [])
            {
                AddObjects(uniqueObjects, access.Sources?.Select(source => source?.NwObject));
                AddObjects(uniqueObjects, access.Destinations?.Select(destination => destination?.NwObject));
                AddObjects(uniqueObjects, access.SourceGroups?.SelectMany(group => group?.NwGroup?.NwGroupMembers?.Select(member => member?.NwObject) ?? []));
                AddObjects(uniqueObjects, access.DestinationGroups?.SelectMany(group => group?.NwGroup?.NwGroupMembers?.Select(member => member?.NwObject) ?? []));
            }

            return [.. uniqueObjects.Values
                .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(nwObject => nwObject.Id)];
        }

        public static void ApplyNwObjectUpdate(IEnumerable<FlowAccess>? accesses, IList<FlowNwObject>? catalog, FlowNwObject updatedObject)
        {
            ReplaceInCatalog(catalog, updatedObject);
            UpdateAccessReferences(accesses, updatedObject);
        }

        private static void AddObjects(Dictionary<long, FlowNwObject> uniqueObjects, IEnumerable<FlowNwObject?>? candidates)
        {
            foreach (FlowNwObject? candidate in candidates ?? [])
            {
                if (candidate == null || candidate.Id <= 0)
                {
                    continue;
                }

                uniqueObjects[candidate.Id] = candidate;
            }
        }

        private static void ReplaceInCatalog(IList<FlowNwObject>? catalog, FlowNwObject updatedObject)
        {
            if (catalog == null)
            {
                return;
            }

            for (int index = 0; index < catalog.Count; index++)
            {
                if (catalog[index].Id == updatedObject.Id)
                {
                    catalog[index] = updatedObject;
                }
            }
        }

        private static void UpdateAccessReferences(IEnumerable<FlowAccess>? accesses, FlowNwObject updatedObject)
        {
            foreach (FlowAccess access in accesses ?? [])
            {
                UpdateObjects(access.Sources?.Select(source => source?.NwObject), updatedObject);
                UpdateObjects(access.Destinations?.Select(destination => destination?.NwObject), updatedObject);

                UpdateObjects(
                    access.SourceGroups?.SelectMany(group => group?.NwGroup?.NwGroupMembers?.Select(member => member?.NwObject) ?? []),
                    updatedObject);
                UpdateObjects(
                    access.DestinationGroups?.SelectMany(group => group?.NwGroup?.NwGroupMembers?.Select(member => member?.NwObject) ?? []),
                    updatedObject);
            }
        }

        private static void UpdateObjects(IEnumerable<FlowNwObject?>? objects, FlowNwObject updatedObject)
        {
            foreach (FlowNwObject? currentObject in objects ?? [])
            {
                if (currentObject == null || currentObject.Id != updatedObject.Id)
                {
                    continue;
                }

                CopyObject(currentObject, updatedObject);
            }
        }

        private static void CopyObject(FlowNwObject target, FlowNwObject source)
        {
            target.Name = source.Name;
            target.IpStart = source.IpStart;
            target.IpEnd = source.IpEnd;
            target.Hash = source.Hash;
            target.State = source.State;
            target.RemovedDate = source.RemovedDate;
            target.ShowInRequestModule = source.ShowInRequestModule;
        }
    }
}
