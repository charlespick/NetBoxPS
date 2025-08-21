using NetBoxPS.CodeGen.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetBoxPS.CodeGen.Generators
{
    public class PowerShellFunctionGenerator
    {
        private readonly string _outputPath;

        public PowerShellFunctionGenerator(string outputPath)
        {
            _outputPath = outputPath;
            Directory.CreateDirectory(_outputPath);
        }

        public void GenerateFunction(EndpointDefinition endpoint)
        {
            var verb = VerbHelper.MapHttpMethodToVerb(endpoint.HttpMethod);
            var noun = NounHelper.BuildNounFromRoute(endpoint.Route);

            var psFunctionName = $"{verb}-{noun}";

            var psContent = $@"function {psFunctionName} {{
    param (
        # TODO: Flatten parameters here
    )

    # TODO: Construct nested objects if needed

    # TODO: Call SDK method
}}";

            File.WriteAllText(Path.Combine(_outputPath, $"{psFunctionName}.ps1"), psContent);
            Console.WriteLine($"Generated {psFunctionName}.ps1");
        }
    }
}
