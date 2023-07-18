using FWO.Api.Data;
using NetTools;

namespace FWO.Ui.Services
{
	public class NetworkZoneService
	{
		public ComplianceNetworkZone[] NetworkZones { get; set; } = new ComplianceNetworkZone[]
		{
			new ComplianceNetworkZone()
			{
				Id = 1,
				Name = "A",
				IPRanges = new IPAddressRange[]
				{
					IPAddressRange.Parse("1.1.1.1"),
				},
			},
			new ComplianceNetworkZone()
			{
				Id = 2,
				Name = "B",
				IPRanges = new IPAddressRange[]
				{
					IPAddressRange.Parse("1.1.1.2"),
				},
			},
			new ComplianceNetworkZone()
			{
				Id = 3,
				Name = "C",
				IPRanges = new IPAddressRange[]
				{
					IPAddressRange.Parse("1.1.1.3"),
				},
			},
		};

		public delegate void ZoneAddEventArgs();
		public delegate void ZoneModificationEventArgs(ComplianceNetworkZone networkZone);
		public event ZoneModificationEventArgs? OnEditZone;
		public event ZoneModificationEventArgs? OnDeleteZone;

		public void InvokeOnEditZone(ComplianceNetworkZone networkZone)
		{
			OnEditZone?.Invoke(networkZone);
		}

		public void InvokeOnDeleteZone(ComplianceNetworkZone networkZone)
		{
			OnDeleteZone?.Invoke(networkZone);
		}

		/// <summary>
		/// Display the IP address range in CIDR notation if possible and it is not a single IP address
		/// otherwise display it in the format "first_ip-last_ip".
		/// </summary>
		/// <param name="ipAddressRange"></param>
		/// <returns>IP address range in CIDR / first-last notation</returns>
		public static string DisplayIpRange(IPAddressRange ipAddressRange)
		{
			try
			{
				int prefixLength = ipAddressRange.GetPrefixLength();
				if (prefixLength != 32)
				{
					return ipAddressRange.ToCidrString();
				}
			}
			catch (FormatException) { }
			return ipAddressRange.ToString();
		}
	}
}
