# UninterruptedUse 1.0.1 验证记录

日期：2026-09-30。环境：Terraria 1.4.4.9 / tModLoader v2026.07.3.0（官方最新稳定版）。

## 构建

运行 `tools/build-uninterrupted-use.ps1`，tModLoader 内置编译器报告 0 错误、0 警告。产物：`dist/UninterruptedUse.tmod`。模组属性 `side = Client`；没有前置模组或客户端配置要求。

## 自动测试

运行 `tools/test-uninterrupted-use.ps1`，共 118 项通过。完整检查结果见 `uninterrupted-use-checks.txt`。

- 修改前加入鼠标抓起可使用物品的回归场景，1.0.0 在 `cursor-carried usable item cannot activate attack protection` 检查中失败；修改后通过。
- 鼠标携带物品或选中鼠标物品槽时不启用保护，快捷栏绘制不屏蔽这类点击。
- 执行原版 `ItemSlot.LeftClick`，确认物品可以放回槽位并重新抓起；执行原版 `dropItemCheck`，确认鼠标持有物品成功丢弃。
- 默认 F8 已注册；模拟按键实际通过 `ProcessTriggers` 切换功能。关闭后原版 UI 输入立即恢复，重新开启不会接管正在按住的点击。
- 开关状态通过 `SaveData` / `LoadData` 往返保留；没有该字段的旧角色默认开启。
- 显式右键和丢弃键优先于攻击保护，不修改原版操作的使用条件。

- 两处 IL 钩子和快捷栏钩子在真实 tModLoader 运行时加载成功。
- 600 帧 UI 悬停期间保持使用输入，真实的 `mouseInterface` 保持为 true。
- 右键 UI 逻辑、松键停止、UI 上开始按键、重新绑定的使用输入均符合预期。
- 菜单、全屏地图、背包、聊天、编辑文字、焦点丢失、死亡等情况取消保护，原来的按住操作不会自动重新获得保护。
- 原版 `Player.Update` 中的悬停延迟 IL 分支：保护期间不会新增等待松键状态；既有等待状态不会被清除；未保护时仍保持原版行为。
- 快捷栏绘制期间抑制已保护的长按选择；绘制结束或抛出异常后恢复鼠标状态；正常点击仍传给原版。
- 使用原版 `ItemCheck` 和 `ItemCheck_StartActualUse`：单次使用武器每次按键只发动一次；可自动连用武器在跨 UI 时重复发动；引导物品经过 120 帧 UI 悬停仍维持引导，松键后结束。
- 卸载后恢复原版 UI 输入阻断，重新加载后重新生效。

隔离进程从生产源代码生成测试副本，仅去掉客户端 `Autoload(Side = ModSide.Client)` 属性，其他生产方法保持一致。测试不打开游戏窗口，不加载角色或世界，不向正式 Mods 目录安装文件。

## 验证范围

未进行图形客户端实玩、多人联机或与其他输入/UI 模组的组合测试。图形客户端中仍需确认增益栏、快捷栏与小地图的实际悬停手感，以及用户原先遇到的交互情境。模组没有增添物品、配方或网络数据包；仅新增一个开关偏好存档字段。

安装说明和源码说明位于 `UninterruptedUse/README.md`。
