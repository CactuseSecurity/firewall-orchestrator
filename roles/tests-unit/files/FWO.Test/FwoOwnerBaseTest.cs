using FWO.Basics;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class FwoOwnerBaseTest
    {
        [Test]
        public void GetOwnerResponsiblesByTypeFiltersByTypeId()
        {
            FwoOwnerBase owner = new()
            {
                OwnerResponsibles =
                [
                    new OwnerResponsible { Dn = "cn=main", ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain },
                    new OwnerResponsible { Dn = "cn=group", ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeSupporting }
                ]
            };

            var mainResponsibles = owner.GetOwnerResponsiblesByType(GlobalConst.kOwnerResponsibleTypeMain);

            Assert.That(mainResponsibles, Has.Count.EqualTo(1));
            Assert.That(mainResponsibles[0], Is.EqualTo("cn=main"));
        }
    }
}
