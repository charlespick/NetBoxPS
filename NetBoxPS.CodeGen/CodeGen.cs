using NetBoxPS.CodeGen.Generators;
using NetBoxPS.CodeGen.Reflection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetBoxPS.CodeGen
{
    internal class CodeGen
    {
        public static void Run(string outputPath = @"..\Output\")
        {
            // Because we now have a project reference, we can pass the SDK assembly directly
            var reflector = new SdkReflector();
            var endpoints = reflector.GetEndpoints();

            var generator = new PowerShellFunctionGenerator(outputPath);
            foreach (var ep in endpoints)
            {
                generator.GenerateFunction(ep);
            }

            Console.WriteLine("PowerShell function generation complete!");
        }
    }
}
