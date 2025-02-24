﻿using NetTools;

namespace FWO.Data
{
    public class Cidr
    {
        private IPAddressRange IpRange { get; set; } = new IPAddressRange();

        public bool Valid { get; set; } = false;

        public string CidrString         
        {
            get => this.getCidrString();
            set => this.setCidrFromString(value);
        }

        public Cidr()
        {}

        public Cidr(string value)
        {
            this.setCidrFromString(value);
        }

        private string getCidrString()
        {
            return (Valid ? IpRange.ToCidrString() : "");
        }

        private void setCidrFromString(string value)
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
