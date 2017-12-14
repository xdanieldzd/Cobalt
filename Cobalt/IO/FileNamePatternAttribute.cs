using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cobalt.IO
{
    internal class FileNamePatternAttribute : Attribute
    {
        public string Pattern;

        public FileNamePatternAttribute(string pattern)
        {
            Pattern = pattern;
        }
    }
}
