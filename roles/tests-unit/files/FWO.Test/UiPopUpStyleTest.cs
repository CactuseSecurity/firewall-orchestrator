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

        [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline, knMilliseconds)]
        private static partial Regex CommentRegex();

        [GeneratedRegex(@"(^|[;{\s])transform\s*:", RegexOptions.Multiline, knMilliseconds)]
        private static partial Regex TransformDeclarationRegex();

        [GeneratedRegex(@"\.custom-modal-center\s*\{(?<body>[^}]*)\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex CenterRuleRegex();

        [GeneratedRegex(@"\s+", RegexOptions.None, knMilliseconds)]
        private static partial Regex WhitespaceRegex();
    }
}
