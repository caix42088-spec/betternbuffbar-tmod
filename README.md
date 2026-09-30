# 持续使用防误触 UI

独立的 Terraria 客户端模组，内部名称为 `UninterruptedUse`，当前版本 **1.0.1**。适配tModLoader v2026.07.3.0 稳定版。

从游戏区域按住使用物品键后，鼠标意外滑到增益图标、快捷栏或小地图等 HUD 时，继续按物品原本的规则使用。松开使用键后正常停止，滑过快捷栏不会误换武器。

## 下载与安装

- [下载模组 UninterruptedUse.tmod](dist/UninterruptedUse.tmod)
- [下载完整包：模组、源码、构建脚本和测试](dist/UninterruptedUse-1.0.1.zip)
- [SHA-256 校验值](dist/SHA256SUMS.txt)

在 GitHub 文件页面点击下载按钮获取文件。在 tModLoader 中打开“创意工坊 → 管理模组 → 打开模组文件夹”，放入 `.tmod`，启用“持续使用防误触 UI”并重新加载。

## 开关和交互

默认 **F8** 开启/关闭功能，可以在“设置 → 控制 → 模组快捷键”中修改。开关状态随角色保存。关闭后恢复原版输入；重新开启后，松开再按使用键才能获得保护。

1.0.1 修复鼠标抓起可使用物品时误启用攻击保护的问题。鼠标携带物品时不启用保护，放回、拿起和丢弃物品使用原版流程。右键、丢弃键、背包、菜单、聊天与全屏地图等操作会取消本次按住的保护。

模组不改变伤害、射速、资源消耗、瞄准坐标或自动连用规则。单次使用武器仍需重新按键，引导类物品仍会在松键时结束。服务器无需安装。

## 源码与验证

- [`UninterruptedUse/`](UninterruptedUse/)：模组源码、英文和中文文本，以及详细说明。
- [`tools/`](tools/)：构建与隔离测试脚本，使用 tModLoader 自带编译器和运行时。
- [`tests/UninterruptedUseQA/`](tests/UninterruptedUseQA/)：自动测试代码。
- [验证记录](docs/uninterrupted-use-validation.md) · [118 项通过的检查结果](docs/uninterrupted-use-checks.txt)

```powershell
.\tools\build-uninterrupted-use.ps1 -TmlDirectory 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
.\tools\test-uninterrupted-use.ps1 -TmlDirectory 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
```

编译通过：0 错误、0 警告。隔离测试通过 118 项，覆盖原版物品放回/拿起/丢弃、F8 开关、持续悬停、单次使用/自动连用/引导和钩子重新加载。尚未进行图形客户端实玩、多人联机及其他输入模组的组合测试。

## 贡献者

- [caix42088-spec](https://github.com/caix42088-spec)（模组署名：始源苹果）：项目负责人，提出需求、测试游戏实际表现并报告交互问题。
- **Codex（OpenAI AI 编程助手）**：参与模组实现、交互修复、按键开关、自动测试和文档整理。

详细贡献者说明见 [CONTRIBUTORS.md](CONTRIBUTORS.md)。

## 许可证

本仓库使用已有的 [MIT License](LICENSE)。
