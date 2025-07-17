using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ComparerTest
    {
        static readonly ModellingAppServer AppSrv1 = new() { Name = "AppSrv_1", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };
        static readonly ModellingAppServer AppSrv2 = new() { Name = "AppSrv2", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };
        static readonly ModellingAppServer AppSrv3 = new() { Name = "", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };
        static readonly ModellingAppServer AppSrv4 = new() { Name = "AppSrv_1", Ip = "1.1.1.1", IpEnd = "1.1.1.2" };
        static readonly ModellingAppServer AppSrv5 = new() { Name = "AppSrv_1", Ip = "", IpEnd = "" };
        static readonly ModellingAppServer AppSrv6 = new() { Name = "AppSrv/1 ", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };

        static readonly ModellingAppRole AppRole1 = new() { Name = "AppRole1", IdString = "AR1", AppServers = [ new(){Content = AppSrv1} ] };
        static readonly ModellingAppRole AppRole2 = new() { Name = "AppRole2", IdString = "AR2", AppServers = [ new(){Content = AppSrv2} ] };

        static readonly ModellingAppServerWrapper AppSrvWrap1 = new() { Content = AppSrv1 };
        static readonly ModellingAppServerWrapper AppSrvWrap2 = new() { Content = AppSrv2 };
        static readonly ModellingAppServerWrapper AppSrvWrap3 = new() { Content = AppSrv3 };
        static readonly ModellingAppServerWrapper AppSrvWrap4 = new() { Content = AppSrv4 };
        static readonly ModellingAppServerWrapper AppSrvWrap5 = new() { Content = AppSrv5 };
        static readonly ModellingAppServerWrapper AppSrvWrap6 = new() { Content = AppSrv6 };

        static readonly NetworkObject NwObj1 = new() { Name = "NwObj1", IP = "1.2.3.4", IpEnd = "1.2.3.4" };
        static readonly NetworkObject NwObj2 = new() { Name = "NwObj2", IP = "1.2.3.4", IpEnd = "1.2.3.4" };
        static readonly NetworkObject NwObj3 = new() { Name = "NwObj3", IP = "1.2.3.4", IpEnd = "" };
        static readonly NetworkObject NwObj4 = new() { Name = "NwObj4", IP = "1.2.3.5", IpEnd = "1.2.3.4" };
        static readonly NetworkObject NwObj5 = new() { Name = "NwObj1", IP = "", IpEnd = "" };

        static readonly NetworkObject NwGrp1 = new() { Name = "NwGrp1", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj1 }]};
        static readonly NetworkObject NwGrp2 = new() { Name = "NwGrp2", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj1 }]};
        static readonly NetworkObject NwGrp3 = new() { Name = "NwGrp3", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj2 }]};
        static readonly NetworkObject NwGrp4 = new() { Name = "NwGrp4", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj1 }, new GroupFlat<NetworkObject>(){ Object = NwObj2 }]};
        static readonly NetworkObject NwGrp5 = new() { Name = "NwGrp1", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj3 }]};
        static readonly NetworkObject NwGrp6 = new() { Name = "NwGrp1", ObjectGroupFlats = [ new GroupFlat<NetworkObject>(){ Object = NwObj1 }, new GroupFlat<NetworkObject>(){ Object = NwObj3 }]};
        
        static readonly NetworkService Svc1 = new() { Name = "Svc1", DestinationPort = 1234, DestinationPortEnd = 1235, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP"} };
        static readonly NetworkService Svc2 = new() { Name = "Svc2", DestinationPort = 1234, DestinationPortEnd = 1235, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP"} };
        static readonly NetworkService Svc3 = new() { Name = "Svc3", DestinationPort = 1234, DestinationPortEnd = 1236, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP"} };
        static readonly NetworkService Svc4 = new() { Name = "Svc4", DestinationPort = 1235, DestinationPortEnd = 1235, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP"} };
        static readonly NetworkService Svc5 = new() { Name = "Svc5", DestinationPort = 1234, DestinationPortEnd = 1235, ProtoId = 12, Protocol = new() { Id = 12, Name = "PUP"} };
        static readonly NetworkService Svc6 = new() { Name = "Svc1", DestinationPort = 1, DestinationPortEnd = 1, ProtoId = 1, Protocol = new() { Id = 1, Name = "ICMP"} };
        static readonly NetworkService Svc7 = new() { Name = "Svc7", DestinationPort = 1235, DestinationPortEnd = null, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP"}  };

        static readonly NetworkService SvcGrp1 = new() { Name = "SvcGrp1", ServiceGroupFlats = [ new GroupFlat<NetworkService>(){ Object = Svc1 }]};
        static readonly NetworkService SvcGrp2 = new() { Name = "SvcGrp2", ServiceGroupFlats = [ new GroupFlat<NetworkService>(){ Object = Svc1 }]};
        static readonly NetworkService SvcGrp3 = new() { Name = "SvcGrp3", ServiceGroupFlats = [ new GroupFlat<NetworkService>(){ Object = Svc2 }]};
        static readonly NetworkService SvcGrp4 = new() { Name = "SvcGrp4", ServiceGroupFlats = [ new GroupFlat<NetworkService>(){ Object = Svc1 }, new GroupFlat<NetworkService>(){ Object = Svc2 }]};
        static readonly NetworkService SvcGrp5 = new() { Name = "SvcGrp1", ServiceGroupFlats = [ new GroupFlat<NetworkService>(){ Object = Svc3 }]};


        [SetUp]
        public void Initialize()
        {
            
        }

        [Test]
        public void TestAppServerComparer()
        {
            ModellingNamingConvention namingConvention = new()
            {
                // NetworkAreaRequired = false,
                // UseAppPart = false,
                // FixedPartLength = 0,
                // FreePartLength = 0,
                // NetworkAreaPattern = "",
                // AppRolePattern = "",
                // AppZone = "",
                AppServerPrefix = "",
                NetworkPrefix = "",
                IpRangePrefix = ""
            };
            AppServerComparer appServerComparer = new(namingConvention);

            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv1));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrv1,AppSrv2));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrv1,AppSrv3));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv4));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv5));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv6));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv1));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv2));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv3));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv4));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv5));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv6));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap1));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap2));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap3));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap4));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap5));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap6));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap1));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap2));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap3));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap4));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap5));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap6));

            namingConvention.AppServerPrefix = "host_";
            namingConvention.NetworkPrefix = "net_";
            namingConvention.IpRangePrefix = "range_";
            appServerComparer = new(namingConvention);

            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv1));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrv1,AppSrv2));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrv1,AppSrv3));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv4));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv5));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrv1,AppSrv6));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv1));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv2));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv3));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv4));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv5));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv6));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap1));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap2));
            ClassicAssert.AreEqual(false, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap3));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap4));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap5));
            ClassicAssert.AreEqual(true, appServerComparer.Equals(AppSrvWrap1,AppSrvWrap6));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap1));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap2));
            ClassicAssert.AreEqual(false, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap3));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap4));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap5));
            ClassicAssert.AreEqual(true, appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap6));
        }

        [Test]
        public void TestAppRoleComparer()
        {
            AppRoleComparer appRoleComparer = new();

            ClassicAssert.AreEqual(true, appRoleComparer.Equals(AppRole1,AppRole1));
            ClassicAssert.AreEqual(false, appRoleComparer.Equals(AppRole1,AppRole2));
            ClassicAssert.AreEqual(true, appRoleComparer.GetHashCode(AppRole1) == appRoleComparer.GetHashCode(AppRole1));
            ClassicAssert.AreEqual(false, appRoleComparer.GetHashCode(AppRole1) == appRoleComparer.GetHashCode(AppRole2));
        }

        [Test]
        public void TestNetworkObjectComparer()
        {
            RuleRecognitionOption ruleRecognitionOption = new()
            {
                NwRegardIp = true,
                NwRegardName = false,
                // NwRegardGroupName = false,
                // NwResolveGroup = false,
                // NwSeparateGroupAnalysis = true,
                // SvcRegardPortAndProt = true,
                // SvcRegardName = false,
                // SvcRegardGroupName  = false,
                // SvcResolveGroup = false
            };
            NetworkObjectComparer networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectComparer.Equals(NwObj1,NwObj1));
            ClassicAssert.AreEqual(true, networkObjectComparer.Equals(NwObj1,NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj4));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj5));
            ClassicAssert.AreEqual(true, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.AreEqual(true, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));

            ruleRecognitionOption.NwRegardName = true;
            networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectComparer.Equals(NwObj1,NwObj1));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj4));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj5));
            ClassicAssert.AreEqual(true, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));

            ruleRecognitionOption.NwRegardIp = false;
            networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectComparer.Equals(NwObj1,NwObj1));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.Equals(NwObj1,NwObj4));
            ClassicAssert.AreEqual(true, networkObjectComparer.Equals(NwObj1,NwObj5));
            ClassicAssert.AreEqual(true, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.AreEqual(false, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.AreEqual(true, networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));
        }

        [Test]
        public void TestNetworkObjectGroupComparer()
        {
            RuleRecognitionOption ruleRecognitionOption = new()
            {
                NwRegardIp = true,
                NwRegardName = false,
                NwRegardGroupName = false,
                // NwResolveGroup = false,
                NwSeparateGroupAnalysis = true,
                // SvcRegardPortAndProt = true,
                // SvcRegardName = false,
                // SvcRegardGroupName  = false,
                // SvcResolveGroup = false
            };
            NetworkObjectGroupFlatComparer networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardName = true;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardGroupName = true;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwSeparateGroupAnalysis = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardGroupName = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardName = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.Equals(NwGrp1,NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.Equals(NwGrp1,NwGrp6));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.AreEqual(true, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.AreEqual(false, networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));
        }

        [Test]
        public void TestNetworkServiceComparer()
        {
            RuleRecognitionOption ruleRecognitionOption = new()
            {
                // NwRegardIp = true,
                // NwRegardName = false,
                // NwRegardGroupName = false,
                // NwResolveGroup = false,
                // NwSeparateGroupAnalysis = true,
                SvcRegardPortAndProt = true,
                SvcRegardName = false,
                // SvcRegardGroupName  = false,
                // SvcResolveGroup = false
            };
            NetworkServiceComparer networkServiceComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc1,Svc1));
            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc1,Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc5));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc6));
            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc4,Svc7));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc4) == networkServiceComparer.GetHashCode(Svc7));

            ruleRecognitionOption.SvcRegardName = true;
            networkServiceComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc1,Svc1));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc5));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc6));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));

            ruleRecognitionOption.SvcRegardPortAndProt = false;
            networkServiceComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc1,Svc1));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.Equals(Svc1,Svc5));
            ClassicAssert.AreEqual(true, networkServiceComparer.Equals(Svc1,Svc6));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.AreEqual(false, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.AreEqual(true, networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));
        }

        [Test]
        public void TestNetworkServiceGroupComparer()
        {
            RuleRecognitionOption ruleRecognitionOption = new()
            {
                // NwRegardIp = true,
                // NwRegardName = false,
                // NwRegardGroupName = false,
                // NwResolveGroup = false,
                // NwSeparateGroupAnalysis = true,
                SvcRegardPortAndProt = true,
                SvcRegardName = false,
                SvcRegardGroupName  = false,
                // SvcResolveGroup = false
            };
            NetworkServiceGroupComparer networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp1));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp2));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp5));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));

            ruleRecognitionOption.SvcRegardName = true;
            networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp1));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp2));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp5));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));

            ruleRecognitionOption.SvcRegardGroupName = true;
            networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.AreEqual(true, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp1));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp2));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.Equals(SvcGrp1,SvcGrp5));
            ClassicAssert.AreEqual(true, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.AreEqual(false, networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));
        }
    }
}
