# FileHelper 脚本完整参考

对应实现：`Sylas.RemoteTasks.Utils/CommandExecutor/FileHelper.cs`。

## 解析与执行顺序

1. 若脚本含 `ENGINE: Razor`，先整体走一遍 Razor 模板渲染（Model 来自执行上下文的环境变量，可用 `@Model.XXX`）。
2. 取第一个 `##` 到第一个 `###` 之间的内容：首行解析出操作名与工作目录，**其余行全部作为全局变量/函数调用行**依次求值。
3. 按 `###` 切分节点，逐节点解析四个字段。
4. 解析变量：先函数变量，再 `#IF` 条件，最后全局变量。
5. 枚举工作目录文件 → 逐节点解析目标文件 → 逐文件解析 `NAMESPACE` → 逐步骤计算并写入。

## 标题行

```
## 操作名称(D:/temp/config/)
```

- 正则 `(?<name>\w+)\s*\((?<workingDir>.*)\)`。括号内即工作目录，缺括号 → `缺少工作目录配置`。
- `name` 只取括号前紧邻的一段 `\w+`，仅用于日志和异常文案，不影响功能。所以标题里可以写中文、连字符等（如 `## MudBlazor - 1.添加包(D:/x)`）。
- 工作目录**不存在时会被自动创建**（预览也会创建，属已知副作用）。
- 目录下文件递归枚举，路径分隔符统一为 `/`，自动排除包含 `/obj/`、`/bin/` 的路径。

## 节点字段

字段名取自 `OperationNode` 与 `NodeStep` 的属性名，匹配规则是 `行.StartsWith("字段名:", 忽略大小写)`：

| 字段 | 说明 |
|---|---|
| `TargetFilePattern` | 目标文件。非 Create 时是匹配**完整路径**的正则；Create 时当作路径用 |
| `OperationType` | `Append` / `Prepend` / `Replace` / `Override` / `Create`，其它值抛`无效的操作类型` |
| `LinePattern` | 定位正则，语义随 `OperationType` 变化 |
| `Value` | 写入内容，支持多行 |

> **保留字陷阱**：`NodeTitle:`、`Steps:` 也在字段名清单里。`Value` 的续行如果以这六个字段名之一加冒号开头（例如 yaml 片段里的 `Value: xxx`），会被当成新的配置项，导致内容被截断。遇到这种内容改用单行 `Value:` + `{br}` 写法。

## 各 OperationType 的精确行为

### Append / Prepend

- 按 `content.Split('\n')` 拆行，每行 `TrimEnd()` 后做 `Regex.IsMatch(行, LinePattern)`（**区分大小写**，不带任何 `RegexOptions`）。
- 因为输入是单行，`^` 和 `$` 等价于行首行尾，可放心使用。
- 取**最后一个**匹配行作为锚点，`Append` 插在其后一行，`Prepend` 插在其前一行。**只插一次**。
- 锚点行若是空白字符串，视为"没有匹配到对应的行"并抛异常。
- `LinePattern` 留空 = 追加到文件末尾：先剥掉末尾空行，补一个空行作分隔，写入 `Value`，再补回原有的末尾空行数量。
- 结果用 `Environment.NewLine` 重新拼接，因此不会引入混合换行。

### Replace

- `Regex.Replace(content, LinePattern, Value)`，对**整个文件内容**匹配，替换**所有**匹配处。
- **不带 `RegexOptions.Multiline`**：`^` 只匹配内容开头、`$` 只匹配内容结尾，用它们表达行边界必然失败。要表达行尾请用 `(?=[ \t]*\r?\n)`，表达行首请用 `(?<=\n)`。同样**区分大小写**。
- 支持 `$1`、`${name}` 等替换语法，是实现"多处插入"的唯一手段。
- 不会重新拼接换行，`Value` 里的 `{br}`（= `\n`）会原样进入 CRLF 文件。

### Override

- 忽略 `LinePattern`，把文件内容整体替换为 `Value`。

### Create

- 忽略 `LinePattern`，创建文件并写入 `Value`。
- **一个节点里最多只能有一个 Create 步骤**，否则抛`只能包含一个步骤`。
- `TargetFilePattern` 含 `/` 时：`/` 之前的部分作为正则在已枚举文件里找第一个匹配项，取其所在目录，再拼上 `/` 之后的文件名。目录没找到只会记 Critical 日志，路径会退化成不可用值，务必保证目录正则能命中。
- 不含 `/` 时：`Path.Combine(工作目录, TargetFilePattern)`。
- 预览模式不真的创建文件，内容视为空。

## 多步骤

`Value` / `OperationType` / `LinePattern` 各自按 `|||` 切分后按下标配对。段数以 `Value` 为准，另两个字段段数不足会越界报错。

## 空白与换行占位符

| 占位符 | 展开 | 生效时机 |
|---|---|---|
| `{sp}` / `{sp:N}` | N 个空格（默认 1） | 解析节点时（`Value` 的每一行） |
| `{br}` / `{br:N}` | N 个 `\n`（默认 1） | 计算改动时 |
| `&nbsp;` | 1 个空格 | 旧语法，兼容保留 |
| `{TAB}` | 4 个空格 | 内置全局变量 |

