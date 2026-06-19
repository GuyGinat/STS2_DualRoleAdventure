using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Saves;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalSelfCoopSaveTag
{
    private const string SaveTagFileName = "local_self_coop_mp.tag";
    private const string V2Prefix = "v2:";
    private const string V3Prefix = "v3:";
    private const int MaxSupportedPlayerCount = 16;

    public static void MarkCurrentProfile(ulong primaryPlayerId, ulong secondaryPlayerId)
    {
        MarkCurrentProfile(new List<ulong> { primaryPlayerId, secondaryPlayerId }, Array.Empty<ulong>());
    }

    public static void MarkCurrentProfile(IReadOnlyList<ulong> playerIds)
    {
        MarkCurrentProfile(playerIds, Array.Empty<ulong>());
    }

    public static void MarkCurrentProfile(IReadOnlyList<ulong> playerIds, IReadOnlyList<ulong> wakuuPlayerIds)
    {
        try
        {
            List<ulong> normalizedPlayers = NormalizeIds(playerIds, maxCount: MaxSupportedPlayerCount);
            if (normalizedPlayers.Count < 2)
            {
                LocalMultiControlLogger.Warn("写入本地多控存档标记失败：有效玩家ID不足2个。");
                return;
            }

            HashSet<ulong> allowed = normalizedPlayers.ToHashSet();
            List<ulong> normalizedWakuuIds = NormalizeIds(wakuuPlayerIds, maxCount: MaxSupportedPlayerCount)
                .Where((playerId) => allowed.Contains(playerId))
                .ToList();

            string serialized =
                $"{V3Prefix}players={string.Join(",", normalizedPlayers)};wakuu={string.Join(",", normalizedWakuuIds)}";
            GodotFileIo fileIo = new(
                UserDataPathProvider.GetProfileScopedPath(SaveManager.Instance.CurrentProfileId, UserDataPathProvider.SavesDir));
            fileIo.WriteFile(SaveTagFileName, serialized);
            LocalMultiControlLogger.Info($"已写入本地多控存档标记: {serialized}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"写入本地多控存档标记失败: {exception.Message}");
        }
    }

    public static void ClearCurrentProfile()
    {
        try
        {
            GodotFileIo fileIo = new(
                UserDataPathProvider.GetProfileScopedPath(SaveManager.Instance.CurrentProfileId, UserDataPathProvider.SavesDir));
            if (fileIo.FileExists(SaveTagFileName))
            {
                fileIo.DeleteFile(SaveTagFileName);
                LocalMultiControlLogger.Info("已清理本地多控存档标记。");
            }
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"清理本地多控存档标记失败: {exception.Message}");
        }
    }

    public static bool TryReadCurrentProfile(out ulong primaryPlayerId, out ulong secondaryPlayerId)
    {
        primaryPlayerId = 0;
        secondaryPlayerId = 0;
        if (!TryReadCurrentProfile(out List<ulong> playerIds, out _) || playerIds.Count < 2)
        {
            return false;
        }

        primaryPlayerId = playerIds[0];
        secondaryPlayerId = playerIds[1];
        return true;
    }

    public static bool TryReadCurrentProfile(out List<ulong> playerIds)
    {
        return TryReadCurrentProfile(out playerIds, out _);
    }

    public static bool TryReadCurrentProfile(out List<ulong> playerIds, out List<ulong> wakuuPlayerIds)
    {
        playerIds = new List<ulong>();
        wakuuPlayerIds = new List<ulong>();

        try
        {
            GodotFileIo fileIo = new(
                UserDataPathProvider.GetProfileScopedPath(SaveManager.Instance.CurrentProfileId, UserDataPathProvider.SavesDir));
            if (!fileIo.FileExists(SaveTagFileName))
            {
                return false;
            }

            string? content = fileIo.ReadFile(SaveTagFileName);
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            if (content.StartsWith(V3Prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseV3(content.Substring(V3Prefix.Length), out playerIds, out wakuuPlayerIds))
                {
                    LocalMultiControlLogger.Warn($"本地多控存档标记格式无效: {content}");
                    return false;
                }

                return true;
            }

            if (content.StartsWith(V2Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string payload = content.Substring(V2Prefix.Length);
                List<ulong> parsedV2 = NormalizeIds(payload.Split(','), maxCount: MaxSupportedPlayerCount);
                if (parsedV2.Count < 2)
                {
                    LocalMultiControlLogger.Warn($"本地多控存档标记格式无效: {content}");
                    return false;
                }

                playerIds = parsedV2;
                return true;
            }

            // 向后兼容旧双人格式: "id1,id2"
            List<ulong> parsedLegacy = NormalizeIds(content.Split(','), maxCount: MaxSupportedPlayerCount);
            if (parsedLegacy.Count == 2)
            {
                playerIds = parsedLegacy;
                return true;
            }

            LocalMultiControlLogger.Warn($"本地多控存档标记格式无效: {content}");
            return false;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"读取本地多控存档标记失败: {exception.Message}");
            return false;
        }
    }

    private static bool TryParseV3(string payload, out List<ulong> playerIds, out List<ulong> wakuuPlayerIds)
    {
        playerIds = new List<ulong>();
        wakuuPlayerIds = new List<ulong>();

        Dictionary<string, string> sections = payload
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select((segment) => segment.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where((parts) => parts.Length == 2)
            .ToDictionary((parts) => parts[0], (parts) => parts[1], StringComparer.OrdinalIgnoreCase);

        if (!sections.TryGetValue("players", out string? playersSection))
        {
            return false;
        }

        playerIds = NormalizeIds(playersSection.Split(','), maxCount: MaxSupportedPlayerCount);
        if (playerIds.Count < 2)
        {
            return false;
        }

        if (sections.TryGetValue("wakuu", out string? wakuuSection))
        {
            HashSet<ulong> allowed = playerIds.ToHashSet();
            wakuuPlayerIds = NormalizeIds(wakuuSection.Split(','), maxCount: MaxSupportedPlayerCount)
                .Where((playerId) => allowed.Contains(playerId))
                .ToList();
        }

        return true;
    }

    private static List<ulong> NormalizeIds(IEnumerable<string> parts, int maxCount)
    {
        return parts
            .Select((part) => part.Trim())
            .Where((part) => ulong.TryParse(part, out _))
            .Select(ulong.Parse)
            .Where((id) => id != 0)
            .Distinct()
            .Take(maxCount)
            .ToList();
    }

    private static List<ulong> NormalizeIds(IReadOnlyList<ulong> ids, int maxCount)
    {
        return ids
            .Where((id) => id != 0)
            .Distinct()
            .Take(maxCount)
            .ToList();
    }
}
