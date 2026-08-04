using FWO.Basics;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;

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

        static readonly ModellingAppRole AppRole1 = new() { Name = "AppRole1", IdString = "AR1", AppServers = [new() { Content = AppSrv1 }] };
        static readonly ModellingAppRole AppRole2 = new() { Name = "AppRole2", IdString = "AR2", AppServers = [new() { Content = AppSrv2 }] };

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

        static readonly NetworkObject DynamicObj1 = new() { Name = "DynamicObj1", IP = "0.0.0.0/32", IpEnd = "255.255.255.255/32", Type = new() { Name = ObjectType.DynamicNetObj } };
        static readonly NetworkObject DynamicObj2 = new() { Name = "DynamicObj2", IP = "0.0.0.0/32", IpEnd = "255.255.255.255/32", Type = new() { Name = ObjectType.DynamicNetObj } };
        static readonly NetworkObject DynamicObj1OtherIp = new() { Name = "DynamicObj1", IP = "1.1.1.1", IpEnd = "1.1.1.1", Type = new() { Name = ObjectType.DynamicNetObj } };
        static readonly NetworkObject AccessRoleObj1 = new() { Name = "AccessRoleObj1", IP = "0.0.0.0/32", IpEnd = "0.0.0.0/32", Type = new() { Name = ObjectType.AccessRole } };
        static readonly NetworkObject AccessRoleObj2 = new() { Name = "AccessRoleObj2", IP = "0.0.0.0/32", IpEnd = "0.0.0.0/32", Type = new() { Name = ObjectType.AccessRole } };

        static readonly NetworkObject NonSpecialObjSameNameAndIp = new()
        {
            Name = "DynamicObj1",
            IP = "0.0.0.0/32",
            IpEnd = "255.255.255.255/32",
            Type = new() { Name = ObjectType.Host }
        };

        static readonly NetworkObject NwGrp1 = new() { Name = "NwGrp1", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj1 }] };
        static readonly NetworkObject NwGrp2 = new() { Name = "NwGrp2", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj1 }] };
        static readonly NetworkObject NwGrp3 = new() { Name = "NwGrp3", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj2 }] };
        static readonly NetworkObject NwGrp4 = new() { Name = "NwGrp4", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj1 }, new GroupFlat<NetworkObject>() { Object = NwObj2 }] };
        static readonly NetworkObject NwGrp5 = new() { Name = "NwGrp1", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj3 }] };
        static readonly NetworkObject NwGrp6 = new() { Name = "NwGrp1", ObjectGroupFlats = [new GroupFlat<NetworkObject>() { Object = NwObj1 }, new GroupFlat<NetworkObject>() { Object = NwObj3 }] };

        static readonly GroupFlat<NetworkObject>[] DynamicObj1GroupFlats =
        {
            new GroupFlat<NetworkObject>() { Object = DynamicObj1 }
        };

        static readonly GroupFlat<NetworkObject>[] DynamicObj2GroupFlats =
        {
            new GroupFlat<NetworkObject>() { Object = DynamicObj2 }
        };

        static readonly NetworkObject NwGrpWithDynamicObj1 = new()
        {
            Name = "NwGrpWithDynamicObj1",
            ObjectGroupFlats = DynamicObj1GroupFlats
        };

        static readonly NetworkObject NwGrpWithDynamicObj2 = new()
        {
            Name = "NwGrpWithDynamicObj2",
            ObjectGroupFlats = DynamicObj2GroupFlats
        };

        static readonly ModellingAppZone AppZone1 = new() { IdString = "AZ1", AppServers = [] };
        static readonly ModellingAppZone AppZone2 = new() { IdString = "AZ2", AppServers = [new() { Content = AppSrv1 }] };
        static readonly ModellingAppZone AppZone3 = new() { IdString = "AZ3", AppServers = [new() { Content = AppSrv1 }, new() { Content = AppSrv2 }] };
        static readonly ModellingAppZone AppZone4 = new() { IdString = "AZ3", AppServers = [new() { Content = AppSrv1 }] };
        static readonly ModellingAppZone AppZone5 = new() { IdString = "AZ3", AppServers = [new() { Content = AppSrv1 }], Comment = "comment", AppId = 3 };

        static readonly NetworkService Svc1 = new() { Name = "Svc1", DestinationPort = 1234, DestinationPortEnd = 1235, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP" } };
        static readonly NetworkService Svc2 = new() { Name = "Svc2", DestinationPort = 1234, DestinationPortEnd = 1235, Protocol = new() { Id = 6, Name = "TCP" } };
        static readonly NetworkService Svc3 = new() { Name = "Svc3", DestinationPort = 1234, DestinationPortEnd = 1236, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP" } };
        static readonly NetworkService Svc4 = new() { Name = "Svc4", DestinationPort = 1235, DestinationPortEnd = 1235, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP" } };
        static readonly NetworkService Svc5 = new() { Name = "Svc5", DestinationPort = 1234, DestinationPortEnd = 1235, ProtoId = 12, Protocol = new() { Id = 12, Name = "PUP" } };
        static readonly NetworkService Svc6 = new() { Name = "Svc1", DestinationPort = 1, DestinationPortEnd = 1, ProtoId = 1, Protocol = new() { Id = 1, Name = "ICMP" } };
        static readonly NetworkService Svc7 = new() { Name = "Svc7", DestinationPort = 1235, DestinationPortEnd = null, ProtoId = 6, Protocol = new() { Id = 6, Name = "TCP" } };
        static readonly NetworkService Svc8 = new() { Name = "", DestinationPort = null, DestinationPortEnd = null, ProtoId = 50 };
        static readonly NetworkService Svc9 = new() { Name = "", DestinationPort = null, DestinationPortEnd = null, ProtoId = 50, Protocol = new() { Id = 50, Name = "ESP" } };
        static readonly NetworkService Svc10 = new() { Name = "", DestinationPort = null, DestinationPortEnd = null };

        static readonly NetworkService SvcGrp1 = new() { Name = "SvcGrp1", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = Svc1 }] };
        static readonly NetworkService SvcGrp2 = new() { Name = "SvcGrp2", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = Svc1 }] };
        static readonly NetworkService SvcGrp3 = new() { Name = "SvcGrp3", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = Svc2 }] };
        static readonly NetworkService SvcGrp4 = new() { Name = "SvcGrp4", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = Svc1 }, new GroupFlat<NetworkService>() { Object = Svc2 }] };
        static readonly NetworkService SvcGrp5 = new() { Name = "SvcGrp1", ServiceGroupFlats = [new GroupFlat<NetworkService>() { Object = Svc3 }] };


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

            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv1));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrv1, AppSrv2));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrv1, AppSrv3));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv4));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv5));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv6));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv1));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv2));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv3));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv4));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv5));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv6));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap1));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap2));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap3));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap4));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap5));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap6));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap1));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap2));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap3));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap4));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap5));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap6));

            namingConvention.AppServerPrefix = "host_";
            namingConvention.NetworkPrefix = "net_";
            namingConvention.IpRangePrefix = "range_";
            appServerComparer = new(namingConvention);

            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv1));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrv1, AppSrv2));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrv1, AppSrv3));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv4));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv5));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrv1, AppSrv6));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv1));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv2));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv3));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv4));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv5));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrv1) == appServerComparer.GetHashCode(AppSrv6));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap1));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap2));
            ClassicAssert.IsFalse(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap3));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap4));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap5));
            ClassicAssert.IsTrue(appServerComparer.Equals(AppSrvWrap1, AppSrvWrap6));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap1));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap2));
            ClassicAssert.IsFalse(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap3));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap4));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap5));
            ClassicAssert.IsTrue(appServerComparer.GetHashCode(AppSrvWrap1) == appServerComparer.GetHashCode(AppSrvWrap6));
        }

        [Test]
        public void TestAppRoleComparer()
        {
            AppRoleComparer appRoleComparer = new();

            ClassicAssert.IsTrue(appRoleComparer.Equals(AppRole1, AppRole1));
            ClassicAssert.IsFalse(appRoleComparer.Equals(AppRole1, AppRole2));
            ClassicAssert.IsTrue(appRoleComparer.GetHashCode(AppRole1) == appRoleComparer.GetHashCode(AppRole1));
            ClassicAssert.IsFalse(appRoleComparer.GetHashCode(AppRole1) == appRoleComparer.GetHashCode(AppRole2));
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

            ClassicAssert.IsTrue(networkObjectComparer.Equals(NwObj1, NwObj1));
            ClassicAssert.IsTrue(networkObjectComparer.Equals(NwObj1, NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj4));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj5));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));

            ruleRecognitionOption.NwRegardName = true;
            networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectComparer.Equals(NwObj1, NwObj1));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj4));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj5));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));

            ruleRecognitionOption.NwRegardIp = false;
            networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectComparer.Equals(NwObj1, NwObj1));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(NwObj1, NwObj4));
            ClassicAssert.IsTrue(networkObjectComparer.Equals(NwObj1, NwObj5));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj1));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj2));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj3));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj4));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(NwObj1) == networkObjectComparer.GetHashCode(NwObj5));

            ruleRecognitionOption.NwRegardIp = true;
            ruleRecognitionOption.NwRegardName = false;
            networkObjectComparer = new(ruleRecognitionOption);

            ClassicAssert.IsFalse(networkObjectComparer.Equals(DynamicObj1, DynamicObj2));
            ClassicAssert.IsTrue(networkObjectComparer.Equals(DynamicObj1, DynamicObj1OtherIp));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(AccessRoleObj1, AccessRoleObj2));
            ClassicAssert.IsFalse(networkObjectComparer.Equals(DynamicObj1, NonSpecialObjSameNameAndIp));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(DynamicObj1) == networkObjectComparer.GetHashCode(DynamicObj2));
            ClassicAssert.IsTrue(networkObjectComparer.GetHashCode(DynamicObj1) == networkObjectComparer.GetHashCode(DynamicObj1OtherIp));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(AccessRoleObj1) == networkObjectComparer.GetHashCode(AccessRoleObj2));
            ClassicAssert.IsFalse(networkObjectComparer.GetHashCode(DynamicObj1) == networkObjectComparer.GetHashCode(NonSpecialObjSameNameAndIp));
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

            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardName = true;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardGroupName = true;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwSeparateGroupAnalysis = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardGroupName = false;
            ruleRecognitionOption.NwRegardName = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrpWithDynamicObj1, NwGrpWithDynamicObj2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrpWithDynamicObj1) == networkObjectGroupComparer.GetHashCode(NwGrpWithDynamicObj2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));

            ruleRecognitionOption.NwRegardName = false;
            networkObjectGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.Equals(NwGrp1, NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.Equals(NwGrp1, NwGrp6));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp1));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp2));
            ClassicAssert.IsTrue(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp3));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp4));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp5));
            ClassicAssert.IsFalse(networkObjectGroupComparer.GetHashCode(NwGrp1) == networkObjectGroupComparer.GetHashCode(NwGrp6));
        }

        [Test]
        public void TestAppZoneComparer()
        {
            ModellingNamingConvention namingConvention = new()
            {
                AppZone = "AZ"
            };
            AppZoneComparer appZoneComparer = new(namingConvention);

            ClassicAssert.IsTrue(appZoneComparer.Equals(AppZone1, AppZone1));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone1, AppZone2));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone1, AppZone3));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone1, AppZone4));
            ClassicAssert.IsTrue(appZoneComparer.Equals(AppZone2, AppZone2));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone2, AppZone3));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone2, AppZone4));
            ClassicAssert.IsFalse(appZoneComparer.Equals(AppZone3, AppZone4));
            ClassicAssert.IsTrue(appZoneComparer.Equals(AppZone4, AppZone5));

            ClassicAssert.IsTrue(appZoneComparer.GetHashCode(AppZone1) == appZoneComparer.GetHashCode(AppZone1));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone1) == appZoneComparer.GetHashCode(AppZone2));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone1) == appZoneComparer.GetHashCode(AppZone3));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone1) == appZoneComparer.GetHashCode(AppZone4));
            ClassicAssert.IsTrue(appZoneComparer.GetHashCode(AppZone2) == appZoneComparer.GetHashCode(AppZone2));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone2) == appZoneComparer.GetHashCode(AppZone3));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone2) == appZoneComparer.GetHashCode(AppZone4));
            ClassicAssert.IsFalse(appZoneComparer.GetHashCode(AppZone3) == appZoneComparer.GetHashCode(AppZone4));
            ClassicAssert.IsTrue(appZoneComparer.GetHashCode(AppZone4) == appZoneComparer.GetHashCode(AppZone5));
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

            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc1, Svc1));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc1, Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc5));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc6));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc4, Svc7));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc5, Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc8, Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc8, Svc10));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc4) == networkServiceComparer.GetHashCode(Svc7));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc5) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc10));

            ruleRecognitionOption.SvcRegardName = true;
            networkServiceComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc1, Svc1));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc5));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc6));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc5, Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc8, Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc8, Svc10));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc5) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc10));

            ruleRecognitionOption.SvcRegardPortAndProt = false;
            networkServiceComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc1, Svc1));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc5));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc1, Svc6));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc1, Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.Equals(Svc5, Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc8, Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.Equals(Svc8, Svc10));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc1));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc2));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc3));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc4));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc5));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc6));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc1) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsFalse(networkServiceComparer.GetHashCode(Svc5) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc9));
            ClassicAssert.IsTrue(networkServiceComparer.GetHashCode(Svc8) == networkServiceComparer.GetHashCode(Svc10));
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
                SvcRegardGroupName = false,
                // SvcResolveGroup = false
            };
            NetworkServiceGroupComparer networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp1));
            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp2));
            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp5));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));

            ruleRecognitionOption.SvcRegardName = true;
            networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp1));
            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp2));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp5));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));

            ruleRecognitionOption.SvcRegardGroupName = true;
            networkServiceGroupComparer = new(ruleRecognitionOption);

            ClassicAssert.IsTrue(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp1));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp2));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.Equals(SvcGrp1, SvcGrp5));
            ClassicAssert.IsTrue(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp1));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp2));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp3));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp4));
            ClassicAssert.IsFalse(networkServiceGroupComparer.GetHashCode(SvcGrp1) == networkServiceGroupComparer.GetHashCode(SvcGrp5));
        }
    }
}
