# how to filter 

first version:

```csharp
<CascadingValue Value="collapseSidebarRule">
    <ObjectGroupCollection FetchObjects="FetchContent" Type="30" StartContentDetailed="true" StartCollapsed="false" InputDataType="Rule" Data="selectedItemsRuleReportTable"
                           NameExtractor=@(rule => $"{rule.RulebaseName} - Rule {rule.Id} {rule.Name}")
                           NetworkObjectExtractor="rule => Array.ConvertAll(rule.Froms.Concat(rule.Tos).GroupBy(x => x.Object?.Id).Select(x => x.FirstOrDefault()).ToArray(), location => location.Object)"
                           NetworkServiceExtractor="rule => Array.ConvertAll(rule.Services, wrapper => wrapper.Content)"
                           NetworkUserExtractor="rule => Array.FindAll(Array.ConvertAll(rule.Froms.Concat(rule.Tos).GroupBy(x => x.User?.Id).Select(x => x.FirstOrDefault()).ToArray(), location => location.User), user => user != null)" />
</CascadingValue>
```
Union improvement see <https://docs.microsoft.com/de-de/dotnet/api/system.linq.enumerable.union?view=net-5.0>
