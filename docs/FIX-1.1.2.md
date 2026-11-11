# 1.1.2 运行记录排版

原因：全局 TextBox 样式设置了 VerticalContentAlignment=Center，日志框继承该样式，少量记录显示在大面积空白区域的中间。

在 MainWindow.xaml 的 LogBox 局部设置顶部对齐、左对齐，字体 12 → 16，行高 26；保留原有追加顺序、换行、清空和滚动行为。全局输入框样式无需修改。

本次功能变更只有 MainWindow.xaml；其余修改用于版本标识、打包及发布说明。验证通过程序自身的 WPF 窗口渲染查看效果，不启动屏幕捕获。
