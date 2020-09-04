using System.Collections.Generic;

namespace FWO_Auth_Server
{


    public partial class YamlImporter
{
        public class DeserializedObject
    {
        public List<People> people { get; set; }
        public class People
        {
            public string name { get; set; }
            public int age { get; set; }
        }
    }
}
        }
