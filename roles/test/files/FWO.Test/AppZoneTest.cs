using FWO.Api.Client.Queries;
using FWO.Api.Client;
using FWO.Api.Client.Data;
using FWO.Api.Data;
using NUnit.Framework;
using FWO.Config.Api;
using static System.Net.Mime.MediaTypeNames;
using FWO.Ui;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AppZoneTest
    {
        static AppZoneTestApiConnection APIConnection;
        dynamic APP1 = new
        {
            name = "APP05630",
            idString = "APP05630",
            appId = 1,
            comment = "",
            creator = ""
        };

        dynamic APP2 = new
        {
            name = "COM15630",
            idString = "COM15630",
            appId = 2,
            comment = "",
            creator = ""
        };

        dynamic APPFAIL1 = new
        {
            name = "APP1999",
            idString = "APP1999",
            appId = 3,
            comment = "",
            creator = ""
        };

        dynamic APPFAIL2 = new
        {
            name = "COM0999",
            idString = "COM0999",
            appId = 4,
            comment = "",
            creator = ""
        };


        [SetUp]
        public void Initialize()
        {
            APIConnection = new();
            Console.WriteLine();
        }

        [Test]
        [Parallelizable]
        public async Task TestCreateAppZone()
        {
            ReturnId[]? returnIdsAPPFAIL1;
            ReturnId[]? returnIdsAPPFAIL2;

            try
            {
                ReturnId[]? returnIdsAPP1 = ( await APIConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, APP1) ).ReturnIds;
                ReturnId[]? returnIdsAPP2 = ( await APIConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, APP2) ).ReturnIds;

                Assert.That(Equals(returnIdsAPP1[0].NewId, 1));
                Assert.That(Equals(returnIdsAPP2[0].NewId, 1));
            }
            catch (Exception) { }

            bool testAPPFAIL1 = false;
            bool testAPPFAIL2 = false;

            try
            {
                returnIdsAPPFAIL1 = ( await APIConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, APPFAIL1) ).ReturnIds;
            }
            catch (Exception)
            {
                testAPPFAIL1 = true;
            }

            try
            {
                returnIdsAPPFAIL2 = ( await APIConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, APPFAIL2) ).ReturnIds;
            }
            catch (Exception)
            {
                testAPPFAIL2 = true;
            }

            Assert.That(Equals(testAPPFAIL1, true));
            Assert.That(Equals(testAPPFAIL2, true));
        }
    }
}
