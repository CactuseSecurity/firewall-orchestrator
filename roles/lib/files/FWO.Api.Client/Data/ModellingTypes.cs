namespace FWO.Api.Data
{
    public static class ModellingTypes
    {
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

            AppServer = 10,
            Network = 11,

            AppRole = 20,
            AppZone = 21,
            NetworkZone = 22,
            NetworkArea = 23,

            Service = 30,
            ServiceGroup = 31,
        }

        public static bool IsNwGroup(this ObjectType objectType)
        {
            switch(objectType)
            {
                case ObjectType.AppRole:
                case ObjectType.AppZone:
                case ObjectType.NetworkZone:
                case ObjectType.NetworkArea:
                    return true;
                default: 
                    return false;
            }
        }

        public static bool IsNwObject(this ObjectType objectType)
        {
            switch(objectType)
            {
                case ObjectType.AppServer:
                case ObjectType.Network:
                    return true;
                default: 
                    return false;
            }
        }

    }
}
