using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Api.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AppRoleTest
    {
        static readonly string ARName = "ARxx12345-100";
        static readonly ModellingNamingConvention NamingConvention = new()
        {
            NetworkAreaRequired = true, UseAppPart = true, FixedPartLength = 4, FreePartLength = 3, NetworkAreaPattern = "NA", AppRolePattern = "AR"
        };

        static readonly NetworkObject nwObj1 = new()
        {
            Id = 1,
            Name = ARName,
            IP = "",
            IpEnd = "",
            Uid = "XYZ123",
            CreateTime = new(){ Time = new(2024, 10, 13, 8, 1, 0) },
            Type = new(){ Name = ObjectType.Group },
            Comment ="Comment nw1",
            ObjectGroupFlats = 
            [
                new(){ Id = 10, Object = new() 
                {
                    Id = 2,
                    Name = "nwObj2",
                    IP = "111.222.33.44",
                    IpEnd = "111.222.33.88",
                    Uid = "XYZ1234",
                    CreateTime = new(){ Time = new(2024, 10, 13, 8, 2, 0) },
                    Type = new(){ Name = "Network" },
                    Comment ="Comment nw2",
                    Number = 2
                }},
                new(){ Id = 11, Object = new() 
                {
                    Id = 3,
                    Name = "nwObj3",
                    IP = "111.222.33.99",
                    IpEnd = "",
                    Uid = "XYZ1234",
                    CreateTime = new(){ Time = new(2024, 10, 13, 8, 3, 0) },
                    Type = new(){ Name = "Network" },
                    Comment ="Comment nw3",
                    Number = 3
                }},
                new(){ Id = 11, Object = new() 
                {
                    Id = 4,
                    Name = "nwObj4",
                    IP = "",
                    IpEnd = "",
                    Uid = "XYZ1234",
                    CreateTime = new(){ Time = new(2024, 10, 13, 8, 4, 0) },
                    Type = new(){ Name = ObjectType.Group },
                    Comment ="Comment nw4",
                    Number = 4
                }}
            ],
            Number = 1
        };
        static readonly ModellingAppRole ar1 = new(nwObj1, NamingConvention);
        static readonly ModellingAppRole ar2 = new(ar1);
        static readonly ModellingAppRole ar3 = new(nwObj1);
        static readonly ModellingAppRole ar4 = new(ar3);


        static readonly NetworkObject nwObjConverted = ar1.ToNetworkObjectGroup();

        [SetUp]
        public void Initialize()
        {}

        [Test]
        public void TestAppRole()
        {
            ClassicAssert.AreEqual(false, ar1.Sanitize());
            ClassicAssert.AreEqual($"<span class=\"{Icons.AppRole}\"></span> <span><b><span class=\"\" ><span class=\"\">{ar1.Name} ({ar1.IdString})</span></span></b></span>", ar1.DisplayWithIcon());

            ClassicAssert.AreEqual(ARName, ar1.Name);
            ClassicAssert.AreEqual(1, ar1.Number);
            ClassicAssert.AreEqual(ARName, ar1.IdString);
            ClassicAssert.AreEqual(false, ar1.IsDeleted);
            ClassicAssert.AreEqual(1, ar1.Id);
            ClassicAssert.AreEqual("Comment nw1", ar1.Comment);
            ClassicAssert.AreEqual(null, ar1.Creator);
            ClassicAssert.AreEqual("13.10.2024 08:01:00", ar1.CreationDate.ToString());
            ClassicAssert.AreEqual(20, ar1.GroupType);
            ClassicAssert.AreEqual(2, ar1.AppServers.Count);

            ClassicAssert.AreEqual(2, ar1.AppServers[0].Content.Id);
            ClassicAssert.AreEqual("nwObj2", ar1.AppServers[0].Content.Name);
            ClassicAssert.AreEqual(2, ar1.AppServers[0].Content.Number);
            ClassicAssert.AreEqual("111.222.33.44", ar1.AppServers[0].Content.Ip);
            ClassicAssert.AreEqual("111.222.33.88", ar1.AppServers[0].Content.IpEnd);
            ClassicAssert.AreEqual(0, ar1.AppServers[0].Content.CustomType);

            ClassicAssert.AreEqual(3, ar1.AppServers[1].Content.Id);
            ClassicAssert.AreEqual("nwObj3", ar1.AppServers[1].Content.Name);
            ClassicAssert.AreEqual(3, ar1.AppServers[1].Content.Number);
            ClassicAssert.AreEqual("111.222.33.99", ar1.AppServers[1].Content.Ip);
            ClassicAssert.AreEqual("", ar1.AppServers[1].Content.IpEnd);
            ClassicAssert.AreEqual(0, ar1.AppServers[1].Content.CustomType);

            ClassicAssert.AreEqual(1, nwObjConverted.Id);
            ClassicAssert.AreEqual(1, nwObjConverted.Number);
            ClassicAssert.AreEqual(ARName, nwObjConverted.Name);
            ClassicAssert.AreEqual("Comment nw1", nwObjConverted.Comment);
            ClassicAssert.AreEqual(ObjectType.Group, nwObjConverted.Type.Name);
            ClassicAssert.AreEqual("nwObj2|nwObj3", nwObjConverted.MemberNames);

            ClassicAssert.AreEqual(ar2.Id, ar1.Id);
            ClassicAssert.AreEqual(ar2.Name, ar1.Name);
            ClassicAssert.AreEqual(ar2.Number, ar1.Number);
            ClassicAssert.AreEqual(ar2.IdString, ar1.IdString);
            ClassicAssert.AreEqual(ar2.Comment, ar1.Comment);
            ClassicAssert.AreEqual(ar2.Creator, ar1.Creator);
            ClassicAssert.AreEqual(ar2.CreationDate, ar1.CreationDate);
            ClassicAssert.AreEqual(ar2.Area, ar1.Area);
            ClassicAssert.AreEqual(ar2.AppId, ar1.AppId);
            ClassicAssert.AreEqual(ar2.AppServers, ar1.AppServers);

            ClassicAssert.AreEqual(ar3.IdString, ar1.IdString);
            ClassicAssert.AreEqual(ar4.IdString, ar1.IdString);
        }
    }
}
