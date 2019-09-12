using System;
using System.IO;
using CommandLine;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ReferenceAssemblyGenerator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            args = new[]
            {
                "C:\\Users\\troja\\source\\repos\\ImperialPlugins\\Plugins\\AdvancedRegions\\bin\\Debug\\net461\\AdvancedRegions.dll"
            };

            var result = Parser.Default.ParseArguments<ProgramOptions>(args)
                .WithParsed(RunWithOptions);

            return result.Tag == ParserResultType.Parsed ? 0 : 1;
        }

        private static void RunWithOptions(ProgramOptions opts)
        {
            if (!File.Exists(opts.AssemblyPath))
            {
                throw new FileNotFoundException("Assembly file was not found", opts.AssemblyPath);
            }

            if (string.IsNullOrEmpty(opts.OutputFile))
            {
                string fileName = Path.GetFileNameWithoutExtension(opts.AssemblyPath);
                string extension = Path.GetExtension(opts.AssemblyPath);

                opts.OutputFile = fileName + "-reference" + extension;
            }


            byte[] assemblyData = File.ReadAllBytes(opts.AssemblyPath);
            using (MemoryStream ms = new MemoryStream(assemblyData))
            {
                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(ms);
                for (var i = 0; i < assemblyDefinition.Modules.Count; i++)
                {
                    var module = assemblyDefinition.Modules[i];
                    for (var j = 0; j < module.Types.Count; j++)
                    {
                        var type = module.Types[j];
                        if (type.IsNotPublic && !opts.KeepNonPublic)
                        {
                            module.Types.Remove(type);
                            continue;
                        }

                        for (var k = 0; k < type.Methods.Count; k++)
                        {
                            var method = type.Methods[k];
                            if (!method.IsPublic && !opts.KeepNonPublic)
                            {
                                type.Methods.Remove(method);
                            }
                            else
                            {
                                PurgeMethodBody(method);
                            }
                        }

                        for (var k = 0; k < type.Fields.Count; k++)
                        {
                            var field = type.Fields[k];
                            if (!field.IsPublic && !opts.KeepNonPublic)
                            {
                                type.Fields.Remove(field);
                            }
                        }

                        for (var k = 0; k < type.Properties.Count; k++)
                        {
                            var property = type.Properties[k];
                            var getMethod = property.GetMethod;
                            var setMethod = property.SetMethod;

                            if (getMethod != null)
                            {
                                if (!getMethod.IsPublic && !opts.KeepNonPublic)
                                {
                                    getMethod = property.GetMethod = null;
                                }
                                else
                                {
                                    PurgeMethodBody(getMethod);
                                }
                            }

                            if (setMethod != null)
                            {
                                if (!setMethod.IsPublic && !opts.KeepNonPublic)
                                {
                                    setMethod = property.SetMethod = null;
                                }
                                else
                                {
                                    PurgeMethodBody(setMethod);
                                }
                            }

                            if (getMethod == null && setMethod == null)
                            {
                                type.Properties.Remove(property);
                            }
                        }

                        for (var k = 0; k < type.Events.Count; k++)
                        {
                            var @event = type.Events[k];
                            var addMethod = @event.AddMethod;
                            var invokeMethod = @event.InvokeMethod;
                            var removeMethod = @event.RemoveMethod;
                            var otherMethods = @event.OtherMethods;

                            if (addMethod != null)
                            {
                                if (!addMethod.IsPublic && !opts.KeepNonPublic)
                                {
                                    addMethod = @event.AddMethod = null;
                                }
                                else
                                {
                                    PurgeMethodBody(addMethod);
                                }
                            }

                            if (invokeMethod != null)
                            {
                                if (!invokeMethod.IsPublic && !opts.KeepNonPublic)
                                {
                                    invokeMethod = @event.InvokeMethod = null;
                                }
                                else
                                {
                                    PurgeMethodBody(invokeMethod);
                                }
                            }

                            if (removeMethod != null)
                            {

                                if (!removeMethod.IsPublic && !opts.KeepNonPublic)
                                {
                                    removeMethod = @event.RemoveMethod = null;
                                }
                                else
                                {
                                    PurgeMethodBody(removeMethod);
                                }
                            }

                            if (otherMethods != null)
                            {
                                foreach (var otherMethod in otherMethods)
                                {
                                    if (!otherMethod.IsPublic && !opts.KeepNonPublic)
                                    {
                                        @event.OtherMethods.Remove(otherMethod);
                                    }
                                    else
                                    {
                                        PurgeMethodBody(otherMethod);
                                    }
                                }
                            }

                            if (@event.OtherMethods.Count == 0)
                            {
                                otherMethods = null;
                            }

                            if (addMethod == null && invokeMethod == null
                                                  && removeMethod == null && otherMethods == null)
                            {
                                type.Events.Remove(@event);
                            }
                        }
                    }

                    if (module.Types.Count == 0)
                    {
                        assemblyDefinition.Modules.Remove(module);
                    }
                }

                assemblyDefinition.Write(opts.OutputFile);
            }
        }

        private static void PurgeMethodBody(MethodDefinition method)
        {
            if (!method.IsIL || method.Body == null)
            {
                return;
            }

            method.Body.Instructions.Clear();
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
    }
}
