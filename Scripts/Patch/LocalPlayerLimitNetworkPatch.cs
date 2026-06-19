using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch]
internal static class LocalPlayerLimitNetworkPatch
{
    private const int TargetPlayerLimit = 16;
    private const int VanillaSlotIdBits = 2;
    private const int VanillaLobbyListLengthBits = 3;
    private const int TargetSlotIdBits = 4;
    private const int TargetLobbyListLengthBits = 5;

    private static readonly MethodInfo? WriterWriteIntWithBitsMethod =
        AccessTools.Method(typeof(PacketWriter), nameof(PacketWriter.WriteInt), new[] { typeof(int), typeof(int) });
    private static readonly MethodInfo? ReaderReadIntWithBitsMethod =
        AccessTools.Method(typeof(PacketReader), nameof(PacketReader.ReadInt), new[] { typeof(int) });
    private static readonly MethodInfo? WriterWriteListWithBitsMethod = typeof(PacketWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault((method) =>
            method.Name == nameof(PacketWriter.WriteList)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 2
            && method.GetParameters()[1].ParameterType == typeof(int));
    private static readonly MethodInfo? ReaderReadListWithBitsMethod = typeof(PacketReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault((method) =>
            method.Name == nameof(PacketReader.ReadList)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 1
            && method.GetParameters()[0].ParameterType == typeof(int));
    private static readonly int LdcI4MinOpcodeValue = OpCodes.Ldc_I4_M1.Value;
    private static readonly int LdcI4MaxOpcodeValue = OpCodes.Ldc_I4_8.Value;
    private static readonly int LdcI4SOpcodeValue = OpCodes.Ldc_I4_S.Value;
    private static readonly int LdcI4OpcodeValue = OpCodes.Ldc_I4.Value;

    [HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost))]
    private static class StartENetHostPatch
    {
        private static void Prefix(ref int maxClients) => maxClients = Math.Max(maxClients, TargetPlayerLimit);
    }

