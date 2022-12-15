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
        public int RecertDisplayPeriod = 0;  // display all overdue rules

        public FwoOwner RecertOwner = null;

        public RecertFilter(FwoOwner owner = null, bool recertOverdueOnly = false, bool recertWithoutOwner = false, bool recertShowAnyMatch = false, bool recertSingleLinePerRule = false, int recertDisplayPeriod = 0)
        {
            RecertOverdueOnly = recertOverdueOnly;
            RecertWithoutOwner = recertWithoutOwner;
            RecertShowAnyMatch = recertShowAnyMatch;
            RecertSingleLinePerRule = recertSingleLinePerRule;
            RecertOwner = owner;
            RecertDisplayPeriod = recertDisplayPeriod;
        }
    }
}
