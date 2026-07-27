using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using LocalMultiControl.Scripts.Runtime;

namespace LocalMultiControl.Scripts;

[ModInitializer(nameof(Init))]
public partial class Entry
{
    private const string BuildMarker = "Revival v1.31 loaded (game v0.109.0, marker=2026-07-27-r1)";

    private static Harmony? _harmony;

    public static void Init()
    {
        LocalMultiControlLogger.Info("开始初始化 Harmony 补丁。");
        LocalMultiControlLogger.Info(BuildMarker);
        LocalWakuuRelicLocalization.Initialize();
        _harmony = new Harmony("sts2.dualroleadventure");
        _harmony.PatchAll();
        LocalMultiControlLogger.Info("Mod 初始化完成。");
    }
}
