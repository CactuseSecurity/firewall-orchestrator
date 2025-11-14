using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Basics;

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
            string? nullString = null;
            ClassicAssert.AreEqual(null, nullString.SanitizeOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, OkText.SanitizeOpt( ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, OkText.SanitizeMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedText, TextToShorten.SanitizeMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeLdapNameOpt(ref shortened));
            ClassicAssert.AreEqual(OkLdapName, OkLdapName.SanitizeLdapNameOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapName, OkLdapName.SanitizeLdapNameMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapName, LdapNameToShorten.SanitizeLdapNameMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeLdapPathOpt(ref shortened));
            ClassicAssert.AreEqual(OkLdapPath, OkLdapPath.SanitizeLdapPathOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapPath, OkLdapPath.SanitizeLdapPathMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapPath, LdapPathToShorten.SanitizeLdapPathMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizePasswOpt(ref shortened));
            ClassicAssert.AreEqual(OkPassw, OkPassw.SanitizePasswOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkPassw, OkPassw.SanitizePasswMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedPassw, PasswToShorten.SanitizePasswMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeKeyOpt(ref shortened));
            ClassicAssert.AreEqual(OkKey, OkKey.SanitizeKeyOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkKey, OkKey.SanitizeKeyMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedKey, KeyToShorten.SanitizeKeyMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeCommentOpt(ref shortened));
            ClassicAssert.AreEqual(OkComment, OkComment.SanitizeCommentOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkComment, OkComment.SanitizeCommentMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedComment, CommentToShorten.SanitizeCommentMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeCidrOpt(ref shortened));
            ClassicAssert.AreEqual(OkCidr, OkCidr.SanitizeCidrOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkCidr, OkCidr.SanitizeCidrMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedCidr, CidrToShorten.SanitizeCidrMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJson, OkJson.SanitizeJsonMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJson, JsonToShorten.SanitizeJsonMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJsonField, OkJsonField.SanitizeJsonFieldMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJsonField, JsonFieldToShorten.SanitizeJsonFieldMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);
        }
    }
}
