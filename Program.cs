using System;
using System.IO;
using System.Xml.Linq;
using Mono.Cecil;

namespace AssemblyInspector
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: AssemblyInspector <path-to-assembly>");
                return;
            }

            string assemblyPath = args[0];

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"[ERROR] File '{assemblyPath}' not found.");
                return;
            }

            try
            {
                Console.WriteLine($"[INFO] Reading assembly: {assemblyPath}");
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

                string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                string outputPath = Path.Combine(assemblyDirectory, $"{assemblyName}_Export.xml");

                var xml = new XElement("Assembly", new XAttribute("Name", assembly.Name.Name));

                foreach (var module in assembly.Modules)
                {
                    Console.WriteLine($"[INFO] Processing module: {module.Name}");

                    foreach (var type in module.Types)
                    {
                        if (!type.IsPublic)
                        {
                            Console.WriteLine($"[DEBUG] Skipping non-public type: {type.FullName}");
                            continue;
                        }

                        Console.WriteLine($"[INFO] Processing type: {type.FullName}");
                        var typeElement = new XElement("Type",
                            new XAttribute("Namespace", type.Namespace),
                            new XAttribute("FullName", type.FullName));

                        foreach (var field in type.Fields)
                        {
                            if (!field.IsPublic)
                            {
                                Console.WriteLine($"[DEBUG] Skipping non-public field: {field.Name}");
                                continue;
                            }

                            Console.WriteLine($"[INFO] Found public field: {field.Name} ({field.FieldType.FullName})");
                            var fieldElement = new XElement("Field",
                                new XAttribute("Name", field.Name),
                                new XAttribute("Type", field.FieldType.FullName));
                            typeElement.Add(fieldElement);
                        }

                        foreach (var method in type.Methods)
                        {
                            if (!method.IsPublic)
                            {
                                Console.WriteLine($"[DEBUG] Skipping non-public method: {method.Name}");
                                continue;
                            }

                            Console.WriteLine($"[INFO] Found public method: {method.Name} (Return: {method.ReturnType.FullName})");
                            var methodElement = new XElement("Method",
                                new XAttribute("Name", method.Name),
                                new XAttribute("ReturnType", method.ReturnType.FullName));

                            foreach (var param in method.Parameters)
                            {
                                Console.WriteLine($"[INFO] Adding parameter: {param.Name} ({param.ParameterType.FullName})");
                                var paramElement = new XElement("Parameter",
                                    new XAttribute("Name", param.Name),
                                    new XAttribute("Type", param.ParameterType.FullName));
                                methodElement.Add(paramElement);
                            }

                            typeElement.Add(methodElement);
                        }

                        xml.Add(typeElement);
                    }
                }

                xml.Save(outputPath);
                Console.WriteLine($"[SUCCESS] Export complete. XML saved to '{outputPath}'.");
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("[ERROR] Invalid assembly format.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error: {ex.Message}");
            }
        }
    }
}
