using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetBoxPS.CodeGen
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Load the assembly from the file path provided in args[1]
            var sdkAssembly = Assembly.LoadFrom(args[1]);
            var endpoints = GetEndpoints(sdkAssembly);

            var functionAsts = new List<FunctionDefinitionAst>();
            var constructorAsts = new List<FunctionDefinitionAst>();

            foreach (var ep in endpoints)
            {
                var verb = SelectPowerShellVerb(ep.MethodName);
                var noun = SelectPowerShellNoun(ep.ObjectType);

                var paramGroups = GetParameterGroups(ep.Parameters);

                // Identify all nested custom types for constructor generation
                var nestedObjects = paramGroups
                    .Where(pg => pg.IsComplex)
                    .Select(pg => pg.Type)
                    .Concat(GetNestedObjects(ep.Parameters))
                    .Distinct();

                foreach (var nested in nestedObjects)
                {
                    var nestedNoun = SelectPowerShellNoun(nested);
                    var ctorAst = BuildConstructorAst(nestedNoun, nested);
                    constructorAsts.Add(ctorAst);
                }

                // Pass paramGroups to FunctionDefinitionAst for scaffolding
                var funcAst = new FunctionDefinitionAst(verb, noun, paramGroups, nestedObjects, ep);
                functionAsts.Add(funcAst);
            }

            // Write all ASTs to output (pseudo-code, implement as needed)
            foreach (var ast in constructorAsts.Concat(functionAsts))
            {
                ast.ToFile(args[0]);
            }

            Console.WriteLine("PowerShell function generation complete!");
            return 0;
        }

        // Reflect over the SDK assembly to find all API endpoints
        public static IEnumerable<ApiEndpoint> GetEndpoints(Assembly sdkAssembly)
        {
            // TODO: Implement actual reflection logic
            throw new NotImplementedException();
        }

        // Analyze method name to choose the correct PowerShell verb
        public static string SelectPowerShellVerb(string methodName)
        {
            // Example logic, expand as needed
            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) return "Get";
            if (methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase)) return "New";
            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return "Remove";
            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase)) return "Set";
            return "Invoke";
        }

        // Convert object type to PowerShell-friendly singular PascalCase noun
        public static string SelectPowerShellNoun(Type objectType)
        {
            // Example: strip plural, convert to PascalCase
            var name = objectType.Name;
            if (name.EndsWith("s")) name = name.Substring(0, name.Length - 1);
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        // Flatten parameters if a first-level object is present
        public static IEnumerable<ParameterInfo> FlattenParameters(IEnumerable<ParameterInfo> parameters)
        {
            var paramList = parameters.ToList();
            if (paramList.Count == 1 && IsComplexType(paramList[0].ParameterType))
            {
                // Flatten properties of the object
                return paramList[0].ParameterType.GetProperties()
                    .Select(p => new DummyParameterInfo(p));
            }
            return paramList;
        }

        // Identify nested objects for constructor generation
        public static IEnumerable<Type> GetNestedObjects(IEnumerable<ParameterInfo> parameters)
        {
            var nested = new List<Type>();
            foreach (var param in parameters)
            {
                if (IsComplexType(param.ParameterType))
                {
                    foreach (var prop in param.ParameterType.GetProperties())
                    {
                        if (IsComplexType(prop.PropertyType))
                            nested.Add(prop.PropertyType);
                    }
                }
            }
            return nested.Distinct();
        }

        // Build constructor AST for nested objects
        public static FunctionDefinitionAst BuildConstructorAst(string noun, Type objectType)
        {
            // TODO: Use PowerShell AST to build the constructor function
            throw new NotImplementedException();
        }

        // Helper: determine if a type is a complex object (not primitive or string)
        public static bool IsComplexType(Type type)
        {
            return !(type.IsPrimitive || type == typeof(string) || type.IsEnum);
        }

        // Dummy ParameterInfo for flattened properties (since PropertyInfo != ParameterInfo)
        public class DummyParameterInfo : ParameterInfo
        {
            public DummyParameterInfo(PropertyInfo prop)
            {
                NameImpl = prop.Name;
                ClassImpl = prop.PropertyType;
            }
        }

        // Represents a parameter group for a top-level parameter (primitive or custom type)
        public class ParameterGroup
        {
            public string Name { get; }
            public Type Type { get; }
            public bool IsComplex { get; }
            public IEnumerable<ParameterInfo> Properties { get; }

            public ParameterGroup(string name, Type type, bool isComplex, IEnumerable<ParameterInfo> properties = null)
            {
                Name = name;
                Type = type;
                IsComplex = isComplex;
                Properties = properties ?? Enumerable.Empty<ParameterInfo>();
            }
        }

        // Returns a list of parameter groups for the endpoint
        public static IEnumerable<ParameterGroup> GetParameterGroups(IEnumerable<ParameterInfo> parameters)
        {
            foreach (var param in parameters)
            {
                if (IsComplexType(param.ParameterType))
                {
                    // Group for custom type: expose its properties
                    var props = param.ParameterType.GetProperties()
                        .Select(p => new DummyParameterInfo(p));
                    yield return new ParameterGroup(param.Name, param.ParameterType, true, props);
                }
                else
                {
                    // Group for primitive type: expose directly
                    yield return new ParameterGroup(param.Name, param.ParameterType, false);
                }
            }
        }

        // Example endpoint descriptor
        public class ApiEndpoint
        {
            public string MethodName { get; set; }
            public Type ObjectType { get; set; }
            public IEnumerable<ParameterInfo> Parameters { get; set; }
        }

        // Placeholder for PowerShell AST function definition
        public class FunctionDefinitionAst
        {
            public string Verb { get; }
            public string Noun { get; }
            public IEnumerable<ParameterGroup> ParameterGroups { get; }
            public IEnumerable<Type> NestedObjects { get; }
            public ApiEndpoint Endpoint { get; }

            public FunctionDefinitionAst(
                string verb,
                string noun,
                IEnumerable<ParameterGroup> parameterGroups,
                IEnumerable<Type> nestedObjects,
                ApiEndpoint endpoint)
            {
                Verb = verb;
                Noun = noun;
                ParameterGroups = parameterGroups;
                NestedObjects = nestedObjects;
                Endpoint = endpoint;
            }

            // Assemble the PowerShell function as a string
            private string AssemblePowerShellFunction()
            {
                var functionName = $"{Verb}-{Noun}";
                var paramLines = new List<string>();
                var objectAssemblyLines = new List<string>();
                var callParams = new List<string>();

                foreach (var group in ParameterGroups)
                {
                    if (group.IsComplex)
                    {
                        // Add parameters for each property of the complex type
                        foreach (var prop in group.Properties)
                        {
                            paramLines.Add($"    [Parameter()]${group.Name}_{prop.Name}");
                        }
                        // Assemble the custom object from its properties
                        objectAssemblyLines.Add($"    ${group.Name} = [PSCustomObject]@{{");
                        foreach (var prop in group.Properties)
                        {
                            objectAssemblyLines.Add($"        {prop.Name} = ${group.Name}_{prop.Name}");
                        }
                        objectAssemblyLines.Add("    }");
                        callParams.Add($"${group.Name}");
                    }
                    else
                    {
                        // Add primitive parameter
                        paramLines.Add($"    [Parameter()]${group.Name}");
                        callParams.Add($"${group.Name}");
                    }
                }

                // Example: Call the SDK endpoint (pseudo-code, adjust as needed)
                var sdkCall = $"    # Call SDK: $result = [SDK]::{Endpoint.MethodName}({string.Join(", ", callParams)})";

                // Assemble the function
                var lines = new List<string>
                {
                    $"function {functionName} {{",
                    "    param(",
                    string.Join(",\n", paramLines),
                    "    )",
                    ""
                };
                lines.AddRange(objectAssemblyLines);
                lines.Add(sdkCall);
                lines.Add("    return $result");
                lines.Add("}");

                return string.Join("\n", lines);
            }

            // Serialize AST to PowerShell code and write to outputPath
            public void ToFile(string outputPath)
            {
                var psCode = AssemblePowerShellFunction();
                System.IO.File.AppendAllText(outputPath, psCode + "\n\n");
            }

            // Add methods as needed for scaffolding
        }
    }
}
