using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace LocalMultiControl.Scripts.Runtime;

internal static class CrystalSphereMirrorRuntime
{
    private const string CrystalSphereEventId = "CRYSTAL_SPHERE";

    public static bool IsInCrystalSphereEventContext(Player? player)
    {
        if (player?.RunState == null)
        {
            return false;
        }

        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode || !RunManager.Instance.IsInProgress)
        {
            return false;
        }

        if (player.RunState.CurrentRoom is not EventRoom)
        {
            return false;
        }

        if (RunManager.Instance.EventSynchronizer.IsShared)
        {
            return false;
        }

        try
        {
            MegaCrit.Sts2.Core.Models.EventModel eventForPlayer = RunManager.Instance.EventSynchronizer.GetEventForPlayer(player);
            return eventForPlayer.Id.Entry == CrystalSphereEventId;
        }
        catch
        {
            return false;
        }
    }

    public static List<Player> GetOtherPlayers(Player sourcePlayer)
    {
        return sourcePlayer.RunState.Players
            .Where((candidate) => candidate.NetId != sourcePlayer.NetId)
            .ToList();
    }
}
