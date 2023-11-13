namespace FWO.Api.Data
{
    public static class ModellingTypes
    {
        public enum NwObjType
        {
            AppServer = 1,
            Network = 2
        }

        public enum NwGroupType
        {
            AppRole = 1,
            AppZone = 2,
            NetworkZone = 3,
            NetworkArea = 4
        }

        public enum ConnectionField
        {
            Source = 1,
            Destination = 2
        }

        public enum ChangeType
        {
            Insert = 1,
            Update = 2,
            Delete = 3,
            Assign = 4,
            Unassign = 5,
            MarkDeleted = 6,
            Reactivate = 7
        }

        public enum ObjectType
        {
            Connection = 1,
            AppRole = 2,
            AppServer = 3,
            ServiceGroup = 4,
            Service = 5
        }
    }
}
