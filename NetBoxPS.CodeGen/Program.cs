using NetBoxPS.CodeGen.Generators;
using NetBoxPS.CodeGen.Reflection;
using System;
using System.Reflection;



namespace NetBoxPS.CodeGen
{
    public static class Program
    {
        // ...

        public static int Main(string[] args)
        {
            // Load the assembly from the file path provided in args[1]
            var sdkAssembly = Assembly.LoadFrom(args[1]);
            var reflector = new SdkReflector(sdkAssembly);
            var endpoints = reflector.GetEndpoints();

            var generator = new PowerShellFunctionGenerator(args[0]);
            foreach (var ep in endpoints)
            {
                generator.GenerateFunction(ep);
            }

            Console.WriteLine("PowerShell function generation complete!");
            return 0;
        }
    }
}
