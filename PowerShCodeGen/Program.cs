
using System.Reflection;
using System.Management.Automation.Language;
using System.Management.Automation;

namespace PowerShCodeGen
{
    internal class Program
    {

        static readonly HashSet<Type> SimpleTypes = new HashSet<Type>
        {
            typeof(string), typeof(bool), typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double),
            typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
            typeof(Guid), typeof(TimeSpan), typeof(Uri)
        };

        static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsEnum || SimpleTypes.Contains(type);
        }

        public static bool IsComplexType(Type type) => !IsSimpleType(type);

        static void Main(string[] args)
        {
            var sdkAssembly = typeof(NetBoxSdk.Api.CircuitsApi).Assembly;

            var endpoints = GetEndpoints(sdkAssembly);

            var functionAsts = new List<FunctionDefinitionAst>();
            var constructorAsts = new List<FunctionDefinitionAst>();

            var emittedTypes = new HashSet<Type>();

            foreach (var ep in endpoints)
            {
                var verb = SelectPowerShellVerb(ep.MethodName);
                var noun = SelectPowerShellNoun(ep.ObjectType);

                var paramGroups = GetParameterGroups(ep.Parameters);
                var flatParams = FlattenParametersForWrapper(paramGroups);

                var nestedObjects = paramGroups
                    .Where(pg => pg.IsComplex)
                    .SelectMany(pg => pg.Properties.Where(p => IsComplexType(p.ParameterType)).Select(p => p.ParameterType))
                    .Distinct();

                foreach (var nested in nestedObjects)
                {
                    if (!emittedTypes.Add(nested)) continue;
                    var nestedNoun = SelectPowerShellNoun(nested);
                    var ctorAst = GenerateConstructorFunctionAst(nestedNoun, nested);
                    constructorAsts.Add(ctorAst);
                }

                var funcAst = GenerateSdkWrapperFunctionAst(verb, noun, flatParams, ep);
                functionAsts.Add(funcAst);
            }

            foreach (var ast in constructorAsts.Concat(functionAsts))
            {
                System.IO.File.AppendAllText(args[0], ast.ToString() + "\n\n");
            }

            Console.WriteLine("PowerShell function generation complete!");
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
            var type = UnwrapGenericType(objectType);
            var name = type.Name;

            if (name.EndsWith("Response", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 8);
            if (name.EndsWith("Result", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 6);

            name = Singularize(name);

            return char.ToUpper(name[0]) + name.Substring(1);
        }

        private static Type UnwrapGenericType(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1)
                    return UnwrapGenericType(genericArgs[0]);
            }
            return type;
        }

        private static string Singularize(string word)
        {
            if (string.IsNullOrEmpty(word))
                return word;

            if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
                return word.Substring(0, word.Length - 3) + "y";

            if (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("xes", StringComparison.OrdinalIgnoreCase))
                return word.Substring(0, word.Length - 2);

            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && word.Length > 1)
                return word.Substring(0, word.Length - 1);

            return word;
        }

        public static IEnumerable<FlattenedParameter> FlattenParametersForWrapper(IEnumerable<ParameterGroup> parameterGroups)
        {
            var flatParams = new List<FlattenedParameter>();

            foreach (var group in parameterGroups)
            {
                if (!group.IsComplex)
                {
                    flatParams.Add(new FlattenedParameter(
                        name: group.Name,
                        type: group.Type,
                        isComplex: false,
                        sourceGroup: group.Name,
                        sourceProperty: null,
                        sourceGroupPosition: group.Position,
                        sourceGroupType: group.Type
                    ));
                }
                else
                {
                    foreach (var prop in group.Properties)
                    {
                        if (IsComplexType(prop.ParameterType))
                        {
                            flatParams.Add(new FlattenedParameter(
                                name: prop.Name,
                                type: prop.ParameterType,
                                isComplex: true,
                                sourceGroup: group.Name,
                                sourceProperty: prop.Name,
                                sourceGroupPosition: group.Position,
                                sourceGroupType: group.Type
                            ));
                        }
                        else
                        {
                            flatParams.Add(new FlattenedParameter(
                                name: prop.Name,
                                type: prop.ParameterType,
                                isComplex: false,
                                sourceGroup: group.Name,
                                sourceProperty: prop.Name,
                                sourceGroupPosition: group.Position,
                                sourceGroupType: group.Type
                            ));
                        }
                    }
                }
            }

            return flatParams;
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

        // Generates a strongly-typed "New-<Noun>" constructor with CmdletBinding, typed params, Mandatory where appropriate, and conditional assignments.
        public static FunctionDefinitionAst GenerateConstructorFunctionAst(string noun, Type objectType)
        {
            var functionName = $"New-{noun}";

            var settableProperties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToList();

            var parameters = new List<ParameterAst>();
            for (int i = 0; i < settableProperties.Count; i++)
            {
                var p = settableProperties[i];
                var attrs = new List<AttributeBaseAst>();

                var namedArgs = new List<NamedAttributeArgumentAst>();

                if ((p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) == null) || IsRequiredProperty(p))
                {
                    namedArgs.Add(new NamedAttributeArgumentAst(
                        extent: null,
                        argumentName: "Mandatory",
                        argument: new VariableExpressionAst(null, "true", false),
                        expressionOmitted: false
                    ));
                }

                namedArgs.Add(new NamedAttributeArgumentAst(
                    extent: null,
                    argumentName: "Position",
                    argument: new ConstantExpressionAst(null, i),
                    expressionOmitted: false
                ));

                attrs.Add(new AttributeAst(
                    extent: null,
                    typeName: new TypeName(null, "Parameter"),
                    positionalArguments: new List<ExpressionAst>(),
                    namedArguments: namedArgs
                ));

                attrs.Add(new TypeConstraintAst(
                    extent: null,
                    typeName: new TypeName(null, p.PropertyType.FullName)
                ));

                parameters.Add(new ParameterAst(
                    extent: null,
                    name: new VariableExpressionAst(null, p.Name, false),
                    attributes: attrs,
                    defaultValue: null
                ));
            }

            var scriptAttrs = new List<AttributeAst>
    {
        new AttributeAst(null, new TypeName(null, "CmdletBinding"), new List<ExpressionAst>(), null),
        new AttributeAst(null, new TypeName(null, "OutputType"),
            new List<ExpressionAst>{ new TypeExpressionAst(null, new TypeName(null, objectType.FullName)) }, null)
    };

            var paramBlock = new ParamBlockAst(null, scriptAttrs, parameters);

            var statements = new List<StatementAst>();

            var hasParameterlessCtor = objectType.GetConstructor(Type.EmptyTypes) != null;
            StatementAst objectCreationStatement;

            if (hasParameterlessCtor)
            {
                // New-Object -TypeName "Full.Type.Name" as a pipeline (StatementAst)
                objectCreationStatement = new PipelineAst(
                    extent: null,
                    pipelineElements: new CommandBaseAst[]
                    {
            new CommandAst(
    extent: null,
    commandElements: new CommandElementAst[]
    {
        new StringConstantExpressionAst(null, "New-Object", StringConstantType.BareWord),
        new CommandParameterAst(
            extent: null,
            parameterName: "TypeName",
            argument: new StringConstantExpressionAst(null, objectType.FullName, StringConstantType.DoubleQuoted),
            errorPosition: null)
    },
    invocationOperator: TokenKind.Unknown,
    redirections: null)

                    });
            }
            else
            {
                // [Full.Type.Name]::new() wrapped in a pipeline (StatementAst)
                var invokeNewExpr = new InvokeMemberExpressionAst(
                    extent: null,
                    expression: new TypeExpressionAst(null, new TypeName(null, objectType.FullName)),
                    method: new StringConstantExpressionAst(null, "new", StringConstantType.BareWord),
                    arguments: null,
                    @static: true);

                objectCreationStatement = new PipelineAst(
                    extent: null,
                    pipelineElements: new CommandBaseAst[]
                    {
            new CommandExpressionAst(null, invokeNewExpr, redirections: null)
                    });
            }


            var objVar = new VariableExpressionAst(null, "obj", false);
            statements.Add(new AssignmentStatementAst(null, objVar, TokenKind.Equals, objectCreationStatement, null));

            foreach (var p in settableProperties)
            {
                var containsKeyCall = new InvokeMemberExpressionAst(
                    null,
                    new VariableExpressionAst(null, "PSBoundParameters", false),
                    new StringConstantExpressionAst(null, "ContainsKey", StringConstantType.BareWord),
                    new List<ExpressionAst> { new StringConstantExpressionAst(null, p.Name, StringConstantType.SingleQuoted) },
                    @static: false
                );

                var condition = new PipelineAst(null, new CommandBaseAst[]
                {
            new CommandExpressionAst(null, containsKeyCall, null)
                });

                var assignStmt = new AssignmentStatementAst(
                    extent: null,
                    left: new MemberExpressionAst(null, objVar, new StringConstantExpressionAst(null, p.Name, StringConstantType.BareWord), @static: false),
                    @operator: TokenKind.Equals,
                    right: new PipelineAst(
                        extent: null,
                        pipelineElements: new CommandBaseAst[]
                        {
                            new CommandExpressionAst(
                                extent: null,
                                expression: new VariableExpressionAst(null, p.Name, splatted: false),
                                redirections: null)
                        }),
                    errorPosition: null
                );

                statements.Add(new IfStatementAst(
                    null,
                    new List<Tuple<PipelineBaseAst, StatementBlockAst>>
                    {
                Tuple.Create<PipelineBaseAst, StatementBlockAst>(
                    condition,
                    new StatementBlockAst(null, new StatementAst[]{ assignStmt }, null)
                )
                    },
                    elseClause: null
                ));
            }

            statements.Add(
                new PipelineAst(
                    extent: null,
                    pipelineElements: new CommandBaseAst[]
                    {
                        new CommandExpressionAst(
                            extent: null,
                            expression: objVar,
                            redirections: null
                        )
                    }
                )
            );


            var scriptBlock = new ScriptBlockAst(
                extent: null,
                paramBlock: paramBlock,
                statements: new StatementBlockAst(null, statements.ToArray(), null),
                isFilter: false,
                isConfiguration: false
            );

            return new FunctionDefinitionAst(
                extent: null,
                name: functionName,
                isFilter: false,
                isWorkflow: false,
                parameters: null,
                body: scriptBlock
            );
        }

        private static bool IsRequiredProperty(PropertyInfo property)
        {
            var attributes = property.GetCustomAttributes(true);
            return attributes.Any(attr =>
                attr.GetType().Name.Contains("Required") ||
                attr.GetType().Name.Contains("Mandatory"));
        }

        // Generates a <Verb>-<Noun> wrapper with CmdletBinding, OutputType, typed parameters, and reconstruction of complex bodies.
        public static FunctionDefinitionAst GenerateSdkWrapperFunctionAst(
            string verb,
            string noun,
            IEnumerable<FlattenedParameter> flattenedParameters,
            ApiEndpoint endpoint)
        {
            var functionName = $"{verb}-{noun}";
            var flatParams = flattenedParameters.ToList();

            var parameters = new List<ParameterAst>();

            var sdkNamedArgs = new List<NamedAttributeArgumentAst>
            {
                new NamedAttributeArgumentAst(
                    extent: null,
                    argumentName: "Mandatory",
                    argument: new VariableExpressionAst(null, "true", false),
                    expressionOmitted: false
                ),
                new NamedAttributeArgumentAst(
                    extent: null,
                    argumentName: "Position",
                    argument: new ConstantExpressionAst(null, 0),
                    expressionOmitted: false
                )
            };

            parameters.Add(new ParameterAst(
                extent: null,
                name: new VariableExpressionAst(null, "Sdk", false),
                attributes: new List<AttributeBaseAst> {
                    new AttributeAst(
                        extent: null,
                        typeName: new TypeName(null, "Parameter"),
                        positionalArguments: new List<ExpressionAst>(),
                        namedArguments: sdkNamedArgs
                    )
                },
                defaultValue: null
            ));

            for (int i = 0; i < flatParams.Count; i++)
            {
                var fp = flatParams[i];
                var namedArgs = new List<NamedAttributeArgumentAst>
                {
                    new NamedAttributeArgumentAst(
                        extent: null,
                        argumentName: "Position",
                        argument: new ConstantExpressionAst(null, i + 1),
                        expressionOmitted: false
                    )
                };

                if (fp.IsComplex || (fp.Type.IsValueType && Nullable.GetUnderlyingType(fp.Type) == null))
                {
                    namedArgs.Add(new NamedAttributeArgumentAst(
                        extent: null,
                        argumentName: "Mandatory",
                        argument: new VariableExpressionAst(null, "true", false),
                        expressionOmitted: false
                    ));
                }

                var attrs = new List<AttributeBaseAst>
                {
                    new AttributeAst(
                        extent: null,
                        typeName: new TypeName(null, "Parameter"),
                        positionalArguments: new List<ExpressionAst>(),
                        namedArguments: namedArgs
                    ),
                    new TypeConstraintAst(
                        null,
                        new TypeName(null, (Nullable.GetUnderlyingType(fp.Type) ?? fp.Type).FullName)
                    )
                };

                parameters.Add(new ParameterAst(
                    extent: null,
                    name: new VariableExpressionAst(null, fp.Name, false),
                    attributes: attrs,
                    defaultValue: null
                ));
            }

            var outputType = Program.UnwrapGenericType(endpoint.ObjectType);
            var scriptAttrs = new List<AttributeAst>
    {
        new AttributeAst(null, new TypeName(null, "CmdletBinding"), new List<ExpressionAst>(), null)
    };
            if (outputType != typeof(void))
            {
                scriptAttrs.Add(new AttributeAst(
                    null,
                    new TypeName(null, "OutputType"),
                    new List<ExpressionAst> { new TypeExpressionAst(null, new TypeName(null, outputType.FullName)) }
                , null));
            }

            var paramBlock = new ParamBlockAst(null, scriptAttrs, parameters);

            var statements = new List<StatementAst>();
            var orderedGroups = flatParams.GroupBy(fp => fp.SourceGroupPosition).OrderBy(g => g.Key);
            var methodArguments = new List<ExpressionAst>();

            foreach (var group in orderedGroups)
            {
                var first = group.First();

                var isComplexGroup = group.Any(gp => !string.IsNullOrEmpty(gp.SourceProperty));
                if (!isComplexGroup)
                {
                    methodArguments.Add(new VariableExpressionAst(null, first.Name, false));
                    continue;
                }

                var modelType = first.SourceGroupType;
                var objVarName = $"obj{first.SourceGroupPosition}";
                var objVar = new VariableExpressionAst(null, objVarName, false);

                var newObjPipeline = new PipelineAst(
                    extent: null,
                    pipelineElements: new CommandBaseAst[]
                    {
                        new CommandAst(
                            extent: null,
                            commandElements: new CommandElementAst[]
                            {
                                new StringConstantExpressionAst(null, "New-Object", StringConstantType.BareWord),
                                new CommandParameterAst(
                                    extent: null,
                                    parameterName: "TypeName",
                                    argument: new StringConstantExpressionAst(null, modelType.FullName, StringConstantType.DoubleQuoted),
                                    errorPosition: null)
                            },
                            invocationOperator: TokenKind.Unknown,
                            redirections: null)
                    });

                statements.Add(new AssignmentStatementAst(
                    extent: null,
                    left: objVar,
                    @operator: TokenKind.Equals,
                    right: newObjPipeline,
                    errorPosition: null));
                foreach (var fp in group)
                {
                    var containsKeyCall = new InvokeMemberExpressionAst(
                        null,
                        new VariableExpressionAst(null, "PSBoundParameters", false),
                        new StringConstantExpressionAst(null, "ContainsKey", StringConstantType.BareWord),
                        new List<ExpressionAst> { new StringConstantExpressionAst(null, fp.Name, StringConstantType.SingleQuoted) },
                        @static: false
                    );

                    var condition = new PipelineAst(null, new CommandBaseAst[]
                    {
                new CommandExpressionAst(null, containsKeyCall, null)
                    });

                    var assignStmt = new AssignmentStatementAst(
                        extent: null,
                        left: new MemberExpressionAst(null, objVar, new StringConstantExpressionAst(null, fp.Name, StringConstantType.BareWord), @static: false),
                        @operator: TokenKind.Equals,
                        right: new PipelineAst(
                            extent: null,
                            pipelineElements: new CommandBaseAst[]
                            {
                                new CommandExpressionAst(
                                    extent: null,
                                    expression: new VariableExpressionAst(null, fp.Name, splatted: false),
                                    redirections: null)
                            }),
                        errorPosition: null
                    );

                    statements.Add(new IfStatementAst(
                        null,
                        new List<Tuple<PipelineBaseAst, StatementBlockAst>>
                        {
                    Tuple.Create<PipelineBaseAst, StatementBlockAst>(
                        condition,
                        new StatementBlockAst(null, new StatementAst[]{ assignStmt }, null)
                    )
                        },
                        elseClause: null
                    ));
                }

                methodArguments.Add(objVar);
            }

            var invokeMember = new InvokeMemberExpressionAst(
                extent: null,
                expression: new VariableExpressionAst(null, "Sdk", false),
                method: new StringConstantExpressionAst(null, endpoint.MethodName, StringConstantType.BareWord),
                arguments: methodArguments,
                @static: false
            );

            statements.Add(new PipelineAst(null, new CommandExpressionAst(null, invokeMember, null)));

            var scriptBlock = new ScriptBlockAst(
                extent: null,
                paramBlock: paramBlock,
                statements: new StatementBlockAst(null, statements.ToArray(), null),
                isFilter: false,
                isConfiguration: false
            );

            return new FunctionDefinitionAst(
                extent: null,
                name: functionName,
                isFilter: false,
                isWorkflow: false,
                parameters: null,
                body: scriptBlock
            );
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
            public int Position { get; }

            public ParameterGroup(string name, Type type, bool isComplex, IEnumerable<ParameterInfo> properties = null, int position = 0)
            {
                Name = name;
                Type = type;
                IsComplex = isComplex;
                Properties = properties ?? Enumerable.Empty<ParameterInfo>();
                Position = position;
            }
        }

        public class FlattenedParameter
        {
            public string Name { get; }
            public Type Type { get; }
            public bool IsComplex { get; }
            public string SourceGroup { get; }
            public string SourceProperty { get; }
            public int SourceGroupPosition { get; }
            public Type SourceGroupType { get; }

            public bool IsComplexFromGroup => !string.IsNullOrEmpty(SourceProperty);

            public FlattenedParameter(string name, Type type, bool isComplex, string sourceGroup, string sourceProperty, int sourceGroupPosition, Type sourceGroupType)
            {
                Name = name;
                Type = type;
                IsComplex = isComplex;
                SourceGroup = sourceGroup;
                SourceProperty = sourceProperty;
                SourceGroupPosition = sourceGroupPosition;
                SourceGroupType = sourceGroupType;
            }
        }

        public static IEnumerable<ParameterGroup> GetParameterGroups(IEnumerable<ParameterInfo> parameters)
        {
            var paramList = parameters.ToList();
            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];
                if (IsComplexType(param.ParameterType))
                {
                    var props = param.ParameterType.GetProperties()
                        .Select(p => new DummyParameterInfo(p));
                    yield return new ParameterGroup(param.Name, param.ParameterType, true, props, i);
                }
                else
                {
                    yield return new ParameterGroup(param.Name, param.ParameterType, false, null, i);
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
