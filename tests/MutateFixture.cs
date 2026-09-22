using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class MutateFixture
{
    static void Main(string[] args)
    {
        using (var a = AssemblyDefinition.ReadAssembly(args[0]))
        {
            if (args[2] == "break-field") a.MainModule.GetType("BongoCat.ShopItem").Fields.Single(f => f.Name == "_price").Name = "_newPrice";
            else
            {
                var m = a.MainModule.GetType("BongoCat.MainCat").Methods.Single(x => x.Name == "Update");
                if (args[2] == "tamper") m.Body.GetILProcessor().InsertAfter(m.Body.Instructions[0],Instruction.Create(OpCodes.Nop));
                else m.Body.GetILProcessor().InsertBefore(m.Body.Instructions[0],Instruction.Create(OpCodes.Nop));
            }
            a.Write(args[1]);
        }
    }
}
