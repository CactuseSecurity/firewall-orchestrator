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
            Reactivate = 7,
            Replace = 8
        }

        public enum ModObjectType
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

        public static bool IsNwGroup(this ModObjectType objectType)
        {
            switch(objectType)
            {
                case ModObjectType.AppRole:
                case ModObjectType.AppZone:
                case ModObjectType.NetworkZone:
                case ModObjectType.NetworkArea:
                    return true;
                default: 
                    return false;
            }
        }

        public static bool IsNwObject(this ModObjectType objectType)
        {
            switch(objectType)
            {
                case ModObjectType.AppServer:
                case ModObjectType.Network:
                    return true;
                default: 
                    return false;
            }
        }
    }

    public class AppServerType
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = "";
    }
}
