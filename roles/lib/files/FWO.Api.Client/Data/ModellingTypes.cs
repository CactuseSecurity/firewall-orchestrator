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
            Disassign = 5,
            MarkDeleted = 6
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
