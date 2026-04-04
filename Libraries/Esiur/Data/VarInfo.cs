using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Esiur.Data
{
    struct VarInfo
    {
        public string Pre;
        public string Post;
        public string VarName;

        public string Build()
        {
            return Regex.Escape(Pre) + @"(?<" + VarName + @">[^\{]*)" + Regex.Escape(Post);
        }
    }

}
