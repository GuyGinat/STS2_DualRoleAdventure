using MegaCrit.Sts2.Core.Localization;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalModText
{
    private const string EnglishLanguageCode = "eng";
    private const string UnknownSlotLabel = "?";

    public static bool IsEnglish => LocManager.Instance.Language == EnglishLanguageCode;

    public static string Select(string zh, string en)
    {
        return IsEnglish ? en : zh;
    }

    public static string RoleSlot(string slotLabel)
    {
        return slotLabel == UnknownSlotLabel
            ? Select("未知角色", "Unknown Player")
            : Select($"角色{slotLabel}", $"Player {slotLabel}");
    }

    public static string RestartRoomButton => Select("重启房间", "Restart Room");
    public static string RestartRoomFailed => Select("重启失败，请手动继续游戏", "Restart failed, please continue manually");

    public static string LocalSelfCoopCardTitle => Select("单人多角色", "Local Multi-Control");
    public static string LocalSelfCoopCardDescription => Select(
        "在本机创建2~12名可切换角色，进行本地协作。\n进入后可用 +/- 调整人数。",
        "Create 2-12 switchable local players on this machine.\nUse +/- to change player count.");

    public static string EnteredLocalSelfCoopHint => Select(
        "已进入本地多角色：按 +/- 调整人数（2~12）",
        "Local multi-control enabled: use +/- to set player count (2-12)");

    public static string LobbyEditingSlot(string slotLabel)
    {
        return Select($"大厅编辑角色：{RoleSlot(slotLabel)}", $"Lobby Editor: {RoleSlot(slotLabel)}");
    }

    public static string ControlledSlot(string slotLabel)
    {
        return Select($"控制角色：{RoleSlot(slotLabel)}", $"Controlled Character: {RoleSlot(slotLabel)}");
    }

    public static string LocalPlayerCount(int count)
    {
        return Select($"本地人数：{count}", $"Local Players: {count}");
    }

    public static string RandomCharacterNotSupported => Select(
        "本地多控暂不支持随机角色",
        "Random character is not supported in local multi-control");

    public static string RestSiteAllChosen => Select(
        "休息区选择完成：所有可选角色都已选择",
        "Rest site complete: all selectable players finished");

    public static string RestSiteFocusHint => Select(
        "休息区提示：若未显示选项，请先按 R 或 ] 切换一次角色",
        "Rest site tip: if options are missing, press R or ] to switch once");

    public static string GlobalWakuuLabel => Select("全瓦库", "All Vakuu");

    public static string VakuuControlsPlayer(string slotLabel)
    {
        return Select($"瓦库将接管角色{slotLabel}的操作", $"Vakuu will control {RoleSlot(slotLabel)}");
    }

    public static string VakuuReleasedPlayer(string slotLabel)
    {
        return Select($"瓦库已停止接管角色{slotLabel}的操作", $"Vakuu stopped controlling {RoleSlot(slotLabel)}");
    }

    public static string VakuuControlsAllPlayers()
    {
        return Select("瓦库将接管所有角色的操作", "Vakuu will control all characters");
    }

    public static string VakuuReleasedAllPlayers()
    {
        return Select("瓦库已停止接管所有角色的操作", "Vakuu stopped controlling all characters");
    }
}
