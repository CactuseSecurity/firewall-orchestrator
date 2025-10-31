using NetTools;

namespace FWO.Data
{
    public class Cidr
    {
        private IPAddressRange IpRange { get; set; } = new IPAddressRange();

        public bool Valid { get; set; } = false;

        public string CidrString         
        {
            get => this.GetCidrString();
            set => this.SetCidrFromString(value);
        }

        public Cidr()
        {}

        public Cidr(string value)
        {
            this.SetCidrFromString(value);
        }

        private string GetCidrString()
        {
            return Valid ? IpRange.ToCidrString() : "";
        }

        private void SetCidrFromString(string value)
        {
            try
            {
                IpRange = IPAddressRange.Parse(value);
                Valid = true;
                // we only want cidr
                try
                {
                    IpRange.GetPrefixLength();
                }
                catch(Exception)
                {
                    // ignore range end
                    IpRange.End = IpRange.Begin;
                }
            }
            catch(Exception)
            {
                Valid = false;
            }
        }

        public bool IsV6()
        {
            return CidrString.Contains(':');
        }
        public bool IsV4()
        {
            return !IsV6();
        }
    }
}
