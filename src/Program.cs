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

        private static readonly List<TypeDef> s_RemovedTypes = new List<TypeDef>();
        private static ProgramOptions s_ProgamOptions;

        private static void RunWithOptions(ProgramOptions opts)
        {
            s_ProgamOptions = opts;

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

            if (File.Exists(opts.OutputFile) && !opts.Force)
            {
                throw new Exception("Output file exists already. Use --force to override it.");
            }

            byte[] assemblyData = File.ReadAllBytes(opts.AssemblyPath);
            using (MemoryStream ms = new MemoryStream(assemblyData))
            {
                ModuleDefMD module = ModuleDefMD.Load(ms);

                RemoveNonPublicTypes(module.Types);
                RemoveNonPublicMembers(module.Types);

                module.IsILOnly = true;
                module.VTableFixups = null;
                module.IsStrongNameSigned = false;
                module.Assembly.PublicKey = null;
                module.Assembly.HasPublicKey = false;
                ClearCustomAttributes(module.CustomAttributes);

                if (File.Exists(opts.OutputFile))
                {
                    File.Delete(opts.OutputFile);
                }

                module.Write(opts.OutputFile);
            }
        }

        private static void RemoveNonPublicMembers(ICollection<TypeDef> types)
        {
            foreach (var type in types)
            {
                ClearCustomAttributes(type.CustomAttributes);

                foreach (var method in type.Methods.ToList())
                {
                    ClearCustomAttributes(method.CustomAttributes);

                    if (ShouldRemoveMethod(method))
                    {
                        type.Methods.Remove(method);
                    }
                    else
                    {
                        PurgeMethodBody(method);
                    }
                }

                foreach (var field in type.Fields.ToList())
                {
                    ClearCustomAttributes(field.CustomAttributes);

                    if (s_RemovedTypes.Any(d => d.FullName.Equals(field.FieldType.FullName, StringComparison.OrdinalIgnoreCase)) || (!field.IsPublic && !s_ProgamOptions.KeepNonPublic))
                    {
                        type.Fields.Remove(field);
                    }
                }

                foreach (var property in type.Properties.ToList())
                {
                    ClearCustomAttributes(property.CustomAttributes);

                    var getMethod = property.GetMethod;
                    var setMethod = property.SetMethod;

                    if (getMethod != null || setMethod != null)
                    {
                        var propertyType = getMethod != null ? getMethod.ReturnType : setMethod.Parameters[0].Type;
                        bool shouldRemoveProperty =
                            s_RemovedTypes.Any(d => d.FullName.Equals(propertyType.FullName));

                        if (getMethod != null)
                        {
                            ClearCustomAttributes(getMethod.CustomAttributes);

                            if (shouldRemoveProperty)
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
                            ClearCustomAttributes(setMethod.CustomAttributes);

                            if (shouldRemoveProperty)
                            {
                                setMethod = property.SetMethod = null;
                            }
                            else
                            {
                                PurgeMethodBody(setMethod);
                            }
                        }
                    }

                    if (getMethod == null && setMethod == null)
                    {
                        type.Properties.Remove(property);
                    }
                }

                foreach (var @event in type.Events)
                {
                    ClearCustomAttributes(@event.CustomAttributes);

                    var addMethod = @event.AddMethod;
                    var invokeMethod = @event.InvokeMethod;
                    var removeMethod = @event.RemoveMethod;
                    var otherMethods = @event.OtherMethods;

                    if (addMethod != null)
                    {
                        ClearCustomAttributes(addMethod.CustomAttributes);

                        if (ShouldRemoveMethod(addMethod))
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
                        ClearCustomAttributes(invokeMethod.CustomAttributes);

                        if (ShouldRemoveMethod(invokeMethod))
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
                        ClearCustomAttributes(removeMethod.CustomAttributes);

                        if (ShouldRemoveMethod(removeMethod))
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
                        foreach (var otherMethod in otherMethods.ToList())
                        {
                            ClearCustomAttributes(otherMethod.CustomAttributes);

                            if (ShouldRemoveMethod(otherMethod))
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

                RemoveNonPublicMembers(type.NestedTypes);
            }
        }

        private static void RemoveNonPublicTypes(ICollection<TypeDef> types)
        {
            foreach (var type in types.ToList())
            {
                type.CustomAttributes.Clear();

                if ((type.IsNotPublic || type.IsGlobalModuleType) && !s_ProgamOptions.KeepNonPublic)
                {
                    type.Fields.Clear();
                    type.Properties.Clear();
                    type.Events.Clear();
                    type.Methods.Clear();
                    type.NestedTypes.Clear();

                    if (!type.IsGlobalModuleType)
                    {
                        types.Remove(type);
                    }

                    s_RemovedTypes.Add(type);
                    continue;
                }

                RemoveNonPublicTypes(type.NestedTypes);
            }
        }

        private static void ClearCustomAttributes(CustomAttributeCollection collection)
        {
            foreach (var type in s_RemovedTypes)
            {
                collection.RemoveAll(type.FullName);
            }
        }

        private static bool ShouldRemoveMethod(MethodDef method)
        {
            if (s_RemovedTypes.Any(d => method.Parameters.Any(e => e.ParamDef?.FullName?.Equals(d.FullName, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                return true;
            }

            return !method.IsPublic && !s_ProgamOptions.KeepNonPublic;
        }

        private static void PurgeMethodBody(MethodDef method)
        {
            if (!method.IsIL || method.Body == null)
            {
                Console.WriteLine($"Skipped method: {method.FullName} (NO IL BODY)");
                return;
            }

            method.Body = new CilBody();

            if (!s_ProgamOptions.UseRet)
            {
                // This is what Roslyn does with /refout and /refonly
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Throw));
            }
            else
            {
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }

            method.Body.UpdateInstructionOffsets();
        }
    }
}
