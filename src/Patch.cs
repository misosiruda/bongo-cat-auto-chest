using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Patch
{
    const string HelperName = "BongoAutoChest";
    static IEnumerable<TypeDefinition> Types(TypeDefinition t)
    { yield return t; foreach (var n in t.NestedTypes) foreach (var x in Types(n)) yield return x; }
    static MethodDefinition Method(AssemblyDefinition a, string type, string name, params string[] parameters)
    {
        var t = a.MainModule.GetType(type);
        if (t == null) throw new Exception("Missing type: " + type);
        var m = t.Methods.SingleOrDefault(x => x.Name == name && !x.IsStatic && x.ReturnType.FullName == "System.Void"
            && x.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
        if (m == null || !m.HasBody) throw new Exception("Incompatible method: " + type + "." + name);
        return m;
    }
    static void Fields(AssemblyDefinition a)
    {
        string[] specs = {
            "BongoCat.Shop|_shopItem|BongoCat.ShopItem", "BongoCat.Shop|_openingChest|System.Boolean",
            "BongoCat.ShopItem|_waitingForServer|System.Boolean", "BongoCat.ShopItem|_price|System.Int32",
            "BongoCat.Multiplayer.OpenChestForMember|_targetUser|Heathen.SteamworksIntegration.SteamUserData",
            "BongoCat.Multiplayer.OpenChestForMember|_chestType|BongoCat.ChestType",
            "BongoCat.Multiplayer.OpenChestForMember|_setChestActive|BongoCat.Multiplayer.SetActiveBasedOnLobbyMemberValue",
            "BongoCat.Multiplayer.OpenChestForMember|_hasClicked|System.Boolean",
            "BongoCat.Multiplayer.SetActiveBasedOnLobbyMemberValue|_lobbyMemberData|Heathen.SteamworksIntegration.SteamLobbyMemberData",
            "BongoCat.Multiplayer.SetActiveBasedOnLobbyMemberValue|_key|System.String",
            "BongoCat.Multiplayer.SetActiveBasedOnLobbyMemberValue|_gameObject|UnityEngine.GameObject"
        };
        foreach (string spec in specs)
        {
            string[] p = spec.Split('|');
            var t = a.MainModule.GetType(p[0]);
            var f = t == null ? null : t.Fields.SingleOrDefault(x => x.Name == p[1]);
            if (f == null || f.IsStatic || f.IsPublic || f.FieldType.FullName != p[2])
                throw new Exception("Incompatible field: " + spec);
        }
    }
    static string Operand(object o, MethodBody body, int skip)
    {
        if (o == null) return "";
        var i = o as Instruction;
        if (i != null) return "target:" + (body.Instructions.IndexOf(i) - skip);
        var many = o as Instruction[];
        if (many != null) return string.Join(",", many.Select(x => Operand(x, body, skip)));
        return o.ToString();
    }
    static string Body(MethodDefinition m, int skip)
    {
        if (!m.HasBody) return "no-body";
        var b = m.Body;
        return b.InitLocals + "|" + string.Join(",", b.Variables.Select(v => v.VariableType.FullName)) + "|"
            + string.Join("\n", b.Instructions.Skip(skip).Select(i => i.OpCode.Code + " " + Operand(i.Operand, b, skip))) + "|"
            + string.Join("\n", b.ExceptionHandlers.Select(h => h.HandlerType + ":" + h.CatchType + ":"
                + Operand(h.TryStart,b,skip) + ":" + Operand(h.TryEnd,b,skip) + ":"
                + Operand(h.HandlerStart,b,skip) + ":" + Operand(h.HandlerEnd,b,skip) + ":" + Operand(h.FilterStart,b,skip)));
    }
    static bool Calls(Instruction i, string method)
    {
        var m = i.Operand as MethodReference;
        return i.OpCode == OpCodes.Call && m != null && m.FullName == method
            && m.DeclaringType.Scope.Name == HelperName;
    }
    static void Verify(AssemblyDefinition original, AssemblyDefinition patched)
    {
        if (original.MainModule.AssemblyReferences.Any(r => r.Name == HelperName)) throw new Exception("Backup is already patched.");
        var expectedRefs = original.MainModule.AssemblyReferences.Select(r => r.FullName).OrderBy(s => s);
        // Cecil's older importer also emitted a harmless self-reference for the callback parameter.
        if (!expectedRefs.SequenceEqual(patched.MainModule.AssemblyReferences.Where(r => r.Name != HelperName && r.FullName != original.Name.FullName).Select(r => r.FullName).OrderBy(s => s))
            || patched.MainModule.AssemblyReferences.Count(r => r.Name == HelperName) != 1)
            throw new Exception("Unexpected assembly references.");
        var a = original.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
        var b = patched.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
        if (!a.Keys.OrderBy(s => s).SequenceEqual(b.Keys.OrderBy(s => s))) throw new Exception("Method set changed.");
        var update = Method(patched,"BongoCat.MainCat","Update");
        var callback = Method(patched,"BongoCat.ShopItem","Callback","System.Int32","System.Boolean");
        if (!Calls(update.Body.Instructions[0],"System.Void BongoAutoChest.AutoChest::Tick()")) throw new Exception("Invalid Update hook.");
        var ins = callback.Body.Instructions;
        if (ins.Count < 4 || ins[0].OpCode != OpCodes.Ldarg_0 || ins[1].OpCode != OpCodes.Ldarg_1 || ins[2].OpCode != OpCodes.Ldarg_2
            || !Calls(ins[3],"System.Void BongoAutoChest.AutoChest::OnOwnResult(BongoCat.ShopItem,System.Int32,System.Boolean)"))
            throw new Exception("Invalid Callback hook.");
        foreach (var pair in a)
        {
            int skip = pair.Key == update.FullName ? 1 : pair.Key == callback.FullName ? 4 : 0;
            if (Body(pair.Value,0) != Body(b[pair.Key],skip)) throw new Exception("Unexpected body change: " + pair.Key);
        }
        Console.WriteLine("Verified: all " + a.Count + " original method bodies preserved after two prefixes.");
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "inspect")
            {
                using (var a = AssemblyDefinition.ReadAssembly(args[1]))
                    Console.WriteLine(a.MainModule.AssemblyReferences.Any(r => r.Name == HelperName) ? "patched" : "clean");
                return 0;
            }
            if (args.Length == 3 && args[0] == "verify")
            {
                using (var a = AssemblyDefinition.ReadAssembly(args[1]))
                using (var b = AssemblyDefinition.ReadAssembly(args[2])) Verify(a,b);
                return 0;
            }
            if (args.Length != 4) throw new Exception("Usage: Patch original.dll helper.dll managed-dir output.dll | inspect dll | verify original patched");
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(args[2]);
            resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(args[1])));
            using (var a = AssemblyDefinition.ReadAssembly(args[0],new ReaderParameters { AssemblyResolver = resolver }))
            using (var h = AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters { AssemblyResolver = resolver }))
            {
                if (a.MainModule.AssemblyReferences.Any(r => r.Name == HelperName)) throw new Exception("Already patched.");
                Fields(a);
                var update = Method(a,"BongoCat.MainCat","Update");
                var callback = Method(a,"BongoCat.ShopItem","Callback","System.Int32","System.Boolean");
                var helperType = h.MainModule.GetType("BongoAutoChest.AutoChest");
                var tick = a.MainModule.ImportReference(helperType.Methods.Single(m => m.Name == "Tick"));
                var result = a.MainModule.ImportReference(helperType.Methods.Single(m => m.Name == "OnOwnResult"));
                result.Parameters[0].ParameterType = a.MainModule.GetType("BongoCat.ShopItem");
                foreach (var self in a.MainModule.AssemblyReferences.Where(r => r.FullName == a.Name.FullName).ToArray())
                    a.MainModule.AssemblyReferences.Remove(self);
                update.Body.GetILProcessor().InsertBefore(update.Body.Instructions[0],Instruction.Create(OpCodes.Call,tick));
                var il = callback.Body.GetILProcessor(); var first = callback.Body.Instructions[0];
                il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0)); il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_2)); il.InsertBefore(first,Instruction.Create(OpCodes.Call,result));
                callback.Body.MaxStackSize = Math.Max(callback.Body.MaxStackSize,3);
                a.Write(args[3]);
            }
            using (var a = AssemblyDefinition.ReadAssembly(args[0]))
            using (var b = AssemblyDefinition.ReadAssembly(args[3])) Verify(a,b);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
