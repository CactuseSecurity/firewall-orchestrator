using FWO.Api.Data;
using NetTools;

namespace FWO.Ui.Services
{
	public class NetworkZoneService
	{
		// TODO: remove mockup zones
		public ComplianceNetworkZone[] NetworkZones { get; set; } = new ComplianceNetworkZone[]
		{
			// new ComplianceNetworkZone()
			// {
			// 	Id = 1,
			// 	Name = "Test",
			// 	IPRanges = new IPAddressRange[]
			// 	{
			// 		IPAddressRange.Parse("192.169.0.1"),
			// 		IPAddressRange.Parse("197.167.3.4/32"),
			// 		IPAddressRange.Parse("192.166.7.0/24"),
			// 		IPAddressRange.Parse("192.168.0.0/16"),
			// 	},
			// 	Subzones = new ComplianceNetworkZone[]
			// 	{
			// 		new ComplianceNetworkZone()
			// 		{
			// 			Id = 2,
			// 			Name = "Test_Subzones",
			// 			IPRanges = new IPAddressRange[]
			// 			{
			// 				IPAddressRange.Parse("192.168.0.0/18"),
			// 			}
			// 		}
			// 	}
			// },
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
		public event ZoneAddEventArgs? OnAddZone;
		public event ZoneModificationEventArgs? OnEditZone;
		public event ZoneModificationEventArgs? OnDeleteZone;

		public void InvokeOnAddZone()
		{
			OnAddZone?.Invoke();
		}

		public void InvokeOnEditZone(ComplianceNetworkZone networkZone)
		{
			OnEditZone?.Invoke(networkZone);
		}

		public void InvokeOnDeleteZone(ComplianceNetworkZone networkZone)
		{
			OnDeleteZone?.Invoke(networkZone);
		}
	}
}
