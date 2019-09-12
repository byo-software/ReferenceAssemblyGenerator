using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ReferenceAssemblyGenerator
{
    public class Program
    {
        public static int Main(string[] args)
        {
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

                opts.OutputFile = opts.AssemblyPath.Replace(fileName + extension, fileName + "-reference" + extension);
            }


            byte[] assemblyData = File.ReadAllBytes(opts.AssemblyPath);
            using (MemoryStream ms = new MemoryStream(assemblyData))
            {
                ModuleDefMD module = ModuleDefMD.Load(ms);

                var removedTypes = new List<TypeDef>();

                for (var j = 0; j < module.Types.Count; j++)
                {
                    var type = module.Types[j];

                    if (type.IsNotPublic && !opts.KeepNonPublic)
                    {
                        removedTypes.Add(type);
                        module.Types.Remove(type);
                        continue;
                    }

                    for (var k = 0; k < type.Methods.Count; k++)
                    {
                        var method = type.Methods[k];

                        if (ShouldRemoveMethod(method, opts, removedTypes))
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
                        if (removedTypes.Any(d => d.FullName.Equals(field.FieldType.FullName, StringComparison.OrdinalIgnoreCase))
                            || (!field.IsPublic && !opts.KeepNonPublic))
                        {
                            type.Fields.Remove(field);
                        }
                        else
                        {
                            Console.WriteLine("Keeping field: " + field.FullName);
                        }
                    }

                    for (var k = 0; k < type.Properties.Count; k++)
                    {
                        var property = type.Properties[k];
                        var getMethod = property.GetMethod;
                        var setMethod = property.SetMethod;

                        if (getMethod != null)
                        {
                            if (ShouldRemoveMethod(getMethod, opts, removedTypes))
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
                            if (ShouldRemoveMethod(setMethod, opts, removedTypes))
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
                            if (ShouldRemoveMethod(addMethod, opts, removedTypes))
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
                            if (ShouldRemoveMethod(invokeMethod, opts, removedTypes))
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
                            if (ShouldRemoveMethod(removeMethod, opts, removedTypes))
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
                                if (ShouldRemoveMethod(otherMethod, opts, removedTypes))
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

                        if (addMethod == null
                            && invokeMethod == null
                            && removeMethod == null
                            && otherMethods == null)
                        {
                            type.Events.Remove(@event);
                        }
                    }

                    module.Write(opts.OutputFile);
                }

            }
        }

        private static bool ShouldRemoveMethod(MethodDef method, ProgramOptions opts, List<TypeDef> removedTypes)
        {
            return removedTypes.Any(d => method.Parameters.Any(e => e.ParamDef?.FullName?.Equals(d.FullName, StringComparison.OrdinalIgnoreCase) ?? false))
                   || (!method.IsPublic && !opts.KeepNonPublic);
        }

        private static void PurgeMethodBody(MethodDef method)
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