    [HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost))]
    private static class StartSteamHostPatch
    {
        private static void Prefix(ref int maxClients) => maxClients = Math.Max(maxClients, TargetPlayerLimit);
    }

    [HarmonyPatch(typeof(LobbyPlayer), nameof(LobbyPlayer.Serialize))]
    private static class LobbyPlayerSerializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(instructions, WriterWriteIntWithBitsMethod, VanillaSlotIdBits, TargetSlotIdBits, nameof(LobbyPlayerSerializePatch));
    }

    [HarmonyPatch(typeof(LobbyPlayer), nameof(LobbyPlayer.Deserialize))]
    private static class LobbyPlayerDeserializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(instructions, ReaderReadIntWithBitsMethod, VanillaSlotIdBits, TargetSlotIdBits, nameof(LobbyPlayerDeserializePatch));
    }

    [HarmonyPatch(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Serialize))]
    private static class ClientLobbyJoinResponseSerializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(
                instructions,
                WriterWriteListWithBitsMethod,
                VanillaLobbyListLengthBits,
                TargetLobbyListLengthBits,
                nameof(ClientLobbyJoinResponseSerializePatch));
    }

    [HarmonyPatch(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Deserialize))]
    private static class ClientLobbyJoinResponseDeserializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(
                instructions,
                ReaderReadListWithBitsMethod,
                VanillaLobbyListLengthBits,
                TargetLobbyListLengthBits,
                nameof(ClientLobbyJoinResponseDeserializePatch));
    }

    [HarmonyPatch(typeof(LobbyBeginRunMessage), nameof(LobbyBeginRunMessage.Serialize))]
    private static class LobbyBeginRunSerializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(
                instructions,
                WriterWriteListWithBitsMethod,
                VanillaLobbyListLengthBits,
                TargetLobbyListLengthBits,
                nameof(LobbyBeginRunSerializePatch));
    }

    [HarmonyPatch(typeof(LobbyBeginRunMessage), nameof(LobbyBeginRunMessage.Deserialize))]
    private static class LobbyBeginRunDeserializePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ReplaceBitWidthBeforeCall(
                instructions,
                ReaderReadListWithBitsMethod,
                VanillaLobbyListLengthBits,
                TargetLobbyListLengthBits,
                nameof(LobbyBeginRunDeserializePatch));
    }

    private static IEnumerable<CodeInstruction> ReplaceBitWidthBeforeCall(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo? targetMethod,
        int sourceBitWidth,
        int targetBitWidth,
        string patchName)
    {
        MethodInfo resolvedTargetMethod = targetMethod
            ?? throw new InvalidOperationException($"{patchName}: target method is null.");
        List<CodeInstruction> list = instructions.ToList();
        int replaceCount = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (!IsCallToMethod(list[i], resolvedTargetMethod))
            {
                continue;
            }

            int bitWidthLoadIndex = FindBitWidthLoadIndex(list, i, sourceBitWidth);
            if (bitWidthLoadIndex < 0)
            {
                continue;
            }

            list[bitWidthLoadIndex] = CloneWithNewIntOperand(list[bitWidthLoadIndex], targetBitWidth);
            replaceCount++;
        }

        if (replaceCount == 0)
        {
            throw new InvalidOperationException(
                $"{patchName}: no bit-width operand replaced for method {resolvedTargetMethod.Name} ({sourceBitWidth}->{targetBitWidth}), game code may have changed.");
        }

        return list;
    }

    private static int FindBitWidthLoadIndex(IReadOnlyList<CodeInstruction> instructions, int callIndex, int expectedValue)
    {
        int searchStart = Math.Max(0, callIndex - 8);
        for (int i = callIndex - 1; i >= searchStart; i--)
        {
            if (instructions[i].opcode == OpCodes.Nop)
            {
                continue;
            }

            int? value = ReadLdcI4Nullable(instructions[i]);
            if (value.HasValue)
            {
                return value.Value == expectedValue ? i : -1;
            }

            if (IsTerminatingOpcode(instructions[i].opcode))
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool IsTerminatingOpcode(OpCode opcode)
    {
        FlowControl flowControl = opcode.FlowControl;
        return flowControl == FlowControl.Branch
               || flowControl == FlowControl.Cond_Branch
               || flowControl == FlowControl.Return
               || flowControl == FlowControl.Throw
               || flowControl == FlowControl.Call;
    }

    private static CodeInstruction CloneWithNewIntOperand(CodeInstruction source, int newValue)
    {
        CodeInstruction replacement = new(OpCodes.Ldc_I4, newValue);
        replacement.labels.AddRange(source.labels);
        replacement.blocks.AddRange(source.blocks);
        return replacement;
    }

    private static bool IsCallToMethod(CodeInstruction instruction, MethodInfo targetMethod)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || instruction.operand is not MethodInfo callMethod)
        {
            return false;
        }

        if (callMethod == targetMethod)
        {
            return true;
        }

        MethodInfo normalizedCall = callMethod.IsGenericMethod ? callMethod.GetGenericMethodDefinition() : callMethod;
        MethodInfo normalizedTarget = targetMethod.IsGenericMethod ? targetMethod.GetGenericMethodDefinition() : targetMethod;
        return normalizedCall == normalizedTarget;
    }

    private static int? ReadLdcI4Nullable(CodeInstruction instruction)
    {
        int opcodeValue = instruction.opcode.Value;
        return opcodeValue switch
        {
            _ when opcodeValue >= LdcI4MinOpcodeValue && opcodeValue <= LdcI4MaxOpcodeValue => opcodeValue - (LdcI4MinOpcodeValue + 1),
            _ when opcodeValue == LdcI4SOpcodeValue && instruction.operand is sbyte shortValue => shortValue,
            _ when opcodeValue == LdcI4OpcodeValue && instruction.operand is int intValue => intValue,
            _ => null
        };
    }
}
