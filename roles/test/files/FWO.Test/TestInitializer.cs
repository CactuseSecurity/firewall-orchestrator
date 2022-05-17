using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Test
{
    [SetUpFixture]
    class TestInitializer
    {
        [OneTimeSetUp]
        public void OnStart()
        {

        }

        [OneTimeTearDown]
        public void OnFinish()
        {

        }
    }
}
