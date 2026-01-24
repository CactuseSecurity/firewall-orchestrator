using FWO.Data;
using FWO.Basics;
using FWO.Services.RuleTreeBuilder;

namespace FWO.Test.Mocks;

public class MockRuleTreeBuilder : RuleTreeBuilder
{
    public new static bool CompareTreeItemPosition(ITreeItem<Rule> treeItem,List<int> nextPosition)
    {
        return RuleTreeBuilder.CompareTreeItemPosition(treeItem, nextPosition);
    }
}
