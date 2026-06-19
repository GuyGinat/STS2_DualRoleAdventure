using System.Collections.Generic;
using System.Threading.Tasks;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LocalMultiControl.Scripts.Models.Relics;

internal sealed class LocalWakuuStarterRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "whispering_earring";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.ForEnergy(this) };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new EnergyVar(1) };

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner)
        {
            return amount;
        }

        return amount + DynamicVars.Energy.BaseValue;
    }

    public override Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
    {
        return LocalWakuuRelicRuntime.ExecuteBeforePlayPhaseStartAsync(this, choiceContext, player);
    }
}
