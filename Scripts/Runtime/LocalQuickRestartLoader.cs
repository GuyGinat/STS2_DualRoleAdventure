using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalQuickRestartLoader
{
    public static async Task<bool> TryLoadDirectlyAsync()
    {
        try
        {
            if (!LocalSelfCoopSaveTag.TryReadCurrentProfile(out List<ulong> playerIds) || playerIds.Count < 2)
            {
                LocalMultiControlLogger.Warn("快速重启失败：未找到有效本地多控玩家标记。");
                return false;
            }

            ulong primaryPlayerId = playerIds[0];
            LocalSelfCoopContext.UseSavedPlayerIds(playerIds);

            ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(primaryPlayerId);
            if (!readSaveResult.Success || readSaveResult.SaveData == null)
            {
                LocalMultiControlLogger.Warn("快速重启失败：多人存档读取失败。");
                return false;
            }

            SerializableRun saveData = readSaveResult.SaveData;
            if (!LocalSelfCoopContext.IsSaveOwnedByLocalSelfCoop(saveData))
            {
                LocalSelfCoopSaveTag.ClearCurrentProfile();
                LocalMultiControlLogger.Warn("快速重启失败：存档玩家与本地多控标记不一致。");
                return false;
            }

            LocalLoopbackHostGameService netService = new LocalLoopbackHostGameService(primaryPlayerId);
            LocalSelfCoopContext.Enable(netService);

            LoadRunLobby lobby = new LoadRunLobby(netService, NoopLoadRunLobbyListener.Instance, saveData);
            lobby.AddLocalHostPlayer();

            NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");
            game.RemoteCursorContainer.Initialize(lobby.InputSynchronizer, lobby.ConnectedPlayerIds);
            game.ReactionContainer.InitializeNetworking(netService);

            SerializablePlayer localPlayer = saveData.Players.First((player) => player.NetId == primaryPlayerId);
            if (localPlayer.CharacterId != null)
            {
                CharacterModel transitionCharacter = ModelDb.GetById<CharacterModel>(localPlayer.CharacterId);
                SfxCmd.Play(transitionCharacter.CharacterTransitionSfx);
                await game.Transition.FadeOut(0.8f, transitionCharacter.CharacterSelectTransitionPath);
            }
            else
            {
                await game.Transition.FadeOut();
            }

            RunState runState = RunState.FromSerializable(saveData);
            await RunManager.Instance.SetUpSavedMultiplayer(runState, lobby);
            await game.LoadRun(runState, saveData.PreFinishedRoom);
            lobby.CleanUp(disconnectSession: false);
            await game.Transition.FadeIn();
            LocalMultiControlLogger.Info("ESC 快速重启已直接完成读档并进入游戏。");
            return true;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Error($"ESC 快速重启直接读档失败: {exception}");
            return false;
        }
    }

    private sealed class NoopLoadRunLobbyListener : ILoadRunLobbyListener
    {
        internal static readonly NoopLoadRunLobbyListener Instance = new NoopLoadRunLobbyListener();

        public void PlayerConnected(ulong playerId)
        {
        }

        public void RemotePlayerDisconnected(ulong playerId)
        {
        }

        public Task<bool> ShouldAllowRunToBegin()
        {
            return Task.FromResult(true);
        }

        public void BeginRun()
        {
        }

        public void PlayerReadyChanged(ulong playerId)
        {
        }

        public void LocalPlayerDisconnected(NetErrorInfo info)
        {
        }
    }
}
