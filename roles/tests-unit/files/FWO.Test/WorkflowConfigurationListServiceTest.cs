using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Covers the paged, filtered workflow configuration list.</summary>
[TestFixture]
internal class WorkflowConfigurationListServiceTest
{
    [Test]
    public void WithoutCriteriaTheFilterMatchesEveryConfiguration()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkflowConfigurationListService.BuildWhere(null), Is.Empty);
            Assert.That(WorkflowConfigurationListService.BuildWhere(new WorkflowConfigurationFilter()), Is.Empty);
        });
    }

    [Test]
    public void TheFilterHoldsOnlySetCriteriaAndMatchesWildcardsLiterally()
    {
        Dictionary<string, object> where = WorkflowConfigurationListService.BuildWhere(new WorkflowConfigurationFilter
        {
            Id = 4,
            Name = "50%_off",
            Description = "default",
            IsActive = false
        });

        JObject json = JObject.FromObject(where);
        Assert.Multiple(() =>
        {
            Assert.That((int?)json["id"]?["_eq"], Is.EqualTo(4));
            Assert.That((string?)json["name"]?["_ilike"], Is.EqualTo("%50\\%\\_off%"));
            Assert.That((string?)json["description"]?["_ilike"], Is.EqualTo("%default%"));
            Assert.That((bool?)json["is_active"]?["_eq"], Is.False);
        });
    }

    [Test]
    public async Task APageIsReadWithFilterLimitAndOffset()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary> { new() { Id = 1, Name = "Default" } });

        List<WorkflowConfigurationSummary> page = await new WorkflowConfigurationListService(api)
            .GetConfigurationsAsync(new WorkflowConfigurationFilter { IsActive = true }, 11, 20);

        JObject variables = JObject.FromObject(api.Calls.Single().Variables!);
        Assert.Multiple(() =>
        {
            Assert.That(page.Single().Name, Is.EqualTo("Default"));
            Assert.That((int?)variables["limit"], Is.EqualTo(11));
            Assert.That((int?)variables["offset"], Is.EqualTo(20));
            Assert.That((bool?)variables["where"]?["is_active"]?["_eq"], Is.True);
        });
    }
}
