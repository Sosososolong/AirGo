# FileHelper 脚本示例

以下脚本均取自项目内已通过的单元测试或已在真实文件上验证过的用法。

## 1. 在每个匹配位置都插入内容（Replace + `$1`）

需求：docker-compose 里所有映射到容器 80 端口的服务，都补上 `depends_on`。

```
## 修改配置中心(D:/temp/config/)

### 给所有映射到容器80端口的服务补充依赖
TargetFilePattern: docker-compose\.yaml$
OperationType: Replace
LinePattern: (\s{6}-\s\d+:80)(?=[ \t]*\r?\n)(?![ \t]*\r?\n([ \t]*\r?\n)?[ \t]*depends_on:)
Value: $1{br}{sp:4}depends_on:{br}{sp:6}- mysql{br}{sp:6}- redis
```

三段正则各自的职责：

| 片段 | 作用 |
|---|---|
| `(\s{6}-\s\d+:80)` | 捕获端口行原文，供 `Value` 里的 `$1` 回填 |
| `(?=[ \t]*\r?\n)` | 要求 `:80` 就是行尾，排除 `- 5014:8080` 这类误匹配 |
| `(?![ \t]*\r?\n([ \t]*\r?\n)?[ \t]*depends_on:)` | 跳过后面已有 `depends_on` 的服务，保证幂等（容忍中间夹一个空行） |

不能写成 `Append`：那样只会在最后一个匹配行后插入一次。
也不能省掉负向断言：`Replace` 的幂等判断是 `content.Contains(Value)`，而 `Value` 含 `$1` 时永远不成立，重复执行会重复插入。

## 2. 定位到某一行后面插入（Append）

需求：给 csproj 添加包引用、给 `_Imports.razor` 追加全局 using。

```
## MudBlazor - 1.添加包(D:/code/MySln/)

### 添加MudBlazor包
TargetFilePattern: Shared\.csproj$
Value: {sp:4}<PackageReference Include="MudBlazor" Version="7.15.0" />
OperationType: Append
LinePattern: PackageReference\s+Include

### 给Blazor组件添加MudBlazor的全局引用
TargetFilePattern: _Imports\.razor$
Value: @using MudBlazor
OperationType: Append
LinePattern: 
```

要点：

- 第一个节点靠 `PackageReference\s+Include` 定位到**最后一个**包引用行，插在它后面，正是想要的效果。
- 第二个节点 `LinePattern` 留空 = 追加到文件末尾。
- `Value` 首行的 4 个空格缩进只能用 `{sp:4}` 表达。

## 3. 一个节点里做多个操作（`|||`）

需求：同一批 Program.cs 既要在 using 区加引用，又要在 `builder.Build()` 之前注册服务。

```
### 客户端添加MudBlazor服务
TargetFilePattern: (\.web.+program\.cs)|(mauiprogram\.cs)
Value: using MudBlazor.Services;|||builder.Services.AddMudServices();
OperationType: Append|||Prepend
LinePattern: using |||builder.Build()
```

三个字段的 `|||` 段数必须一致，按下标配对成两个步骤：
`Append using MudBlazor.Services;` 到最后一个 `using ` 行之后，
`Prepend builder.Services.AddMudServices();` 到 `builder.Build()` 行之前。

## 4. 整个文件重写（Override）

需求：把布局文件初始化成固定内容。多行 `Value` 直接换行书写即可（只有首行受 Trim 影响）。

```
### MainLayout初始化布局
TargetFilePattern: MainLayout\.razor$
OperationType: Override
LinePattern:
Value: @inherits LayoutComponentBase

<MudThemeProvider @ref="_mudThemeProvider" @bind-IsDarkMode="_isDarkMode" />
<MudPopoverProvider />
<MudDialogProvider />
```

## 5. 新建文件（Create）

```
### 新增服务接口文件
TargetFilePattern: Services/IOrderService.cs
OperationType: Create
LinePattern:
Value: namespace {NAMESPACE};

public interface IOrderService
{
}
```

要点：

- `TargetFilePattern` 含 `/` 时，`/` 之前的 `Services` 会作为正则去已枚举文件里找目录，务必确保该目录下已有文件能命中。
- 不含 `/` 时直接相对工作目录创建。
- 一个节点只能有一个 `Create` 步骤。
- `{NAMESPACE}` 会按目标文件所属 csproj 与子目录实时算出。

## 6. 简单替换（Replace，不带捕获组）

```
### index.html/App.razor将lang=en替换为lang=zh-cn
TargetFilePattern: (index\.html)|(App\.razor)
OperationType: Replace
LinePattern: lang="en"
Value: lang="zh-cn"
```

`Value` 不含 `$1`，所以 `content.Contains("lang=\"zh-cn\"")` 能生效，天然幂等。

## 7. 函数变量与全局变量

```
## 生成服务代码(D:/code/MySln/)
GetFileProjDirAndNamespace(D:/code/MySln/|||/Service/) => ServiceRootDir, ServiceRootNS
Pluralize(Order) => EntityPluralizeName

### 创建服务文件
TargetFilePattern: Service/{EntityPluralizeName}Service.cs
OperationType: Create
LinePattern:
Value: namespace {ServiceRootNS}.Services;
```

要点：

- 函数的多个参数用 `|||` 分隔。
- `=> A, B` 形式把返回数组按序存入变量，只适合单独占一行写在标题下方。
- 需要在 `Value` 内部就地取值时用 `=> [0]` 形式。
