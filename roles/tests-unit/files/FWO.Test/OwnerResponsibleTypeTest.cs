using FWO.Data;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Generic;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class OwnerResponsibleTypeTest
    {
        [Test]
        public void OwnerResponsiblesAreFilteredByType()
        {
            FwoOwner owner = new();
            owner.AddOwnerResponsible(OwnerResponsibleType.kMainResponsible, "dn-main");
            owner.AddOwnerResponsible(OwnerResponsibleType.kSupportingResponsible, "dn-group");
            owner.AddOwnerResponsible(OwnerResponsibleType.kOptionalEscalationResponsible, "dn-optional");

            ClassicAssert.AreEqual(new List<string> { "dn-main" }, owner.GetOwnerResponsiblesByType(OwnerResponsibleType.kMainResponsible));
            ClassicAssert.AreEqual(new List<string> { "dn-group" }, owner.GetOwnerResponsiblesByType(OwnerResponsibleType.kSupportingResponsible));
            ClassicAssert.AreEqual(new List<string> { "dn-optional" }, owner.GetOwnerResponsiblesByType(OwnerResponsibleType.kOptionalEscalationResponsible));
        }
    }
}
