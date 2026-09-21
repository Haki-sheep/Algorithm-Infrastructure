# TMP 字体工作台

入口：`Tools > Image2UGUI > Font Manager`。窗口为 Odin 左中右：左侧 `OdinMenuTree` 选 TMP 与功能页，中间固定预览，右侧只画当前页。工作中心是一个实际的 `TMP_FontAsset`。`FontBakeProfile` 只保存文案来源和预览样式。

## 左侧

- `TMP 字体 / 名称`：资源关系（源 TTF、Static/Dynamic、字符与字形、Fallback、图集内存、材质）
- 预览调参、缺失补齐、性能优化、应用到 UI
- 从源字体新建
- 配置：切换或新建 `FontBakeProfile`

## 中间预览

真实 TMP 渲染当前字体和 fallback 链。会克隆 TMP、图集、材质及全局 fallback，动态字体试显示新字符不会污染工程图集。右侧调参后自动刷新。有优化候选时上下对照。预览字号不是烘焙 sampling size，Padding 也不是排版字距。

## 缺失补齐

先收集手动文案、TextAsset、Scene 和 Prefab 中的文本，再按码点去重并保留每个来源。诊断区分：

- `TMP 已有`：当前 TMP 字符表已覆盖
- `Fallback 已覆盖`：主 TMP 没有，但 fallback 链可提供
- `主 TTF 有字 · 尚未写入 TMP`：源字体有轮廓，需要写入主 TMP 图集
- `主 TTF 与 Fallback 都缺字`：没有可用字形，列表会定位到具体文案文件、Prefab、场景和文本组件

缺字操作保留已经收集的字符，并引导补充源字库或配置正确的 fallback。场景来源记录组件 GlobalObjectId；定位时会临时 Additive 打开未加载场景、选中具体对象，不保存场景修改。

`创建主 TMP` / `补齐当前 TMP` 会在所有字形检查通过后生成或更新资源。更新工具生成的资源会保留 TMP、图集和材质的引用；图集数量发生变化时拒绝危险的原位更新并另存新资源。失败不会留下不完整资源。

## 性能优化

在同一份字符集上调整 sampling size、Padding、图集尺寸、SDF 渲染模式、Static / Dynamic 和多图集开关，生成临时候选。中间预览同时展示当前 TMP 与候选效果。

点击「备份并写回当前 TMP」才会写回。若图集数量无法保持，写回按钮不可用，可另存为新 TMP 后再应用到 UI。纹理内存按整张纹理估算：1024² Alpha8 约 1 MiB、2048² 约 4 MiB、4096² 约 16 MiB，未计入 CPU 副本、材质和源字体。

## 应用到 UI

样式材质可单独保存；应用到选中 UI 前会先列出范围，检查实际文本是否由主 TMP 或 fallback 覆盖，并支持 Undo。应用不会改文本内容、RectTransform 尺寸，也不会自动保存场景。

验收入口：`Image2UGUI.FontManagement.FontManagerVerification.Run`（Unity `-executeMethod`）。验证应在隔离工程中设置 `FONT_MANAGER_VALIDATION=1`，结果写入 `Logs/FontManagerValidation/results.txt`。
