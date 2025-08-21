using System;
using System.Collections.Generic;
using System.Text;

namespace NetBoxPS.CodeGen.Models
{
    public class EndpointDefinition
    {
        public string Name { get; set; }           // e.g., "GetDevices"
        public string HttpMethod { get; set; }     // GET, POST, PUT, DELETE
        public string Route { get; set; }          // "/api/dcim/devices/"
        public Type ReturnType { get; set; }       // Return type from SDK method
        public Dictionary<string, Type> Parameters { get; set; } // paramName -> Type
    }

}
