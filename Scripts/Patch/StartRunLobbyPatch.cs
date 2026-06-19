using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// 兼容 0.103.2：开局入口已改为 BeginRunForAllPlayers，具体本地开局流程由 BeginRunLocally 负责。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
internal static class StartRunLobbyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(StartRunLobby __instance, string seed, List<ModifierModel> modifiers)
    {
        if (__instance.NetService is not LocalLoopbackHostGameService)
        {
            return true;
        }

        LocalMultiControlLogger.Info("检测到本地回环 Lobby 开始开局，接管 BeginRunForAllPlayers 逻辑。");

        MethodInfo? updatePreferredAscensionMethod = AccessTools.Method(typeof(StartRunLobby), "UpdatePreferredAscension");
        updatePreferredAscensionMethod?.Invoke(__instance, Array.Empty<object>());

        LobbyBeginRunMessage beginRunMessage = new()
        {
            playersInLobby = __instance.Players,
            seed = seed,
            modifiers = modifiers.Select((modifier) => modifier.ToSerializable()).ToList(),
            act1 = __instance.Act1
        };
        __instance.NetService.SendMessage(beginRunMessage);

        MethodInfo? beginRunLocallyMethod = AccessTools.Method(typeof(StartRunLobby), "BeginRunLocally");
        if (beginRunLocallyMethod == null)
        {
            throw new MissingMethodException(typeof(StartRunLobby).FullName, "BeginRunLocally");
        }

        beginRunLocallyMethod.Invoke(__instance, new object[] { seed, modifiers });
        LocalMultiControlLogger.Info($"本地回环 Lobby 开局流程完成，玩家数={__instance.Players.Count}，seed={seed}");
        return false;
    }
}
