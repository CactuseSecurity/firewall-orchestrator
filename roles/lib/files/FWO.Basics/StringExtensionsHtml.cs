using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {

        private const string HtmlTagPattern = "<[^>]*>";

        public static string StripHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(text, HtmlTagPattern, string.Empty, options, TimeSpan.FromMilliseconds(100));
        }
    }
}
