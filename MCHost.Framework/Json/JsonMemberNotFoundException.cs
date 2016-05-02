using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonMemberNotFoundException : JsonException
    {
        public string MemberName { get; private set; }

        public JsonMemberNotFoundException(string memberName) :
            base($"The member '{memberName}' does not exist in Json object.")
        {
            MemberName = memberName;
        }
    }
}
