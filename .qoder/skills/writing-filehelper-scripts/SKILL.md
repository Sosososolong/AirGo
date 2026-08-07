---
name: writing-filehelper-scripts
description: 为 Sylas.RemoteTasks 的 FileHelper 命令执行器编写和校验文件操作脚本(批量改文件的 Markdown DSL, 含 TargetFilePattern/OperationType/LinePattern/Value 四个字段与 Append/Prepend/Replace/Override/Create 五种操作)。当用户需要生成或修改 FileHelper 脚本、Anything 命令模板、代码生成模板, 或提出"给所有匹配的文件插入一段内容""批量替换配置""脚本只改了最后一处""正则匹配不到行"这类需求与问题时使用。
---

# 编写 FileHelper 文件操作脚本

FileHelper 用一段 Markdown 风格的 DSL 描述"在工作目录里找到哪些文件、用正则定位到哪里、写入什么内容"。
执行器：`Sylas.RemoteTasks.Utils/CommandExecutor/FileHelper.cs`。

## 脚本骨架

```
## 操作名称(工作目录绝对路径)
<全局变量与函数调用行, 可选>

### 步骤标题
TargetFilePattern: 匹配目标文件完整路径的正则
OperationType: Append
LinePattern: 定位行的正则
Value: 要写入的内容
```

硬性结构要求：

- 标题行以 `##` 开头，**工作目录必须写在圆括号里**，缺了括号会抛"缺少工作目录配置"。
- **必须至少有一个 `###` 节点**，否则标题解析为空、脚本直接失败。
- 一个 `###` 节点 = 一组"目标文件 + 操作"，节点之间用空行分隔。
- 四个字段名大小写不敏感（`value:` 也可以），顺序任意。
- 需要 Razor 模板能力时，在整个脚本**最前面**单独加一行 `ENGINE: Razor`。

## 选择 OperationType

| 目标 | OperationType | LinePattern 的作用 |
|---|---|---|
| 在某一行后面插入内容 | `Append` | 定位行；**只在最后一个匹配行插入一次** |
| 在某一行前面插入内容 | `Prepend` | 定位行；**只在最后一个匹配行插入一次** |
| 追加到文件末尾 | `Append` | **留空** |
| 替换内容 / **在每个匹配处都插入** | `Replace` | 全文正则，替换掉**所有**匹配 |
| 整个文件内容替换成 Value | `Override` | 忽略 |
| 新建文件并写入 Value | `Create` | 忽略 |

### 最关键的一条：Append/Prepend 是"多处匹配、单点写入"

实现是 `lines.LastOrDefault(匹配)` 取锚点 + 一次 `Insert`。
所以哪怕 20 行都匹配 `LinePattern`，也**只有最后一行**会被写入。

需要"每个匹配处都插入"时，必须改用 `Replace` + 捕获组回填：

```
OperationType: Replace
LinePattern: (\s{6}-\s\d+:80)(?=[ \t]*\r?\n)
Value: $1{br}{sp:4}depends_on:{br}{sp:6}- redis
```

即把"插入"表达为"把锚点替换成 锚点 + 新内容"。这时 `Value` 里的 `$1` 会回填锚点原文。

## 两种匹配范围，正则写法完全不同

| | Append / Prepend | Replace |
|---|---|---|
| 匹配对象 | 逐行（`Split('\n')` 后每行已 `TrimEnd`） | 整个文件内容 |
| `^` `$` | 就是行首行尾，可放心用 | **没有 `Multiline` 选项**，只代表整个内容的首尾，用了必然匹配不到 |
| 表达行边界 | 直接 `^...$` | 用 `(?=[ \t]*\r?\n)`、`\r?\n` 这类显式换行断言 |
| 跨行匹配 | 不可能 | 可以 |

所以用户常写的 `(\s{6}-\s\d+:80)+` 在 Append 下会退化成"任何含该片段的行"：`(...)+` 的重复量词与跨行意图都失效。
另外两种模式的 `LinePattern` 都**区分大小写**（只有 `TargetFilePattern` 忽略大小写），且 `IsMatch` 不锚定，`\d+:80` 会命中 `- 5014:8080`。

## Value 的书写规则

| 规则 | 说明 |
|---|---|
| **首行缩进会被 Trim 掉** | 要缩进必须用 `{sp:N}`，例如 `Value: {sp:4}<PackageReference ... />` |
| 第二行起保留原缩进 | 但仍建议统一用 `{sp:N}`，避免脚本编辑器吞空格 |
| `Value:` 后**不要**直接换行 | 那会让首行是空串，在写入结果里凭空多出一个空行 |
| 换行用 `{br}` / `{br:N}` | 单行写法里表达多行内容 |
| 空格用 `{sp}` / `{sp:N}` | `{sp}` 是 1 个空格 |

