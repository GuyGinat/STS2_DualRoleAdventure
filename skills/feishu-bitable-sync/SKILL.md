---
name: feishu-bitable-sync
description: Synchronize LocalMultiControl progress to Feishu Bitable task boards by upserting records after code or docs work. Use when users ask to update Feishu multi-dimensional tables, sync task status, backfill historical tasks from commits/docs, or keep player-visible progress boards current.
---

# Feishu Bitable Sync

Use this skill to standardize Feishu task synchronization in this project and avoid ad-hoc API calls.

## Run Workflow

1. Ensure user-level environment variables are set:
- `FEISHU_APP_ID`
- `FEISHU_APP_SECRET`
- `FEISHU_WIKI_NODE_TOKEN`
- `FEISHU_TABLE_ID` (default task table: `tblsr2MTjWeiuNZ8`)

2. Run the wrapper script in the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\skills\feishu-bitable-sync\scripts\sync_task.ps1 `
  -TaskDescription "任务标题" `
  -LatestNote "完成内容摘要" `
  -Progress "已完成" `
  -DevStatus "已完成" `
  -Category "Bug修复" `
  -Source "AI整理"
```

3. Verify command output:
- `CREATED|<record_id>|...` means a new record is inserted.
- `UPDATED|<record_id>|...` means an existing record is updated.

## Default Targets

- Task board:
  - `https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblsr2MTjWeiuNZ8&view=vewXxBNTOK`
- Player feedback result board:
  - `https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblosEdrnEI0L77n&view=vewjPpNXxV`
- Player form:
  - `https://my.feishu.cn/share/base/form/shrcn5DNr4nXrBDHMkLrHaEaVyh`

## Field Mapping Standard

- Keep these fields aligned with project workflow:
- `任务描述`, `进展`, `开发状态`, `任务分类`, `最新进展记录`, `提交参考`, `数据来源`
- Also keep date and urgency fields updated:
- `开始日期`, `预计完成日期`, `实际完成日期`, `是否延期`, `重要紧急程度`

## Failure Handling

- If API returns permission or forbidden errors, stop direct sync attempts.
- Record equivalent progress in `Mod玩家更新与使用说明.md`.
- Report the failure code and mark the Feishu sync as pending follow-up.

## Resources (optional)

### scripts/
- `scripts/sync_task.ps1`: wrapper for `Scripts/Tools/SyncFeishuTask.ps1`.
