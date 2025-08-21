using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NetBoxPS.CodeGen.Generators
{
    public static class NounHelper
    {
        public static string BuildNounFromRoute(string route)
        {
            // Strip API prefix
            var clean = Regex.Replace(route, "^/api/", "");

            // Take last segment, singularize and PascalCase
            var segments = clean.Split('/');
            var last = segments.Length > 0 ? segments[segments.Length - 1] : "Object";
            last = last.EndsWith("s") ? last[..^1] : last;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(last);
        }
    }
}
