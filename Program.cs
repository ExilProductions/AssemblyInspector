using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

namespace AssemblyInspector
{
    class Program
    {
        static StreamWriter logWriter;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: AssemblyInspector <path-to-assembly> [--namespace <namespaceName>]");
                return;
            }

            string assemblyPath = args[0];
            string namespaceArg = null;

            //check if the namespace flag was passed
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--namespace", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        namespaceArg = args[i + 1]; //get the namespace name
                    else
                        namespaceArg = ""; //if flag only show all avalible namespaces
                    break;
                }
            }

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"[ERROR] File '{assemblyPath}' not found.");
                return;
            }

            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(appDirectory, $"{assemblyName}_{timestamp}.log");

            using (logWriter = new StreamWriter(logPath))
            {
                logWriter.AutoFlush = true;

                try
                {
                    Log($"[INFO] Reading assembly: {assemblyPath}");
                    var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

                    //get all namespaces
                    var allNamespaces = assembly.Modules
                        .SelectMany(m => m.Types)
                        .Where(t => t.IsPublic && t.Name != "_Module_" && !t.Name.StartsWith("<"))
                        .Select(t => t.Namespace)
                        .Where(ns => !string.IsNullOrEmpty(ns))
                        .Distinct()
                        .OrderBy(ns => ns)
                        .ToList();

                    //show namespaces if only the flag was passed
                    if (namespaceArg == "")
                    {
                        Console.WriteLine("Available namespaces:");
                        foreach (var ns in allNamespaces)
                        {
                            Console.WriteLine($"- {ns}");
                        }
                        return;
                    }

                    string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                    string outputPath = Path.Combine(assemblyDirectory, $"{assemblyName}_Export.xml");
                    var xml = new XElement("Assembly", new XAttribute("Name", assembly.Name.Name));

                    foreach (var module in assembly.Modules)
                    {
                        Log($"[INFO] Processing module: {module.Name}");

                        var sortedTypes = module.Types
                            .Where(t => t.IsPublic && t.Name != "_Module_" && !t.Name.StartsWith("<"))
                            .Where(t => string.IsNullOrEmpty(namespaceArg) || t.Namespace == namespaceArg)
                            .OrderBy(t => t.FullName);

                        foreach (var type in sortedTypes)
                        {
                            Log($"[INFO] Processing type: {type.FullName}");
                            var typeElement = new XElement("Type",
                                new XAttribute("Namespace", type.Namespace),
                                new XAttribute("FullName", type.FullName));

                            //fields
                            var sortedFields = type.Fields
                                .Where(f => f.IsPublic && !f.Name.Contains("k__BackingField"))
                                .OrderBy(f => f.Name);

                            foreach (var field in sortedFields)
                            {
                                Log($"[INFO] Found public field: {field.Name}");
                                typeElement.Add(new XElement("Field",
                                    new XAttribute("Name", field.Name),
                                    new XAttribute("Type", SimplifyTypeName(field.FieldType.FullName))));
                            }

                            //methods
                            var sortedMethods = type.Methods
                                .Where(m => m.IsPublic &&
                                            !m.IsGetter &&
                                            !m.IsSetter &&
                                            m.Name != "get_Il2CppType" &&
                                            !m.Name.StartsWith("<") &&
                                            !m.IsConstructor) //remove all constructors
                                .OrderBy(m => m.Name);

                            foreach (var method in sortedMethods)
                            {
                                Log($"[INFO] Found public method: {method.Name}");
                                var methodElement = new XElement("Method",
                                    new XAttribute("Name", method.Name),
                                    new XAttribute("ReturnType", SimplifyTypeName(method.ReturnType.FullName)));

                                foreach (var param in method.Parameters)
                                {
                                    methodElement.Add(new XElement("Parameter",
                                        new XAttribute("Name", param.Name),
                                        new XAttribute("Type", SimplifyTypeName(param.ParameterType.FullName))));
                                }

                                typeElement.Add(methodElement);
                            }

                            xml.Add(typeElement);
                        }
                    }

                    xml.Save(outputPath);
                    Log($"[SUCCESS] Export complete. XML saved to '{outputPath}'");
                    Log($"[INFO] Log file saved as '{logPath}'");
                }
                catch (BadImageFormatException)
                {
                    Log("[ERROR] Invalid assembly format.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Unexpected error: {ex.Message}");
                }
            }
        }

        static void Log(string message)
        {
            string timestampedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            Console.WriteLine(timestampedMessage);
            logWriter?.WriteLine(timestampedMessage);
        }

        static string SimplifyTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;

            int genericIndex = fullName.IndexOf('`');
            if (genericIndex > 0)
                fullName = fullName.Substring(0, genericIndex);

            int lastDot = fullName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullName.Length - 1)
                return fullName.Substring(lastDot + 1);

            return fullName;
        }
    }
}
