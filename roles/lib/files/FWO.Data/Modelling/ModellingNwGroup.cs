using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Basics;

namespace FWO.Data.Modelling
{
    public class ModellingNwGroup : ModellingNwObject
    {
        [JsonProperty("group_type"), JsonPropertyName("group_type")]
        public int GroupType { get; set; }

        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string IdString
        {
            get { return ManagedIdString.Whole; }
            set { ManagedIdString = new (value); }
        }
        public ModellingManagedIdString ManagedIdString { get; set; } = new ();


        public ModellingNwGroup()
        {}

        public ModellingNwGroup(ModellingNwGroup nwGroup) : base(nwGroup)
        {
            GroupType = nwGroup.GroupType;
            IdString = nwGroup.IdString;
            ManagedIdString.NamingConvention = nwGroup.ManagedIdString.NamingConvention;
        }

        public ModellingNwGroup(NetworkObject nwObj, ModellingNamingConvention? namCon = null) : base(nwObj)
        {
            GroupType = MapObjectType(nwObj.Type.Name);
            IdString = nwObj.Name;
            ManagedIdString.NamingConvention = namCon ?? new();
        }

        public override string Display()
        {
            return base.Display() + " (" + IdString + ")";
        }

        public override string DisplayHtml()
        {
            return $"<span><b>{base.DisplayHtml()}</b></span>";
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.NwGroup}\"></span> " + DisplayHtml();
        }

        public virtual NetworkObject ToNetworkObjectGroup(bool IdAsName = false, bool ListMembers = true)
        {
            return new()
            {
                Id = Id,
                Name = IdAsName ? IdString : Name + " (" + IdString + ")",
                Number = Number,
                Type = new NetworkObjectType(){ Name = ObjectType.Group }
            };
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            ManagedIdString.FreePart = Sanitizer.SanitizeMand(ManagedIdString.FreePart, ref shortened);
            return shortened;
        }

        protected static int MapObjectType(string nwObjType)
        {
            return nwObjType switch
            {
                ObjectType.Group => (int)ModellingTypes.ModObjectType.AppRole,
                ObjectType.Network => (int)ModellingTypes.ModObjectType.Network,
                ObjectType.Host or ObjectType.IPRange => (int)ModellingTypes.ModObjectType.AppServer,
                _ => (int)ModellingTypes.ModObjectType.AppRole,
            };
        }
    }
    
    public class ModellingNwGroupWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public virtual ModellingNwGroup Content { get; set; } = new();

        public static ModellingNwGroup[] Resolve(List<ModellingNwGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}
