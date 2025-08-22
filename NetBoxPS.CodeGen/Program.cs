using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Management.Automation.Language;
using System.Management.Automation;

namespace NetBoxPS.CodeGen
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var sdkAssembly = Assembly.LoadFrom(args[1]);
            var endpoints = GetEndpoints(sdkAssembly);

            var functionAsts = new List<FunctionDefinitionAst>();
            var constructorAsts = new List<FunctionDefinitionAst>();

            foreach (var ep in endpoints)
            {
                var verb = SelectPowerShellVerb(ep.MethodName);
                var noun = SelectPowerShellNoun(ep.ObjectType);

                var paramGroups = GetParameterGroups(ep.Parameters);

                var nestedObjects = paramGroups
                    .Where(pg => pg.IsComplex)
                    .Select(pg => pg.Type)
                    .Concat(GetNestedObjects(ep.Parameters))
                    .Distinct();

                foreach (var nested in nestedObjects)
                {
                    var nestedNoun = SelectPowerShellNoun(nested);
                    var ctorAst = GenerateConstructorFunctionAst(nestedNoun, nested);
                    constructorAsts.Add(ctorAst);
                }

                var funcAst = GenerateSdkWrapperFunctionAst(verb, noun, paramGroups, ep);
                functionAsts.Add(funcAst);
            }

            foreach (var ast in constructorAsts.Concat(functionAsts))
            {
                // Write the generated PowerShell AST to file as script text
                System.IO.File.AppendAllText(args[0], ast.Extent.Text + "\n\n");
            }

            Console.WriteLine("PowerShell function generation complete!");
            return 0;
        }

        public static IEnumerable<ApiEndpoint> GetEndpoints(Assembly sdkAssembly)
        {
            var endpoints = new List<ApiEndpoint>();
            var verbs = new[] { "Get", "Create", "Delete", "Update" };

            foreach (var type in sdkAssembly.GetExportedTypes())
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (!verbs.Any(v => method.Name.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (method.IsSpecialName)
                        continue;

                    endpoints.Add(new ApiEndpoint
                    {
                        MethodName = method.Name,
                        ObjectType = method.ReturnType,
                        Parameters = method.GetParameters()
                    });
                }
            }

            return endpoints;
        }

        public static string SelectPowerShellVerb(string methodName)
        {
            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
                return VerbsCommon.Get;
            if (methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                return VerbsCommon.New;
            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
                return VerbsCommon.Remove;
            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
                return VerbsCommon.Set;
            throw new ArgumentException($"Unknown method verb for method name: {methodName}", nameof(methodName));
        }

        public static string SelectPowerShellNoun(Type objectType)
        {
            var name = objectType.Name;
            if (name.EndsWith("s")) name = name.Substring(0, name.Length - 1);
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static IEnumerable<ParameterInfo> FlattenParameters(IEnumerable<ParameterInfo> parameters)
        {
            var paramList = parameters.ToList();
            if (paramList.Count == 1 && IsComplexType(paramList[0].ParameterType))
            {
                return paramList[0].ParameterType.GetProperties()
                    .Select(p => new DummyParameterInfo(p));
            }
            return paramList;
        }

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

        // Generates a PowerShell AST for a constructor function for a custom object type
        public static FunctionDefinitionAst GenerateConstructorFunctionAst(string noun, Type objectType)
        {
            var functionName = $"New-{noun}";
            var parameters = objectType.GetProperties()
                .Select(p => new ParameterAst(
                    extent: null,
                    new VariableExpressionAst(null, p.Name, false),
                    null,
                    null,
                    new List<AttributeAst>()
                )).ToList();

            var paramBlock = new ParamBlockAst(null, parameters, null);

            // The body just creates a new object and sets properties from parameters
            var hashtableEntries = objectType.GetProperties().Select(p =>
                new ExpressionAstPair(
                    new StringConstantExpressionAst(null, p.Name, StringConstantType.DoubleQuoted),
                    new VariableExpressionAst(null, p.Name, false)
                )
            ).ToList();

            var scriptBlock = new ScriptBlockAst(
                null,
                paramBlock,
                new StatementBlockAst(
                    null,
                    new[]
                    {
                        new PipelineAst(
                            null,
                            new CommandAst[]
                            {
                                new CommandAst(
                                    null,
                                    new List<CommandElementAst>
                                    {
                                        new StringConstantExpressionAst(null, "[PSCustomObject]", StringConstantType.DoubleQuoted),
                                        new CommandParameterAst(null, "Property", new HashtableAst(
                                            null,
                                            hashtableEntries
                                        ), null)
                                    },
                                    TokenKind.Function,
                                    null
                                )
                            }
                        )
                    },
                    null
                ),
                false,
                false
            );

            return new FunctionDefinitionAst(
                null,
                functionName,
                false,
                paramBlock,
                scriptBlock
            );
        }

        // Generates a PowerShell AST for an SDK wrapper function
        public static FunctionDefinitionAst GenerateSdkWrapperFunctionAst(
            string verb,
            string noun,
            IEnumerable<ParameterGroup> parameterGroups,
            ApiEndpoint endpoint)
        {
            var functionName = $"{verb}-{noun}";
            var parameters = parameterGroups.Select(pg =>
                new ParameterAst(
                    extent: null,
                    new VariableExpressionAst(null, pg.Name, false),
                    null,
                    null,
                    new List<AttributeAst>()
                )).ToList();

            var paramBlock = new ParamBlockAst(null, parameters, null);

            var commandElements = new List<CommandElementAst>
            {
                new StringConstantExpressionAst(null, $"$sdk.{endpoint.MethodName}", StringConstantType.DoubleQuoted)
            };
            commandElements.AddRange(parameterGroups.Select(pg =>
                new CommandParameterAst(null, pg.Name, new VariableExpressionAst(null, pg.Name, false), null)
            ));

            var scriptBlock = new ScriptBlockAst(
                null,
                paramBlock,
                new StatementBlockAst(
                    null,
                    new[]
                    {
                        new PipelineAst(
                            null,
                            new CommandAst[]
                            {
                                new CommandAst(
                                    null,
                                    commandElements,
                                    TokenKind.Function,
                                    null
                                )
                            }
                        )
                    },
                    null
                ),
                false,
                false
            );

            return new FunctionDefinitionAst(
                null,
                functionName,
                false,
                paramBlock,
                scriptBlock
            );
        }

        public static bool IsComplexType(Type type)
        {
            return !(type.IsPrimitive || type == typeof(string) || type.IsEnum);
        }

        public class DummyParameterInfo : ParameterInfo
        {
            public DummyParameterInfo(PropertyInfo prop)
            {
                NameImpl = prop.Name;
                ClassImpl = prop.PropertyType;
            }
        }

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

        public static IEnumerable<ParameterGroup> GetParameterGroups(IEnumerable<ParameterInfo> parameters)
        {
            foreach (var param in parameters)
            {
                if (IsComplexType(param.ParameterType))
                {
                    var props = param.ParameterType.GetProperties()
                        .Select(p => new DummyParameterInfo(p));
                    yield return new ParameterGroup(param.Name, param.ParameterType, true, props);
                }
                else
                {
                    yield return new ParameterGroup(param.Name, param.ParameterType, false);
                }
            }
        }

        public class ApiEndpoint
        {
            public string MethodName { get; set; }
            public Type ObjectType { get; set; }
            public IEnumerable<ParameterInfo> Parameters { get; set; }
        }
    }
}
