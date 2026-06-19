using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RewardSynchronizer), nameof(RewardSynchronizer.SyncLocalObtainedCard))]
internal static class RewardCardMirrorPatch
{
    private static readonly AsyncLocal<bool> IsMirroring = new();

    [HarmonyPostfix]
    private static void Postfix(RewardSynchronizer __instance, CardModel card)
    {
        if (IsMirroring.Value || !LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        Player? sourcePlayer = ResolveSourcePlayer(__instance);
        if (sourcePlayer == null || !CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(sourcePlayer))
        {
            return;
        }

        TaskHelper.RunSafely(MirrorCardToOtherPlayersAsync(sourcePlayer, card));
    }

    private static Player? ResolveSourcePlayer(RewardSynchronizer synchronizer)
    {
        ulong localPlayerId = AccessTools.Field(typeof(RewardSynchronizer), "_localPlayerId")?.GetValue(synchronizer) as ulong? ?? 0UL;
        if (localPlayerId == 0UL)
        {
            return null;
        }

        IPlayerCollection? playerCollection = AccessTools.Field(typeof(RewardSynchronizer), "_playerCollection")
            ?.GetValue(synchronizer) as IPlayerCollection;
        return playerCollection?.GetPlayer(localPlayerId);
    }

    private static async Task MirrorCardToOtherPlayersAsync(Player sourcePlayer, CardModel card)
    {
        IsMirroring.Value = true;
        try
        {
            foreach (Player otherPlayer in CrystalSphereMirrorRuntime.GetOtherPlayers(sourcePlayer))
            {
                CardModel mirroredCard = otherPlayer.RunState.CreateCard(card, otherPlayer);
                await CardPileCmd.Add(mirroredCard, PileType.Deck);
                LocalMultiControlLogger.Info(
                    $"水晶球事件卡牌奖励同步: source={sourcePlayer.NetId}, target={otherPlayer.NetId}, card={mirroredCard.Id.Entry}");
            }
        }
        finally
        {
            IsMirroring.Value = false;
        }
    }
}
