# 流场寻路骨架

`FlowFieldData` 保存单个终点的地图输入和两份结果

- `IntegrationCostList` 存每格到终点的累计代价
- `DirectionList` 存每格下一步应走的方向
- 采用现有网格坐标 左上角为原点 右和下分别为正方向

`FlowFieldCore` 预留两阶段入口

1. `BuildIntegrationField` 从终点反向计算累计代价
2. `BuildDirectionField` 为可达格选择累计代价更低的邻格

当前两个入口会抛出 `NotImplementedException` 表明算法尚未实现
尚未接入 `GridSearchController` 或右侧算法选择界面
后续可将 `DirectionList` 逐格交给 `SearchGridView.SetCellArrow` 展示

同一终点只需生成一张流场 多个起点可以共用结果
障碍或终点变化后需要重新计算流场
