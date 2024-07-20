using Mono.Cecil.Cil;
using Mono.Linker;

namespace MailClient.Linker;

public class FixReleaseBlockWhenDelegateIsCollectedStep : Mono.Linker.Steps.BaseStep
{
    protected override void ProcessAssembly(Mono.Cecil.AssemblyDefinition assembly)
    {
        if (assembly.Name.Name == "Microsoft.iOS" ||
            assembly.Name.Name == "Microsoft.MacCatalyst" ||
            assembly.Name.Name == "Microsoft.macOS")
        {
            var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
            if (action == AssemblyAction.Delete)
                return;

            var runtimeType = assembly.MainModule.GetType("ObjCRuntime", "Runtime");
            if (runtimeType is not null)
            {
                var method = runtimeType.Methods.Where(m => m.Name == "ReleaseBlockWhenDelegateIsCollected").FirstOrDefault();
                if (method is not null)
                {
                    Context.LogMessage("Found ReleaseBlockWhenDelegateIsCollected");
                    var instructions = method.Body.Instructions;

                    // Check for the following sequence:
                    // IL_0000: ldarg.1
                    // IL_0001: brtrue.s IL_000d
                    // IL_0003: ldstr "delegate"
                    // IL_0008: call void ObjCRuntime.ThrowHelper::ThrowArgumentNullException(string)
                    if (instructions[0].OpCode == OpCodes.Ldarg_1 &&
                        instructions[1].OpCode == OpCodes.Brtrue_S &&
                        instructions[2].OpCode == OpCodes.Ldstr &&
                        Equals(instructions[2].Operand, "delegate") &&
                        instructions[3].OpCode == OpCodes.Call)
                    {
                        Context.LogMessage("Need to patch ReleaseBlockWhenDelegateIsCollected");
                        var ilProcessor = method.Body.GetILProcessor();
                        // Remove the null check
                        ilProcessor.RemoveAt(0);
                        ilProcessor.RemoveAt(0);
                        ilProcessor.RemoveAt(0);
                        ilProcessor.RemoveAt(0);

                        if (action == AssemblyAction.Skip || action == AssemblyAction.Copy)
                            Annotations.SetAction(assembly, AssemblyAction.Save);                        
                    }
                }
            }
        }
    }
}