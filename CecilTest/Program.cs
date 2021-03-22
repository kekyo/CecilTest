using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;

namespace CecilTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ////////////////////////////

            var basePath = Path.GetFullPath(
                Path.Combine(
                    "..", "..", "..", "..", "TestTarget", "bin", "Debug", "netstandard2.0"));

            var assemblyResolver = new DefaultAssemblyResolver();
            assemblyResolver.AddSearchDirectory(basePath);

            var testAssembly = AssemblyDefinition.ReadAssembly(
                Path.Combine(basePath, "TestTarget.dll"),
                new ReaderParameters
                {
                    AssemblyResolver = assemblyResolver,
                }
            );

            var module = testAssembly.MainModule;

            ////////////////////////////
            // Contains:
            // public delegate void TestGenericDelegate<TValue>(TValue value);

            var testGenericDelegateTypeT = module.Types.
                First(t => t.Name == "TestGenericDelegate`1")!;

            var testGenericDelegateType = new GenericInstanceType(
                module.ImportReference(testGenericDelegateTypeT));
            testGenericDelegateType.GenericArguments.Add(module.TypeSystem.String);

            // HOW TO?:
            //   Want to: TestGenericDelegate<string>
            //   Will get unresolved generic argument constructor ref...
            var testGenericDelegateTypeCtor = testGenericDelegateType.Resolve().
                Methods.First(m => m.IsConstructor);

            ////////////////////////////
            // int.Parse(string)

            var intParseMethod = module.TypeSystem.Int32.
                Resolve().
                Methods.
                First(m => m.IsPublic && m.IsStatic &&
                    (m.Name == "Parse") &&
                    (m.Parameters.SingleOrDefault(p =>
                        p.ParameterType.FullName == "System.String")) != null);

            ////////////////////////////
            // public static class FooClass
            // {
            //   public static Delegate FooMethod() =>
            //     new TestGenericDelegate<string>(int.Parse);
            // }

            var fooClass = new TypeDefinition(
                "TestTarget",
                "FooClass",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);

            var fooMethod = new MethodDefinition(
                "FooMethod",
                MethodAttributes.Public | MethodAttributes.Static,
                module.TypeSystem.String);
            var ilp = fooMethod.Body.GetILProcessor();

            ilp.Append(Instruction.Create(OpCodes.Ldnull));
            ilp.Append(Instruction.Create(OpCodes.Ldftn,
                module.ImportReference(intParseMethod)));
            ilp.Append(Instruction.Create(OpCodes.Newobj,
                module.ImportReference(testGenericDelegateTypeCtor)));
            ilp.Append(Instruction.Create(OpCodes.Ret));

            fooClass.Methods.Add(fooMethod);
            module.Types.Add(fooClass);

            ////////////////////////////
            // Write

            testAssembly.Write("TestTarget_modified.dll");
        }
    }
}
