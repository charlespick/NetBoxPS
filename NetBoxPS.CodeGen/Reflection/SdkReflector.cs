using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NetBoxPS.CodeGen.Reflection
{
    internal class SdkReflector
    {
        private readonly Assembly _sdkAssembly;

        public SdkReflector()
        {
            // Use the assembly from the SDK project reference
            _sdkAssembly = typeof(NetBoxSdk.SomeSdkEntryPoint).Assembly;
        }

        public IEnumerable<EndpointDefinition> GetEndpoints()
        {
            var endpoints = new List<EndpointDefinition>();

            foreach (var type in _sdkAssembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.DeclaringType != type) continue;

                    endpoints.Add(new EndpointDefinition
                    {
                        Name = method.Name,
                        HttpMethod = ExtractHttpMethod(method),
                        Route = ExtractRoute(method),
                        ReturnType = method.ReturnType,
                        Parameters = method.GetParameters()
                                           .ToDictionary(p => p.Name, p => p.ParameterType)
                    });
                }
            }

            return endpoints;
        }

        private string ExtractHttpMethod(MethodInfo method)
        {
            // TODO: Use SDK attributes or naming conventions
            return "GET";
        }

        private string ExtractRoute(MethodInfo method)
        {
            // TODO: Use SDK attributes or naming conventions
            return "/api/unknown/";
        }
    }
}
