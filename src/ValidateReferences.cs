using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
class ValidateReferences
{
    static IEnumerable<TypeDefinition> Types(TypeDefinition t) { yield return t; foreach (var n in t.NestedTypes) foreach (var x in Types(n)) yield return x; }
    static int Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        foreach (var old in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(old);
        resolver.AddSearchDirectory(args[1]);
        using (var asm = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        {
            var members = asm.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods).Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MemberReference>()
                .GroupBy(m => m.FullName).Select(g => g.First());
            int errors = 0, checkedCount = 0;
            foreach (var member in members)
            {
                try
                {
                    var method = member as MethodReference;
                    var field = member as FieldReference;
                    var type = member as TypeReference;
                    if (method != null && method.Resolve() == null || field != null && field.Resolve() == null || type != null && type.Resolve() == null)
                    { Console.WriteLine("MISSING " + member.FullName); errors++; }
                    checkedCount++;
                }
                catch (Exception ex) { Console.WriteLine("ERROR " + member.FullName + " " + ex.Message); errors++; }
            }
            Console.WriteLine("Checked " + checkedCount + " references against game assemblies; missing=" + errors);
            return errors == 0 ? 0 : 1;
        }
    }
}
