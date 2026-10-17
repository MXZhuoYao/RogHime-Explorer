## ROG Explorer Theme v2.2.0

把 Windows 11 资源管理器变成 ROG 的样子 —— 零注入、外置覆盖层、双主题、一键还原。

### 这个版本带来了

**双主题系统**
RO姬（红黑）/ 吹雪（白蓝），可跟随系统浅色/深色自动切换。每个主题自带完整色板、默认壁纸与遮罩强度。

**三种边框风格**
刀锋刃片 · 碳纤维材质 · 纯色。碳纤维会自动按主题切换红 / 蓝版本。

**背景系统**
文件列表 + 主文件夹双图层，4 张内置壁纸 + 自定义图片。遮罩浓度按主题自动选色。

**开箱可调**
边框厚度、45° 切角、圆角、上下左右留白、铭牌、glitch 故障特效——全部在设置界面实时预览。

---

### 下载

| 文件 | 说明 |
|---|---|
| `RogHime-Setup-v2.2.0.exe` | 图形安装向导，默认管理员运行，可自选安装位置 |
| `RogHime-Explorer-v2.2.0-win64.zip` | 便携版，解压后双击 `安装.bat` |

**校验**
```
SHA256SUMS.txt
```

---

### 环境

- Windows 11
- .NET Framework 4.x（系统自带，无需额外安装）
- 6.4 MB 安装包 / 安装后约 7.6 MB

---

### 卸载

设置界面 → 关于 → 卸载按钮，或从 Windows「设置 → 应用」卸载。

会还原系统强调色、DWM 配色、开机自启项、外壳扩展注册，并删除安装目录。

---

### 已知行为

背景图由外壳扩展加载后会缓存在内存。切换背景或主题后，已打开的窗口不会立即刷新 —— **新开窗口**或重启 Explorer 即可。

---

### 致谢

- [ExplorerBgTool](https://github.com/Maplespe/ExplorerBgTool) — 文件列表背景扩展（MIT）
- [Windows 11 File Explorer Styler](https://github.com/m417z/windhawk-mods) — 主文件夹背景支持
- [simple-icons](https://simpleicons.org/) — ROG 之眼图标（CC0）

个人爱好者作品，与华硕 / ROG 官方无任何关联。
