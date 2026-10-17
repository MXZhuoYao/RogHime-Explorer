# GitHub 仓库结构

## 上传什么

```
rog-explorer-theme/                ← 仓库根
│
├─ README.md                   ★ 门面（居中封面 + 徽章 + 动图 + 特性表）
├─ .gitignore                    排除 dist/ release/ 运行时文件
├─ VERSION.txt                   2.2.0
│
├─ docs/
│   ├─ CHANGELOG.md              版本历史
│   ├─ 使用手册.md                完整中文手册（用户向）
│   ├─ github/                   ★ README 用素材
│   │   ├─ preview-roghime.png      RO姬 边框预览
│   │   ├─ preview-fubuki.png       吹雪 边框预览
│   │   ├─ preview-carbon.png       碳纤维边框预览
│   │   ├─ preview-settings.png     设置界面
│   │   ├─ demo-theme-switch.gif    主题切换动图
│   │   ├─ demo-glitch.gif          故障特效动图
│   │   ├─ RELEASE-v2.2.0.md        发布说明（贴到 Release 里）
│   │   └─ STRUCTURE.md             本文件
│   └─ gen_art.py                 刃片花纹生成脚本（开发用）
│
├─ src/                         ★ 源码
│   ├─ Engine/                    引擎（8 文件 C#）
│   │   ├─ Program.cs               主逻辑 · 托盘 · 调度
│   │   ├─ Art.cs                   边框绘制 · 刃片 · glitch
│   │   ├─ Brand.cs                 铭牌 · 字体转矢量
│   │   ├─ Svg.cs                   迷你 SVG 解析器
│   │   ├─ Overlay.cs               分层覆盖层窗口
│   │   ├─ Config.cs                配置 + 主题色板
│   │   ├─ AutoStart.cs             开机自启四方式
│   │   ├─ Native.cs                Win32 封装
│   │   └─ app.manifest             DPI 感知
│   └─ Setup/                     安装向导
│       ├─ Setup.cs                 5 页分页向导
│       └─ app.manifest             requireAdministrator
│
├─ assets/                       图标 · 字体 · 壁纸 · 材质
├─ ExplorerBg/                   第三方外壳扩展（MIT + 许可证）
├─ tools/                        内部脚本
│
├─ build.ps1                     编译引擎
├─ make-release.ps1              打包
├─ install.ps1 / uninstall.ps1   安装 / 卸载
├─ 安装.bat 卸载.bat 设置.bat      用户脚本
└─ 构建.bat 发布.bat              开发脚本
```

---

## 不入库的内容（已在 .gitignore）

```
dist/                  构建产物
release/               发布产物
src/Setup/payload.zip  打包输入（每次生成）
state/  *.log  report.txt   运行时文件
docs/素材原图/          原始素材（体积大）
```

---

## GitHub Release 页面怎么填

**Tag**：`v2.2.0`

**Title**：`ROG Explorer Theme v2.2.0`

**Description**：直接粘贴 `docs/github/RELEASE-v2.2.0.md`

**附件**（拖进去）：
```
RogHime-Setup-v2.2.0.exe              6478 KB   ← 主推
RogHime-Explorer-v2.2.0-win64.zip     6403 KB
SHA256SUMS.txt
```

**勾选**：`Set as the latest release`

---

## 仓库设置建议

| 项 | 值 |
|---|---|
| Description | `Windows 11 资源管理器美化 · 零注入外置边框 · 双主题 · 一键还原` |
| Website | 留空 |
| Topics | `windows-11` `explorer` `theme` `rog` `customization` `csharp` `no-injection` |
| License | 按你的意愿选择（第三方 MIT 组件已单独保留许可证）|

---

## README 写法说明（参考了 GitHub 通行规范）

| 手法 | 用在哪 |
|---|---|
| `<div align="center">` 居中封面 | 顶部：标题 + 定位语 + 徽章 + 动图 |
| Shields.io 徽章 | 平台 / .NET 版本 / 零注入 / 体积 / 主题 |
| 一句话定位（加粗） | 「把 Windows 11 的资源管理器，变成 ROG 的样子。」|
| 引用块讲理念 | 「能不能在旁边加一层，而不是钻进去。」|
| 特性表格 | 7 项核心能力，可扫读 |
| `<details>` 折叠 | 更多细节 / 技术实现（保持首屏干净）|
| ASCII 树 | 项目结构 |
| `> [!NOTE]` `[!WARNING]` | GitHub 彩色提示块（比普通引用醒目）|
| 分割线 `---` | 区块分隔 |
| 致谢 + 免责 | 底部 |
| 不用 © 符号 | 现代简洁 |
