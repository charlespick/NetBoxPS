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
            var reflector = new SdkReflector();  // No path needed
            var endpoints = reflector.GetEndpoints();

            var generator = new PowerShellFunctionGenerator(outputPath);
            foreach (var ep in endpoints)
            {
                generator.GenerateFunction(ep);
            }

            Console.WriteLine("PowerShell function generation complete!");
        }

        // Optional: keep a Main for standalone debugging
        static void Main(string[] args)
        {
            var output = args.Length > 0 ? args[0] : @"..\Output\";
            Run(output);
        }
    }
}
