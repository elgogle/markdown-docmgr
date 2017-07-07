using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MarkdownRepository.Lib
{
    public static class Extentions
    {
        public static string GetUserName(this string fullName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                if (fullName.Any(t => t == '\\'))
                {
                    return fullName.Substring(fullName.LastIndexOf('\\') + 1);
                }

                return fullName;
            }

            return fullName;
        }
    }
}