using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the cursor of switches, check boxes and radio buttons. The stylesheet is embedded into this
    /// assembly at build time (see FWO.Test.csproj).
    /// </summary>
    [TestFixture]
    internal partial class UiFormCheckStyleTest
    {
        private const string kSiteCssResource = "FWO.Test.site.css";
        private const int knMilliseconds = 1000;

        /// <summary>
        /// Bootstrap leaves the default arrow on check controls, which makes switches look as if they
        /// could not be clicked. Inputs and their labels need the pointer cursor.
        /// </summary>
        [Test]
        public void SiteCss_ShowsPointerOnCheckControlsAndLabels()
        {
            string siteCss = ReadStyleSheetWithoutComments();

            Match rule = FormCheckCursorRuleRegex().Match(siteCss);

            Assert.That(rule.Success, Is.True, "Check inputs and their labels do not get the pointer cursor.");
        }

        /// <summary>
        /// The rule must not raise its specificity above Bootstrap's :disabled rules, otherwise disabled
        /// controls would look clickable.
        /// </summary>
        [Test]
        public void SiteCss_DoesNotOverrideTheDisabledCursor()
        {
            string siteCss = ReadStyleSheetWithoutComments();

            Assert.That(ImportantCursorRegex().IsMatch(siteCss), Is.False,
                "An !important pointer cursor would override the default cursor of disabled controls.");
        }

        private static string ReadStyleSheetWithoutComments()
        {
            Assembly assembly = typeof(UiFormCheckStyleTest).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(kSiteCssResource)
                ?? throw new InvalidOperationException($"Embedded stylesheet '{kSiteCssResource}' not found.");
            using StreamReader reader = new(stream);
            return CommentRegex().Replace(reader.ReadToEnd(), "");
        }

        [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline, knMilliseconds)]
        private static partial Regex CommentRegex();

        [GeneratedRegex(@"(^|\})\s*\.form-check-input\s*,\s*\.form-check-label\s*\{[^}]*cursor\s*:\s*pointer[^}]*\}", RegexOptions.None, knMilliseconds)]
        private static partial Regex FormCheckCursorRuleRegex();

        [GeneratedRegex(@"\.form-check-(input|label)[^{]*\{[^}]*cursor\s*:\s*pointer\s*!important", RegexOptions.None, knMilliseconds)]
        private static partial Regex ImportantCursorRegex();
    }
}