`Value` 首行会被 `Trim()`，所以行首缩进只能靠 `{sp:N}`；第二行起保留原缩进。
`Value:` 后直接换行会让首行成为空串并输出一个空行。整个 `Value` 末尾的换行会被 `TrimEnd`。

## 变量

### 全局变量

- 语法 `{VarName}` 或 `{{VarName}}`；`${...}` 形式**不属于** FileHelper（那是外层 TmplHelper 模板，在脚本进入 FileHelper 之前就已展开）。
- 变量名不能含空白和 `;`。
- 取数组元素：`{VarName[0]}`。
- 支持链式替换：`{VarName}.Replace("a","b");`（注意结尾分号）。
- 未定义的变量原样保留，不会报错。

### 内置变量

| 变量 | 值 |
|---|---|
| `{TAB}` | 4 个空格 |
| `{NAMESPACE}` | 当前目标文件所属项目的命名空间 = `.csproj` 文件名 + 文件相对该 csproj 的子目录，按 `.` 连接；每个目标文件实时重算 |

### 函数变量

写在 `##` 标题行下方（第一个 `###` 之前），也可内嵌在字段值中。

```
FuncName(参数1|||参数2) => VarA, VarB
FuncName(参数1) => [0]
```

- 函数名必须**大写字母开头**，且是 `FileHelper` 的 `public` 方法。
- **多个参数用 `|||` 分隔**（不是逗号，避免与内容里的逗号冲突）。
- `=> VarA, VarB` 形式：把返回数组按序存入这些变量名（已存在的变量不会被覆盖），并且**整段文本会被替换为函数返回值**，因此这种形式只适合单独占一行作为全局变量声明。
- `=> [0]` / `=> 0` 形式：就地替换为返回数组的第 n 个元素，适合内嵌到 `Value` 里。
- 旧格式 `FuncName(...)->Key->["A","B"]` 仍兼容，`Key` 被忽略。
- 返回 `string[]` 时内部用 `|||` 连接；`Async` 结尾的方法要求返回 `Task<string>` 或 `Task<string[]>`。

常用可调用函数（均为 static，参数皆为 string）：

| 函数 | 返回 |
|---|---|
| `GetSolutionDirectory()` | 解决方案目录 |
| `GetDirectoriesUnderSolution()` | 解决方案下的子目录数组 |
| `GetDirectoryFileInfo(目录\|\|\|正则1,正则2)` | 每个正则在该目录下**第一个**匹配到的片段（正则用 `,` 或 `;` 分隔） |
| `GetFileProjDirAndNamespace(目录\|\|\|文件正则)` | `[项目目录, 项目根命名空间]` |
| `ToCamelCase(名称)` | 小驼峰 |
| `Pluralize(单词)` | 复数形式 |
| `BuildWhereIfStatement(属性代码)` | WhereIf 链式语句 |
| `GetDateTimePropAssignCode(实体属性代码)` | 时间字段赋值代码 |
| `UnformatJsonString(json)` | 压缩后的 json |

> 已知限制：函数一律以 `Invoke(null, ...)` 调用，因此**只有静态方法可用**。实例方法（如 `BuildEntityClassCodeAsync`）通过函数变量调用会失败。

### 条件输出

```
#IF:VarName.Contains(子串)需要的内容#ELSE:否则的内容#IFEND
#IF:!VarName.Contains(子串)不包含时的内容#IFEND
```

- 只作用于 `TargetFilePattern` 和 `Value`。
- `VarName` 必须是已定义的全局变量，否则抛 `KeyNotFoundException`。
- `#ELSE:` 可省略。

### Razor 引擎

脚本最前面单独一行 `ENGINE: Razor` 后，整个脚本先经 RazorEngine 渲染，可用 `@Model.变量名`（Model 来自执行上下文的环境变量）、`@foreach` 等。模板按"标题行 + 全局变量行"做缓存键。

## 幂等

| OperationType | 跳过条件 |
|---|---|
| `Append` / `Prepend` | `content.Contains(Value)` |
| `Override` / `Create` | `Value == content` |
| `Replace` | `Value` 非空白且 `content.Contains(Value)` |

`Replace` 的 `Value` 含 `$1` 等替换语法时，`Contains` 永远不成立，幂等必须由 `LinePattern` 的负向断言保证。

## 预览与真实执行的一致性

- 预览（dryRun）与真实执行共用 `ResolveOperation` / `ResolveOperationVariablesAsync` / `ResolveTargetFiles` / `ComputeModification`，只在"是否落盘""是否创建文件"上不同。
- 预览时同一文件的多个步骤基于上一步的内存结果继续计算，因此后续步骤能看到前面步骤的效果。
- "预览正则匹配"会标出真实执行时会被写入的锚点：`Append`/`Prepend` 只有最后一个匹配行标为锚点，`Replace` 的每个匹配都是锚点。存在多个内容完全相同的行时会给出提示（真实执行按 `IndexOf` 定位到第一个同内容行）。
