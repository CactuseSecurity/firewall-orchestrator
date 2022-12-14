using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;

namespace FWO.Api.Data
{

    public class RecertFilter
    {
        public List<int> RecertOwnerList {get; set;} = new List<int>();
        public bool RecertOverdueOnly {get; set;} = false;
        public bool RecertWithoutOwner {get; set;} = false;
        public bool RecertShowAnyMatch {get; set;} = false;
        public bool RecertSingleLinePerRule {get; set;} = false;
    }
}
