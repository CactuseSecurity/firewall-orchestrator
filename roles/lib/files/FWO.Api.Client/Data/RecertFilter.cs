using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;

namespace FWO.Api.Data
{

    public class RecertFilter
    {
        public bool RecertOverdueOnly = false;
        public bool RecertWithoutOwner = false;
        public bool RecertShowAnyMatch = false;
        public bool RecertSingleLinePerRule = false;

        public FwoOwner RecertOwner = null;

        public RecertFilter(FwoOwner owner = null, bool recertOverdueOnly = false, bool recertWithoutOwner = false, bool recertShowAnyMatch = false, bool recertSingleLinePerRule = false)
        {
            RecertOverdueOnly = recertOverdueOnly;
            RecertWithoutOwner = recertWithoutOwner;
            RecertShowAnyMatch = recertShowAnyMatch;
            RecertSingleLinePerRule = recertSingleLinePerRule;
            RecertOwner = owner;
        }
    }
}
