using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that imported custom fields that are no flat JSON object of strings do not abort the variance analysis.
    /// </summary>
    [TestFixture]
    internal class ModellingVarianceAnalysisCustomFieldTest
    {
        private const string kMarker = "FWOC";

        [TestCase("{\"FWOC\":\"12\"}", "12")]
        [TestCase("{\"other\":\"12\"}", null)]
        [TestCase("{\"FWOC\":12}", null)]
        [TestCase("{\"FWOC\":{\"nested\":\"x\"}}", null)]
        [TestCase("not json at all", null)]
        public void GetFromCustomField_ReturnsMarkerOrNullWithoutThrowing(string customFields, string? expected)
        {
            SimulatedUserConfig userConfig = new() { ModModelledMarker = kMarker, ModModelledMarkerLocation = MarkerLocation.Customfields };
            ModellingVarianceAnalysis varianceAnalysis = new(new ModellingVarianceAnalysisTestApiConn(), new ExtStateHandler(new ExtStateTestApiConn()),
                userConfig, new FwoOwner { Id = 1, Name = "App1" }, DefaultInit.DoNothing);
            Rule rule = new() { Id = 7, CustomFields = customFields };
            MethodInfo method = typeof(ModellingVarianceAnalysis).GetMethod("GetFromCustomField", BindingFlags.NonPublic | BindingFlags.Instance)!;

            object?[] arguments = [rule];

            object? result = method.Invoke(varianceAnalysis, arguments);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
