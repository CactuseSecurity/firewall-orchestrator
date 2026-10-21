using FWO.Ui.Services;
using NUnit.Framework;
using SoPro.FancyTable.Components;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FancyTableTextFactoryTest
    {
        [Test]
        public void Create_MapsEveryTextToItsLocalizationKey()
        {
            FancyTableTexts texts = FancyTableTextFactory.Create(new EchoUserConfig());

            Assert.Multiple(() =>
            {
                Assert.That(texts.SearchPlaceholder, Is.EqualTo("search"));
                Assert.That(texts.RowsPerPageLabel, Is.EqualTo("rows_per_page"));
                Assert.That(texts.AllItemsLabel, Is.EqualTo("all"));
                Assert.That(texts.HiddenColumnsLabel, Is.EqualTo("hidden_columns"));
                Assert.That(texts.PaginationAriaLabel, Is.EqualTo("pagination"));
                Assert.That(texts.PreviousPageLabel, Is.EqualTo("previous"));
                Assert.That(texts.NextPageLabel, Is.EqualTo("next"));
                Assert.That(texts.NoRowsLabel, Is.EqualTo("no_rows"));
                Assert.That(texts.NoMatchingRowsFormat, Is.EqualTo("no_matching_rows"));
                Assert.That(texts.NoMatchingRowsWithTotalFormat, Is.EqualTo("no_matching_rows_with_total"));
                Assert.That(texts.ShowingItemsFormat, Is.EqualTo("showing_items_format"));
                Assert.That(texts.ShowingFilteredItemsFormat, Is.EqualTo("showing_filtered_items_format"));
                Assert.That(texts.ExpandLabel, Is.EqualTo("expand"));
                Assert.That(texts.CollapseLabel, Is.EqualTo("collapse"));
                Assert.That(texts.NoMatchingRootItemsFormat, Is.EqualTo("no_matching_root_items_format"));
                Assert.That(texts.ShowingVisibleRowsFormat, Is.EqualTo("showing_visible_rows_format"));
                Assert.That(texts.ResizeColumnFormat, Is.EqualTo("resize_column_format"));
                Assert.That(texts.ReorderColumnFormat, Is.EqualTo("reorder_column_format"));
            });
        }

        private sealed class EchoUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key;
            }
        }
    }
}
