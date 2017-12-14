using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace Cobalt.IO
{
    internal class FileMethods
    {
        public static Type IdentifyFileByName(string filename, List<Type> typesToCheck)
        {
            List<Tuple<Type, string, int>> matchedTypes = new List<Tuple<Type, string, int>>();

            foreach (Type type in typesToCheck)
            {
                FileNamePatternAttribute attrib = (FileNamePatternAttribute)type.GetCustomAttributes(typeof(FileNamePatternAttribute), false).FirstOrDefault();
                if (attrib != null)
                {
                    Regex reg = new Regex(attrib.Pattern, RegexOptions.IgnoreCase);
                    if (reg.IsMatch(Path.GetFileName(filename)))
                        matchedTypes.Add(new Tuple<Type, string, int>(type, attrib.Pattern, attrib.Pattern.Length));
                }
            }

            /* Assume longer Regex pattern means more precise match */
            Tuple<Type, string, int> bestMatch = matchedTypes.OrderByDescending(x => x.Item3).FirstOrDefault();
            return (bestMatch == null ? null : bestMatch.Item1);
        }

        public static string CreateFullPath(string p1, string p2)
        {
            string path = p2;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(p1), path);
            return Path.GetFullPath(path);
        }
    }
}
