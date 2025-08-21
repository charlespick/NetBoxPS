using NetBoxPS.CodeGen.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetBoxPS.CodeGen.Reflection
{
    internal class SdkReflector
    {
        private readonly Assembly _sdkAssembly;

        // Optional explicit assembly injection (handy for tests)
        public SdkReflector(Assembly? sdkAssembly = null)
        {
            if (sdkAssembly != null)
            {
                _sdkAssembly = sdkAssembly;
                return;
            }

            // 1) Look through assemblies referenced by the current/entry/calling assemblies
            var roots = new[]
            {
                Assembly.GetExecutingAssembly(),
                Assembly.GetEntryAssembly(),
                Assembly.GetCallingAssembly()
            }.Where(a => a != null)!.Distinct();

            var candidates = new List<Assembly>();

            foreach (var root in roots!)
            {
                foreach (var name in root!.GetReferencedAssemblies())
                {
                    try
                    {
                        var asm = Assembly.Load(name);
                        if (!asm.IsDynamic) candidates.Add(asm);
                    }
                    catch
                    {
                        // ignore load failures and continue
                    }
                }
            }

            // 2) Also include anything already loaded (useful under test runners / VS)
            candidates.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic));

            // 3) Pick the first assembly that looks like an OpenAPI SDK (classes like DcimApi in *.Api)
            _sdkAssembly = candidates
                .Where(IsLikelyOpenApiSdk)
                // nudge prefer assemblies with "NetBox" in the name if multiple match
                .OrderByDescending(a => (a.GetName().Name?.IndexOf("NetBox", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Could not find the NetBox OpenAPI SDK among referenced assemblies. " +
                    "If needed, pass the assembly explicitly to SdkReflector(Assembly).");
        }

        public IEnumerable<EndpointDefinition> GetEndpoints()
        {
            var endpoints = new List<EndpointDefinition>();

            foreach (var type in _sdkAssembly.GetTypes())
            {
                // OpenAPI Generator convention: client classes live under *.Api and end with "Api"
                if (!(type.IsClass &&
                      type.Name.EndsWith("Api", StringComparison.Ordinal) &&
                      (type.Namespace?.EndsWith(".Api", StringComparison.Ordinal) ?? false)))
                    continue;

                // DeclaredOnly avoids inherited/Object methods
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    endpoints.Add(new EndpointDefinition
                    {
                        Name = method.Name,
                        HttpMethod = ExtractHttpMethod(method),
                        Route = ExtractRoute(method),
                        ReturnType = method.ReturnType,
                        Parameters = method.GetParameters().ToDictionary(p => p.Name!, p => p.ParameterType)
                    });
                }
            }

            return endpoints;
        }

        private static bool IsLikelyOpenApiSdk(Assembly asm)
        {
            try
            {
                // Look for any *.Api namespace with classes named *Api (DcimApi, IpamApi, etc.)
                return asm.GetTypes().Any(t =>
                    t != null &&
                    t.IsClass &&
                    t.Name.EndsWith("Api", StringComparison.Ordinal) &&
                    (t.Namespace?.EndsWith(".Api", StringComparison.Ordinal) ?? false));
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some assemblies may partially load; check the successfully loaded types
                return ex.Types?.Any(t =>
                    t != null &&
                    t.IsClass &&
                    t.Name.EndsWith("Api", StringComparison.Ordinal) &&
                    (t.Namespace?.EndsWith(".Api", StringComparison.Ordinal) ?? false)) == true;
            }
            catch
            {
                return false;
            }
        }

        private string ExtractHttpMethod(MethodInfo method)
        {
            // TODO: Improve by parsing generated method name or attributes if present.
            return "GET";
        }

        private string ExtractRoute(MethodInfo method)
        {
            // TODO: Improve by inspecting generated request builders/paths if the SDK exposes them.
            return "/api/unknown/";
        }
    }
}
