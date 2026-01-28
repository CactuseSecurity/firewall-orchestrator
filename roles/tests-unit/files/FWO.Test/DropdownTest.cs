using System.Reflection;
using FWO.Ui.Shared;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class DropdownTest
    {
        /// <summary>
        /// Ensures that a global click callback tolerates a missing JS runtime after disposal.
        /// </summary>
        [Test]
        public void OnGlobalClick_DoesNotThrow_WhenJsRuntimeIsNull()
        {
            Dropdown<string> dropdown = new();
            MethodInfo? method = typeof(Dropdown<string>).GetMethod("OnGlobalClick", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);
            Assert.DoesNotThrow(() => method!.Invoke(dropdown, new object[] { "test-element" }));
        }
    }
}
