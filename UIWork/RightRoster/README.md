# 右侧角色选择 UI

主资产 `Assets/TestImgae/RightRoster/RightRoster.prefab`

直接双击 `Assets/TestImgae/RightRoster/RightRosterPreview.unity` 查看静态画面 或将主 Prefab 拖入空场景

## 交付内容

- 96 个原生节点 使用 Canvas Image RectTransform 和 TextMeshProUGUI
- 58 张独立透明 PNG 切片和 SpriteAtlas
- 13 张可见角色头像 三列交错布局及原图首尾裁切
- 安比选中框 品质 属性 等级 三项页签 筛选 收藏 SELECT UID 与信号
- 主尺寸 1540×928 另验证 1280×928 和 1920×928 右侧锚定 高度等比缩放
- 左侧模型依照用户要求省略 预览左侧使用暗色底

本次交付为静态视觉资产 未添加筛选 点击 切换角色或其它业务脚本

## 技能执行与证据

直接调用 `C:/Users/fmz/Desktop/30-integrated/30-integrated/payload/skills` 中的资源摄取与 UI 制作技能 无需安装整包 工程已有 Unity Pipeline

资源摄取与验证见 `reference-package` 资源准备和字形表见 `resources`

六阶段按 begin → 产出 → 实际观察 → finish 顺序完成 分别见 `01-composition` 到 `06-micro-detail` 保留 started candidates submissions 与截图

六步检查 `six-stage-check.json` 与原生小样制作检查 `production-check.json` 已通过 技能工具的 368 项测试通过 详见 `tool-validation`

主 Prefab 采用空壳加分区原生编辑 每批保存重开 然后导出真实层级并先展示层级后展示截图

最终对照补齐右下 UID 信号和底边 保留 `native/footer-edit-plan.json` 与前后资产和截图 旧的 92 个节点身份和导出属性一致

修复 TMP 内部下划线缺失警告 保留 `native/font-patch-plan.json` 和 `native/font-patch-regression.json` 修复前后三种宽度逐像素一致 最后重开无新增警告

## 最终检查状态

`delivery-check.json` 结果为 **未通过 ITERATION_BUDGET_EXCEEDED**

检查已进入最后的预算验收 即此前的来源 真实层级 原生组件 文字绑定与三视口声明关系检查未报错 最后由于墙钟耗时约 130 分钟 超出原定 90 分钟而失败 其中一次工具等待约 59 分钟 没有改旧预算或将失败改写为通过

初次交付索引超过技能字符串列表的 32 项上限 原失败保存在 `delivery-submissions` 修正后的交付索引列出全部头像导航与不同等级品质属性代表 完整 74 个视觉节点均独立核验 三份捕获 JSON 保留完整节点 测量范围见 `native/layout-audit.json`

真实 Unity 验证见 `native/engineering-audit.json`

- 预览场景保存重开成功 且关联主 Prefab
- 0 丢失脚本 0 丢失 Sprite 0 丢失字体
- 等级与中文页签原文逐项读回
- 字体修复后日志 cursor 423 之后 0 新增警告或错误 历史日志保留
- 原 VisualizationDemo 场景仍为未修改状态

检查器验证文件关系与声明 不代表逐像素一致或用户验收 黑条网点经过重建 UID 使用项目现有字体 原图 JPEG 模糊保留

## 维护限制

ReferenceBitmap 保留原图 11 个可见字形 `等级604基础技能装备` 加一个 TMP 内部下划线字形 适用于当前静态文案 需要其它中文或数值时应更换或扩充字体

UID 使用项目现有 LiberationSans SDF 字体

`NativeAuthor.cs` 位于 Assets 外 是通过 Pipeline 执行的编辑器制作配方 不是运行时依赖 已完成的导入 新建和截图入口拒绝覆盖 不要对成品直接重复执行

## 画面对照

最终主尺寸 `native/final-primary-v3.png`

原图与 Unity 右侧并排 `native/comparison.png`

完整实际层级 `native/hierarchy-v3.json`

来源快照为 Unity 导出的只读字节副本 `native/source-snapshots` 不可作为手写 Unity 资产入口
