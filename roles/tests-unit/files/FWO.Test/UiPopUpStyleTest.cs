using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the style rules the popups depend on. The stylesheets are embedded into this
    /// assembly at build time (see FWO.Test.csproj), so no file of another component is read at runtime.
    /// </summary>
    [TestFixture]
    internal partial class UiPopUpStyleTest
    {
        private const string kPopUpCssResource = "FWO.Test.PopUp.css";
        private const string kSiteCssResource = "FWO.Test.site.css";
        private const string kModalMarginVariable = "--bs-custom-modal-margin-top";
        private const string kModalPaddingVariable = "--fwo-modal-content-padding-top";
        private const int knMilliseconds = 1000;

        /// <summary>
        /// All rules of the popup stylesheet apply to the modal itself, which is an ancestor of every
        /// element inside a popup. A transform on an ancestor becomes the containing block of its
        /// position: fixed descendants and would shift the dropdown menus (Dropdown.razor) inside popups.
        /// </summary>
        [Test]
        public void PopUpCss_DoesNotPositionTheModalWithATransform()
        {
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);

            Assert.That(TransformDeclarationRegex().IsMatch(popUpCss), Is.False,
                "The modal must not use a transform: it would shift every position: fixed element inside the popup.");
        }

        /// <summary>
        /// The modal is centered by the automatic margins of a fixed box spanning the whole viewport.
        /// Without these declarations the popups would be rendered in the upper left corner.
        /// </summary>
        [Test]
        public void PopUpCss_CentersTheModalWithAutomaticMargins()
        {
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);

            Match centerRule = CenterRuleRegex().Match(popUpCss);
            Assert.That(centerRule.Success, Is.True, "The class centering the popups is missing.");

            string declarations = NormalizeWhitespace(centerRule.Groups["body"].Value);
            Assert.Multiple(() =>
            {
                Assert.That(declarations, Does.Contain("position: fixed"));
                Assert.That(declarations, Does.Contain("margin: auto"));
            });
        }

        /// <summary>
        /// An auto sized popup has to leave room for its footer. Reserving that room with a pixel constant
        /// fails as soon as the footer wraps, so the height is distributed by a column flex box instead:
        /// the footer keeps its intrinsic height and the content box takes whatever is left of the viewport.
        /// </summary>
        [Test]
        public void PopUpCss_DistributesTheAutoSizedPopupWithAFlexBox()
        {
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);

            Match modalRule = AutoModalRuleRegex().Match(popUpCss);
            Match contentRule = AutoContentRuleRegex().Match(popUpCss);
            Assert.Multiple(() =>
            {
                Assert.That(modalRule.Success, Is.True, "The class of the auto sized modal is missing.");
                Assert.That(contentRule.Success, Is.True, "The content class of the auto sized modal is missing.");
            });

            string modalDeclarations = NormalizeWhitespace(modalRule.Groups["body"].Value);
            string contentDeclarations = NormalizeWhitespace(contentRule.Groups["body"].Value);
            Assert.Multiple(() =>
            {
                Assert.That(modalDeclarations, Does.Contain("display: flex"));
                Assert.That(modalDeclarations, Does.Contain("flex-direction: column"));
                Assert.That(contentDeclarations, Does.Contain("flex: 1 1 auto"));
                Assert.That(contentDeclarations, Does.Contain("min-height: 0"),
                    "Without min-height: 0 the content box keeps its automatic minimum size and overflows the modal.");
                Assert.That(contentDeclarations, Does.Contain("scrollbar-gutter: stable"),
                    "The modal is only as wide as its content, so the scrollbar needs a reserved gutter.");
                Assert.That(PixelConstantRegex().IsMatch(contentDeclarations), Is.False,
                    "The room for the footer must not be reserved with a pixel constant.");
            });
        }

        /// <summary>
        /// The content box of an auto sized popup scrolls, so its header has to be pinned to keep the title
        /// and the close button reachable. It shares the scroll container with the sticky headers of the
        /// tables inside the popup and therefore has to be stacked above them.
        /// </summary>
        [Test]
        public void PopUpCss_PinsTheHeaderOfTheAutoSizedPopupAboveTheStickyTableHeaders()
        {
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);
            string siteCss = ReadStyleSheetWithoutComments(kSiteCssResource);

            Match headerRule = AutoModalHeaderRuleRegex().Match(popUpCss);
            Match tableHeaderRule = StickyTableHeaderRuleRegex().Match(siteCss);
            Assert.Multiple(() =>
            {
                Assert.That(headerRule.Success, Is.True, "The header of the auto sized modal is not pinned.");
                Assert.That(tableHeaderRule.Success, Is.True, "The sticky table header rule is missing.");
            });

            string headerDeclarations = NormalizeWhitespace(headerRule.Groups["body"].Value);
            Assert.Multiple(() =>
            {
                Assert.That(headerDeclarations, Does.Contain("position: sticky"));
                Assert.That(headerDeclarations, Does.Contain("background-color"),
                    "The scrolling content would shine through a transparent header.");
                Assert.That(ReadZIndex(headerDeclarations), Is.GreaterThan(ReadZIndex(NormalizeWhitespace(tableHeaderRule.Groups["body"].Value))),
                    "The pinned modal header has to stay above the sticky table headers inside the popup.");
            });
        }

        /// <summary>
        /// A custom property that is referenced but never defined makes the whole declaration invalid,
        /// which silently disabled the sticky table headers inside the popups before.
        /// </summary>
        [Test]
        public void SiteCss_DoesNotReferenceAnUndefinedModalMarginVariable()
        {
            string siteCss = ReadStyleSheetWithoutComments(kSiteCssResource);
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);

            bool isReferenced = siteCss.Contains($"var({kModalMarginVariable}") || popUpCss.Contains($"var({kModalMarginVariable}");
            bool isDefined = siteCss.Contains($"{kModalMarginVariable}:") || popUpCss.Contains($"{kModalMarginVariable}:");

            Assert.That(isReferenced && !isDefined, Is.False,
                $"'{kModalMarginVariable}' is used without being defined, so the declaration using it has no effect.");
        }

        /// <summary>
        /// The .modal-content box is the scroll container of a popup and its top padding belongs to the
        /// scrollport. The sticky table headers have to compensate it, so the padding has to stay
        /// readable for them through a custom property instead of being written as a literal length.
        /// </summary>
        [Test]
        public void PopUpCss_PublishesTheTopPaddingOfTheScrollContainer()
        {
            string popUpCss = ReadStyleSheetWithoutComments(kPopUpCssResource);

            Match scrollContainerRule = ModalContentRuleRegex().Match(popUpCss);
            Assert.That(scrollContainerRule.Success, Is.True, "The rule of the popup scroll container is missing.");

            string declarations = NormalizeWhitespace(scrollContainerRule.Groups["body"].Value);
            Assert.Multiple(() =>
            {
                Assert.That(declarations, Does.Contain($"{kModalPaddingVariable}:"),
                    $"'{kModalPaddingVariable}' has to define the top padding of the popup scroll container.");
                Assert.That(declarations, Does.Contain($"padding: var({kModalPaddingVariable})"),
                    "The top padding of the popup scroll container has to be taken from its custom property.");
            });
        }

        /// <summary>
        /// A sticky offset is resolved against the content box of the scroll container: without
        /// compensating the top padding of the popup, the rows scroll through the gap above the header.
        /// </summary>
        [Test]
        public void SiteCss_CompensatesThePopUpPaddingForStickyTableHeaders()
        {
            string siteCss = ReadStyleSheetWithoutComments(kSiteCssResource);

            Match stickyHeaderRule = ModalStickyHeaderRuleRegex().Match(siteCss);
            Assert.That(stickyHeaderRule.Success, Is.True, "The sticky header rule of the popups is missing.");

            string declarations = NormalizeWhitespace(stickyHeaderRule.Groups["body"].Value);
            Assert.Multiple(() =>
            {
                Assert.That(declarations, Does.Contain($"var({kModalPaddingVariable}, "),
                    $"The sticky headers of the popups have to compensate '{kModalPaddingVariable}' with a fallback, "
                    + "because a custom property without one makes the whole declaration invalid if it is removed.");
                Assert.That(declarations, Does.Contain("-1px"), "The sticky headers lost their chrome offset.");
            });
        }

        private static string ReadStyleSheetWithoutComments(string resourceName)
        {
            Assembly assembly = typeof(UiPopUpStyleTest).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded stylesheet '{resourceName}' not found.");
            using StreamReader reader = new(stream);
            return CommentRegex().Replace(reader.ReadToEnd(), "");
        }

        private static string NormalizeWhitespace(string declarations)
        {
            return WhitespaceRegex().Replace(declarations, " ");
        }

        private static int ReadZIndex(string declarations)
        {
            Match zIndex = ZIndexRegex().Match(declarations);
            Assert.That(zIndex.Success, Is.True, $"No z-index found in '{declarations}'.");
            return int.Parse(zIndex.Groups["value"].Value);
        }

        [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline, knMilliseconds)]
        private static partial Regex CommentRegex();

        [GeneratedRegex(@"(^|[;{\s])transform\s*:", RegexOptions.Multiline, knMilliseconds)]
        private static partial Regex TransformDeclarationRegex();

        [GeneratedRegex(@"\.custom-modal-center\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex CenterRuleRegex();

        [GeneratedRegex(@"\.custom-modal-auto\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex AutoModalRuleRegex();

        [GeneratedRegex(@"\.custom-modal-content-auto\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex AutoContentRuleRegex();

        [GeneratedRegex(@"\.custom-modal-content-auto\s+\.modal-header\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex AutoModalHeaderRuleRegex();

        [GeneratedRegex(@"\.sticky-header\s+thead\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex StickyTableHeaderRuleRegex();

        [GeneratedRegex(@"z-index\s*:\s*(?<value>-?\d+)", RegexOptions.None, knMilliseconds)]
        private static partial Regex ZIndexRegex();

        [GeneratedRegex(@"\d+\s*px", RegexOptions.None, knMilliseconds)]
        private static partial Regex PixelConstantRegex();

        [GeneratedRegex(@"\.modal-content\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex ModalContentRuleRegex();

        [GeneratedRegex(@"\.modal-content\s+\.sticky-header\s+thead\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex ModalStickyHeaderRuleRegex();

        [GeneratedRegex(@"\s+", RegexOptions.None, knMilliseconds)]
        private static partial Regex WhitespaceRegex();
    }
}
