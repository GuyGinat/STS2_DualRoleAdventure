using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalMerchantInventoryRuntime
{
    private static readonly CardType[] CharacterCardTypes =
    {
        CardType.Attack,
        CardType.Attack,
        CardType.Skill,
        CardType.Skill,
        CardType.Power
    };

    private static readonly CardRarity[] ColorlessCardRarities =
    {
        CardRarity.Uncommon,
        CardRarity.Rare
    };

    private static readonly Dictionary<MerchantRoom, Dictionary<ulong, MerchantInventory>> CachedInventories = new();

    public static void Clear()
    {
        CachedInventories.Clear();
    }

    public static MerchantInventory GetOrCreateInventory(MerchantRoom room, Player player)
    {
        if (!CachedInventories.TryGetValue(room, out Dictionary<ulong, MerchantInventory>? perPlayer))
        {
            perPlayer = new Dictionary<ulong, MerchantInventory>();
            CachedInventories[room] = perPlayer;
        }

        if (!perPlayer.TryGetValue(player.NetId, out MerchantInventory? inventory))
        {
            inventory = BuildDualPoolInventory(player);
            perPlayer[player.NetId] = inventory;
            LocalMultiControlLogger.Info($"已生成角色专属商店库存: player={player.NetId}");
        }

        return inventory;
    }

    public static void BindInventoryToRoom(MerchantRoom room, Player player, MerchantInventory inventory)
    {
        IRunState? runState = player.RunState;
        if (runState == null)
        {
            return;
        }

        int playerSlotIndex = runState.GetPlayerSlotIndex(player);
        if (playerSlotIndex < 0 || playerSlotIndex >= room.Inventories.Count)
        {
            LocalMultiControlLogger.Warn(
                $"商店库存绑定失败：角色槽位越界。player={player.NetId}, slot={playerSlotIndex}, inventoryCount={room.Inventories.Count}");
            return;
        }

        room.Inventories[playerSlotIndex] = inventory;
    }

    public static void RefreshShopRoomForPlayer(ulong playerId)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null || runState.CurrentRoom is not MerchantRoom merchantRoom)
        {
            return;
        }

        Player? player = runState.GetPlayer(playerId);
        if (player == null)
        {
            return;
        }

        MerchantInventory inventory = GetOrCreateInventory(merchantRoom, player);
        BindInventoryToRoom(merchantRoom, player, inventory);

        NMerchantRoom? roomNode = NMerchantRoom.Instance;
        if (roomNode == null)
        {
            return;
        }

        NMerchantRoom? refreshedRoomNode = NMerchantRoom.Create(merchantRoom, runState.Players);
        if (refreshedRoomNode == null)
        {
            return;
        }

        NRun.Instance?.SetCurrentRoom(refreshedRoomNode);
        LocalMultiControlLogger.Info($"商店界面已切换到当前角色库存: player={playerId}");
    }

    private static MerchantInventory BuildDualPoolInventory(Player player)
    {
        MerchantInventory inventory = new MerchantInventory(player);

        List<MerchantCardEntry> characterCardEntries = GetField<List<MerchantCardEntry>>(inventory, "_characterCardEntries");
        List<MerchantCardEntry> colorlessCardEntries = GetField<List<MerchantCardEntry>>(inventory, "_colorlessCardEntries");
        List<MerchantRelicEntry> relicEntries = GetField<List<MerchantRelicEntry>>(inventory, "_relicEntries");
        List<MerchantPotionEntry> potionEntries = GetField<List<MerchantPotionEntry>>(inventory, "_potionEntries");

        List<CardModel> characterCards = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();

        int onSaleIndex = player.PlayerRng.Shops.NextInt(CharacterCardTypes.Length);
        for (int i = 0; i < CharacterCardTypes.Length; i++)
        {
            MerchantCardEntry entry = new MerchantCardEntry(player, inventory, characterCards, CharacterCardTypes[i]);
            entry.Populate();
            if (i == onSaleIndex)
            {
                entry.SetOnSale();
            }

            characterCardEntries.Add(entry);
        }

        List<CardModel> colorlessCards = ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();
        foreach (CardRarity rarity in ColorlessCardRarities)
        {
            MerchantCardEntry entry = new MerchantCardEntry(player, inventory, colorlessCards, rarity);
            entry.Populate();
            colorlessCardEntries.Add(entry);
        }

        RelicRarity[] relicRarities =
        {
            RelicFactory.RollRarity(player),
            RelicFactory.RollRarity(player),
            RelicRarity.Shop
        };
        foreach (RelicRarity rarity in relicRarities)
        {
            relicEntries.Add(new MerchantRelicEntry(rarity, player));
        }

        List<PotionModel> potions = PotionFactory.CreateRandomPotionsOutOfCombat(player, 3, player.PlayerRng.Shops);
        foreach (PotionModel potion in potions)
        {
            potionEntries.Add(new MerchantPotionEntry(potion.ToMutable(), player));
        }

        AccessTools.PropertySetter(typeof(MerchantInventory), nameof(MerchantInventory.CardRemovalEntry))?.Invoke(inventory, new object[] { new MerchantCardRemovalEntry(player) });

        Action<PurchaseStatus, MerchantEntry> updateEntriesHandler =
            (Action<PurchaseStatus, MerchantEntry>)Delegate.CreateDelegate(
                typeof(Action<PurchaseStatus, MerchantEntry>),
                inventory,
                AccessTools.Method(typeof(MerchantInventory), "UpdateEntries")!);

        foreach (MerchantEntry entry in inventory.AllEntries)
        {
            entry.PurchaseCompleted += updateEntriesHandler;
        }

        return inventory;
    }

    private static T GetField<T>(MerchantInventory inventory, string fieldName)
    {
        return (T)(AccessTools.Field(typeof(MerchantInventory), fieldName)?.GetValue(inventory)
            ?? throw new InvalidOperationException($"无法读取商店字段: {fieldName}"));
    }
}