`{br}` 展开成 `\n`（不是 `Environment.NewLine`）。`Replace` 分支直出这个结果，在 CRLF 文件里会产生混合换行；`Append`/`Prepend` 分支最后按 `Environment.NewLine` 重新拼接，不受影响。

## 一个节点里放多个操作

`Value`、`OperationType`、`LinePattern` 都用 `|||` 分隔，按下标一一配对，**三者段数必须相同**：

```
### 客户端添加MudBlazor服务
TargetFilePattern: (\.web.+program.cs)|(mauiprogram.cs)
Value: using MudBlazor.Services;|||builder.Services.AddMudServices();
OperationType: Append|||Prepend
LinePattern: using |||builder.Build()
```

同一文件的多个步骤按顺序在**前一步的结果**上继续计算。

## 幂等判定

执行前会判断"是否已经做过"，命中就跳过该步骤：

| OperationType | 跳过条件 |
|---|---|
| `Append` / `Prepend` | 文件内容已 `Contains(Value)` |
| `Override` / `Create` | 文件内容与 `Value` 完全相等 |
| `Replace` | 文件内容已 `Contains(Value)` — **`Value` 含 `$1` 时永远不成立** |

所以用 `Replace` 做插入时，**幂等只能靠正则自己保证**，要加负向先行断言排除已处理过的位置：

```
LinePattern: (\s{6}-\s\d+:80)(?=[ \t]*\r?\n)(?![ \t]*\r?\n([ \t]*\r?\n)?[ \t]*depends_on:)
```

否则重复执行会重复插入。写完后务必自问一句：这条正则能认出"已经处理过的位置"吗？

## 生成脚本的工作流

1. **确认工作目录**：绝对路径，写进 `##` 标题的括号里。目录下的文件会被递归枚举（路径统一为 `/`，自动排除 `/obj/`、`/bin/`）。
2. **写 TargetFilePattern**：它是对**文件完整路径**做 `Regex.IsMatch`（忽略大小写），所有匹配的文件都会被处理。用 `\.` 转义扩展名点号、用 `$` 收尾避免误伤（如 `Shared\.csproj$`）。匹配不到任何文件会抛异常。
3. **按上面的表选 OperationType**：需要多处写入就直接用 `Replace`，不要试图让 `Append` 处理多处。
4. **写 LinePattern**：Append/Prepend 用 `^...$` 锚定整行避免子串误匹配（行已 `TrimEnd`，行尾空白不用管）；Replace 不能用 `^`/`$`，改用 `(?=[ \t]*\r?\n)` 表达行尾。注意大小写敏感。
5. **写 Value**：首行加 `{sp:N}` 补缩进；多行内容要么真换行、要么用 `{br}`。
6. **补幂等**：`Replace` 且 `Value` 含 `$1` 时，必须加负向断言。
7. **自检**（见下）后，建议先用模板编辑器的"预览正则匹配"/"预览改动"验证，预览只读不落盘，且与真实执行共用同一套计算逻辑。

### 交付前自检清单

- [ ] `##` 标题有括号且里面是工作目录
- [ ] 至少一个 `###` 节点，节点间有空行
- [ ] `TargetFilePattern` 对路径而非纯文件名做匹配，正则里的 `.` 已转义
- [ ] 需要多处写入的场景没有误用 `Append`
- [ ] `Value` 首行缩进用 `{sp:N}` 表达
- [ ] `Value:` 后面没有直接换行（除非确实要那个空行）
- [ ] 一个节点内 `Value`/`OperationType`/`LinePattern` 的 `|||` 段数一致
- [ ] `Replace` + `$1` 的场景加了负向断言保证幂等
- [ ] 重复执行两遍的结果与执行一遍相同

## 高频错误速查

| 现象 | 原因 |
|---|---|
| 只有最后一处被改 | `Append`/`Prepend` 的固有行为，改用 `Replace` + `$1` |
| 多匹配了 `:8080` 这类更长的值 | 正则不锚定；Append/Prepend 加 `$`，Replace 加 `(?=[ \t]*\r?\n)` |
| Replace 用了 `^`/`$` 后匹配不到 | `Regex.Replace` 没有 `Multiline`，`^`/`$` 只匹配整个内容首尾 |
| 重复执行内容翻倍 | `Replace` 的 `Value` 含 `$1`，幂等失效，需要负向断言 |
| 写入内容顶格、缩进丢了 | `Value` 首行被 Trim，改用 `{sp:N}` |
| 结果里凭空多一个空行 | `Value:` 后直接换行了 |
| "没有匹配到对应的行" | `LinePattern` 不匹配，或匹配到的行是空白行（空白锚点被当作未匹配） |
| "没有找到文件:xxx" | `TargetFilePattern` 与完整路径不匹配 |
| 混合换行(CRLF 文件里出现 LF) | `Replace` + `{br}` 的已知行为 |

## 更多资料

- 完整字段语义、变量与函数、执行顺序、已知限制：[reference.md](reference.md)
- 可直接套用的完整脚本示例：[examples.md](examples.md)
