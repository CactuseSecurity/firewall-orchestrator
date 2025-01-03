using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class SanitizerTest
    {
        static readonly string OkText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_";
        static readonly string TextToShorten = " A\"\\'!,; ";
        static readonly string ShortenedText = "A";
        static readonly string OkLdapName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()_";
        static readonly string LdapNameToShorten = " A+;,\"<>#= B ";
        static readonly string ShortenedLdapName = "A B";
        static readonly string OkLdapPath = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()_,=";
        static readonly string LdapPathToShorten = " A+;,\"<>#= B ";
        static readonly string ShortenedLdapPath = "A,= B";
        static readonly string OkPassw = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#";
        static readonly string PasswToShorten = " a \n\rb ";
        static readonly string ShortenedPassw = "a b";
        static readonly string OkKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_\"\\'!,;";
        static readonly string KeyToShorten = " anykey ";
        static readonly string ShortenedKey = "anykey";
        static readonly string OkComment = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_\\!,;";
        static readonly string CommentToShorten = "\"anytext'";
        static readonly string ShortenedComment = "anytext";
        static readonly string OkCidr = "1234567890ABCDEFabcdef:./";
        static readonly string CidrToShorten = " FGHIJKLMNOPQRSTUVWXYZ fghijklmnopqrstuvwxyz?@()[]{}$+<>#_\\!,; ";
        static readonly string ShortenedCidr = "Ff";
        static readonly string OkJson = "1234567890ABCDEFabcdef:._{a§}+*(){}[]?!#<>=,;/\\@$%^|&";
        static readonly string JsonToShorten = " {\t\n\r}";
        static readonly string ShortenedJson = "{}";
        static readonly string OkJsonField = "1234567890ABCDEFabcdef:._";
        static readonly string JsonFieldToShorten = " {a§}+*(){}[]?!#<>=,;/\\\t@$%^|&~ ";
        static readonly string ShortenedJsonField = "_a§___________________________";

        [SetUp]
        public void Initialize()
        {}

        [Test]
        public void TestSanitizer()
        {
            bool shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeOpt(null, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, Sanitizer.SanitizeOpt(OkText, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, Sanitizer.SanitizeMand(OkText, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedText, Sanitizer.SanitizeMand(TextToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeLdapNameOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkLdapName, Sanitizer.SanitizeLdapNameOpt(OkLdapName, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapName, Sanitizer.SanitizeLdapNameMand(OkLdapName, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapName, Sanitizer.SanitizeLdapNameMand(LdapNameToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeLdapPathOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkLdapPath, Sanitizer.SanitizeLdapPathOpt(OkLdapPath, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapPath, Sanitizer.SanitizeLdapPathMand(OkLdapPath, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapPath, Sanitizer.SanitizeLdapPathMand(LdapPathToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizePasswOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkPassw, Sanitizer.SanitizePasswOpt(OkPassw, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkPassw, Sanitizer.SanitizePasswMand(OkPassw, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedPassw, Sanitizer.SanitizePasswMand(PasswToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeKeyOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkKey, Sanitizer.SanitizeKeyOpt(OkKey, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkKey, Sanitizer.SanitizeKeyMand(OkKey, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedKey, Sanitizer.SanitizeKeyMand(KeyToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeCommentOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkComment, Sanitizer.SanitizeCommentOpt(OkComment, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkComment, Sanitizer.SanitizeCommentMand(OkComment, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedComment, Sanitizer.SanitizeCommentMand(CommentToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, Sanitizer.SanitizeCidrOpt(null, ref shortened));
            ClassicAssert.AreEqual(OkCidr, Sanitizer.SanitizeCidrOpt(OkCidr, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkCidr, Sanitizer.SanitizeCidrMand(OkCidr, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedCidr, Sanitizer.SanitizeCidrMand(CidrToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJson, Sanitizer.SanitizeJsonMand(OkJson, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJson, Sanitizer.SanitizeJsonMand(JsonToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJsonField, Sanitizer.SanitizeJsonFieldMand(OkJsonField, ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJsonField, Sanitizer.SanitizeJsonFieldMand(JsonFieldToShorten, ref shortened));
            ClassicAssert.AreEqual(true, shortened);
        }
    }
}
