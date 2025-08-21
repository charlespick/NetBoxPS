using System;
using System.Collections.Generic;
using System.Text;

namespace NetBoxPS.CodeGen.Generators
{
    public static class VerbHelper
    {
        public static string MapHttpMethodToVerb(string httpMethod) => httpMethod.ToUpper() switch
        {
            "GET" => "Get",
            "POST" => "New",
            "PUT" => "Set",
            "PATCH" => "Update",
            "DELETE" => "Remove",
            _ => "Invoke"
        };
    }
}
