# AnythingService 核心功能

<cite>
**本文引用的文件**
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs)
- [AnythingInfo.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingInfo.cs)
- [AnythingSetting.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSetting.cs)
- [AnythingSettingDetails.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetails.cs)
- [AnythingSettingDetailsInDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetailsInDto.cs)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs)
- [OperationResult.cs](file://Sylas.RemoteTasks.Common/Dtos/OperationResult.cs)
- [RequestResult.cs](file://Sylas.RemoteTasks.Common/Dtos/RequestResult.cs)
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs)
- [CommandInfoInDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandInfoInDto.cs)
- [CommandInfoTaskDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandInfoTaskDto.cs)
- [CommandResolveDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandResolveDto.cs)
- [EntityBase.cs](file://Sylas.RemoteTasks.Database/EntityBase.cs)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs)
- [ExecutorAttribute.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ExecutorAttribute.cs)
</cite>

## 目录
1. [简介](#简介)
2. [项目结构](#项目结构)
3. [核心组件](#核心组件)
4. [架构总览](#架构总览)
5. [详细组件分析](#详细组件分析)
6. [依赖关系分析](#依赖关系分析)
7. [性能考量](#性能考量)
8. [故障排查指南](#故障排查指南)
9. [结论](#结论)
10. [附录](#附录)

## 简介
本文围绕 AnythingService 的核心功能进行系统化说明，涵盖其职责边界、关键方法、数据模型、调用流程与错误处理策略，并结合实际代码路径给出使用范式与最佳实践。读者可据此快速上手配置与扩展 Anything 相关能力，同时获得面向资深开发者的实现细节与优化建议。

## 项目结构
AnythingService 所属模块位于远程主机模块下，围绕“配置-命令-执行器”三元组组织业务逻辑；其依赖仓储层完成持久化，使用内存缓存提升读取性能，并通过命令执行器接口解耦具体执行实现。

```mermaid
graph TB
subgraph "远程主机模块"
AS["AnythingService"]
AI["AnythingInfo"]
ASI["AnythingSetting"]
ASD["AnythingSettingDetails"]
AC["AnythingCommand"]
AE["AnythingExecutor"]
CIn["CommandInfoInDto"]
CIT["CommandInfoTaskDto"]
CR["CommandResolveDto"]
end
subgraph "基础设施"
RB["RepositoryBase<T>"]
IC["ICommandExecutor"]
EA["ExecutorAttribute"]
end
subgraph "公共DTO"
OR["OperationResult"]
RR["RequestResult<T>"]
end
AS --> RB
AS --> AI
AS --> ASI
AS --> ASD
AS --> AC
AS --> AE
AS --> CIn
AS --> CIT
AS --> CR
AS --> OR
AS --> RR
AS --> IC
IC --> EA
```

图表来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L30-L680)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L14-L73)
- [ExecutorAttribute.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ExecutorAttribute.cs#L9-L25)
- [OperationResult.cs](file://Sylas.RemoteTasks.Common/Dtos/OperationResult.cs#L8-L52)
- [RequestResult.cs](file://Sylas.RemoteTasks.Common/Dtos/RequestResult.cs#L6-L65)

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L17-L680)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)

## 核心组件
- AnythingService：Anything 配置与命令的统一管理入口，负责查询、增删改、命令解析、执行调度与缓存维护。
- AnythingSetting/AnythingSettingDetails：配置实体及带命令明细的视图模型。
- AnythingInfo：运行时对象，承载标题、属性、命令集合与命令执行器名称。
- RepositoryBase<T>：通用仓储，封装分页查询、新增、更新、删除等基础操作。
- ICommandExecutor/ExecutorAttribute：命令执行器抽象与依赖注入装配标记。
- DTOs：OperationResult、RequestResult<T> 作为统一的返回体。

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L30-L680)
- [AnythingSetting.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSetting.cs#L8-L34)
- [AnythingSettingDetails.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetails.cs#L3-L11)
- [AnythingInfo.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingInfo.cs#L9-L38)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L14-L73)
- [ExecutorAttribute.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ExecutorAttribute.cs#L9-L25)
- [OperationResult.cs](file://Sylas.RemoteTasks.Common/Dtos/OperationResult.cs#L8-L52)
- [RequestResult.cs](file://Sylas.RemoteTasks.Common/Dtos/RequestResult.cs#L6-L65)

## 架构总览
AnythingService 采用“配置-命令-执行器”的三层结构：
- 配置层：AnythingSetting/Details 描述操作对象与执行器选择。
- 命令层：AnythingCommand 定义可执行命令与模板化内容。
- 执行层：ICommandExecutor 抽象具体执行器，通过反射或 DI 创建实例。

```mermaid
classDiagram
class AnythingService {
+GetAnythingSettingsAsync(search)
+GetAnythingSettingByIdAsync(id)
+GetAnythingSettingDetailsByIdAsync(id)
+AddAnythingSettingAsync(setting)
+DeleteAnythingSettingByIdAsync(id)
+AddCommandAsync(command)
+UpdateAnythingSettingAsync(map)
+ExecutorsAsync(search)
+ExecuteAsync(dto)
+GetAllAnythingInfosAsync()
+GetAnythingInfoBySettingIdAsync(id)
+ResolveCommandSettingAsync(dto)
}
class AnythingSetting {
+int Id
+string Title
+string Properties
+int Executor
+ToDetails(commands)
}
class AnythingSettingDetails {
+IEnumerable~AnythingCommand~ Commands
}
class AnythingInfo {
+string Title
+IEnumerable~AnythingCommand~ Commands
+Dictionary~string,object~ Properties
+int SettingId
+string CommandExecutor
}
class RepositoryBase_T_ {
+GetPageAsync(search)
+GetByIdAsync(id)
+AddAsync(t)
+UpdateAsync(map)
+DeleteAsync(id)
}
class ICommandExecutor {
+ExecuteAsync(command)
+Create(name,args,scope)
}
AnythingService --> RepositoryBase_T_ : "依赖"
AnythingService --> AnythingSetting : "读写"
AnythingService --> AnythingSettingDetails : "转换"
AnythingService --> AnythingInfo : "构建"
AnythingService --> ICommandExecutor : "创建/调用"
```

图表来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L30-L680)
- [AnythingSetting.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSetting.cs#L8-L34)
- [AnythingSettingDetails.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetails.cs#L3-L11)
- [AnythingInfo.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingInfo.cs#L9-L38)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L14-L73)

## 详细组件分析

### AnythingService 关键方法与使用模式
- 查询与详情
  - GetAnythingSettingsAsync：分页查询配置集合，返回分页结果。
  - GetAnythingSettingByIdAsync：按主键获取配置。
  - GetAnythingSettingDetailsByIdAsync：获取配置及命令明细（含分页查询命令）。
- 新增与删除
  - AddAnythingSettingAsync：新增配置并返回操作结果。
  - DeleteAnythingSettingByIdAsync：删除配置并级联删除其命令。
  - DeleteAnythingCommandByIdAsync：删除单条命令并同步更新缓存中的 AnythingInfo。
- 命令管理
  - GetAnythingCommandsAsync：按 AnythingId 获取命令列表。
  - AddCommandAsync：新增命令并更新缓存中的 AnythingInfo。
  - UpdateAnythingSettingAsync：基于字典的局部更新配置。
  - UpdateCommandAsync：基于字典的局部更新命令，必要时刷新缓存。
- 运行时信息与执行
  - GetAllAnythingInfosAsync：构建并缓存所有 AnythingInfo，供前端展示。
  - GetAnythingInfoBySettingIdAsync：按设置ID获取运行时信息，优先命中缓存。
  - ResolveCommandSettingAsync：解析命令模板，返回解析后的命令文本。
  - ExecuteAsync：执行命令，支持本地与跨节点转发，返回流式结果。
  - ExecutorsAsync：查询可用的命令执行器列表。
- 缓存与队列
  - 内存缓存：AllAnythingInfos、单个 AnythingInfo、Executor 查询结果。
  - 任务队列：按域名维护命令任务队列，用于跨节点命令调度。

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L45-L680)

### 数据模型与关系
- AnythingSetting/AnythingSettingDetails：配置实体与带命令明细的视图模型，支持从配置生成详情。
- AnythingInfo：运行时对象，包含标题、属性字典、命令集合与命令执行器名称。
- 实体基类：EntityBase<int> 提供 Id、CreateTime、UpdateTime 等通用字段。

```mermaid
erDiagram
ANYTHING_SETTING {
int Id PK
string Title
string Properties
int Executor
datetime CreateTime
datetime UpdateTime
}
ANYTHING_COMMAND {
int Id PK
int AnythingId FK
string Name
string CommandTxt
string ExecutedState
string Domain
datetime CreateTime
datetime UpdateTime
}
ANYTHING_EXECUTOR {
int Id PK
string Name
string Arguments
datetime CreateTime
datetime UpdateTime
}
ANYTHING_SETTING ||--o{ ANYTHING_COMMAND : "拥有"
ANYTHING_SETTING }o--|| ANYTHING_EXECUTOR : "使用"
```

图表来源
- [AnythingSetting.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSetting.cs#L8-L34)
- [AnythingSettingDetails.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetails.cs#L3-L11)
- [AnythingInfo.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingInfo.cs#L9-L38)
- [EntityBase.cs](file://Sylas.RemoteTasks.Database/EntityBase.cs#L9-L32)

章节来源
- [AnythingSetting.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSetting.cs#L8-L34)
- [AnythingSettingDetails.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingSettingDetails.cs#L3-L11)
- [AnythingInfo.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingInfo.cs#L9-L38)
- [EntityBase.cs](file://Sylas.RemoteTasks.Database/EntityBase.cs#L9-L32)

### 执行流程与序列图
以下序列图展示了 ExecuteAsync 的关键调用链：从命令ID解析到执行器创建、命令模板解析、跨节点转发与结果回传。

```mermaid
sequenceDiagram
participant Client as "客户端"
participant Controller as "HostsController"
participant Service as "AnythingService"
participant Repo as "RepositoryBase"
participant Exec as "ICommandExecutor"
Client->>Controller : "POST /Hosts/ExecuteCommand"
Controller->>Service : "ExecuteAsync(dto)"
Service->>Repo : "GetByIdAsync(CommandId)"
Repo-->>Service : "AnythingCommand"
Service->>Service : "GetAnythingInfoBySettingIdAsync(AnythingId)"
alt "目标域与本机域不同"
Service->>Service : "GetCommandTaskAsync/队列入队"
Service-->>Client : "SSE 流式返回结果"
else "本机执行"
Service->>Service : "ResolveCommandSettingAsync"
Service->>Exec : "Create(executorName, args)"
Exec-->>Service : "Func<object[], IAsyncEnumerable<CommandResult>>"
Service->>Exec : "ExecuteAsync(resolvedCommand)"
Exec-->>Service : "IAsyncEnumerable<CommandResult>"
Service-->>Client : "SSE 流式返回结果"
end
```

图表来源
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs#L85-L97)
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L294-L491)
- [CommandInfoInDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandInfoInDto.cs#L3-L14)
- [CommandInfoTaskDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandInfoTaskDto.cs#L3-L18)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L31-L73)

章节来源
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs#L85-L97)
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L294-L491)

### 命令解析与模板处理
- ResolveCommandSettingAsync：根据 AnythingSetting 的 Properties 解析命令模板，返回解析后的命令文本。
- BuildAnythingInfoAsync：解析执行器参数、构建执行器实例、解析命令模板、预执行状态命令并生成 AnythingInfo。

```mermaid
flowchart TD
Start(["开始"]) --> LoadSetting["加载 AnythingSetting"]
LoadSetting --> BuildProps["构建属性字典<br/>合并默认常量"]
BuildProps --> ParseExecutor["解析执行器与参数"]
ParseExecutor --> CreateExecutor["创建执行器实例"]
CreateExecutor --> ResolveCmds["逐条解析命令模板"]
ResolveCmds --> PreState{"是否包含已执行状态?"}
PreState --> |是| ExecState["执行状态命令并收集输出"]
PreState --> |否| SkipState["跳过"]
ExecState --> BuildInfo["组装 AnythingInfo"]
SkipState --> BuildInfo
BuildInfo --> End(["结束"])
```

图表来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L529-L631)
- [CommandResolveDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandResolveDto.cs#L3-L14)

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L529-L631)
- [CommandResolveDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandResolveDto.cs#L3-L14)

### 配置选项、参数与返回值
- GetAnythingSettingsAsync
  - 参数：DataSearch（可空，默认构造）
  - 返回：PagedData<AnythingSetting>
- GetAnythingSettingByIdAsync
  - 参数：int id
  - 返回：AnythingSetting?
- GetAnythingSettingDetailsByIdAsync
  - 参数：int id
  - 返回：AnythingSettingDetails
- AddAnythingSettingAsync
  - 参数：AnythingSetting
  - 返回：OperationResult
- DeleteAnythingSettingByIdAsync
  - 参数：int id
  - 返回：OperationResult
- DeleteAnythingCommandByIdAsync
  - 参数：int id
  - 返回：OperationResult
- GetAnythingCommandsAsync
  - 参数：int anythingId
  - 返回：IEnumerable<AnythingCommand>
- AddCommandAsync
  - 参数：AnythingCommand
  - 返回：RequestResult<bool>
- UpdateAnythingSettingAsync
  - 参数：Dictionary<string, string>
  - 返回：OperationResult
- UpdateCommandAsync
  - 参数：Dictionary<string, string>
  - 返回：OperationResult
- GetAllAnythingInfosAsync
  - 参数：无
  - 返回：List<AnythingInfo>
- GetAnythingInfoBySettingIdAsync
  - 参数：int settingId
  - 返回：AnythingInfo
- ResolveCommandSettingAsync
  - 参数：CommandResolveDto（Id, CmdTxt）
  - 返回：RequestResult<string>
- ExecuteAsync
  - 参数：CommandInfoInDto（CommandId, CommandExecuteNo）
  - 返回：IAsyncEnumerable<CommandResult>
- ExecutorsAsync
  - 参数：DataSearch
  - 返回：PagedData<AnythingExecutor>

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L45-L680)
- [CommandInfoInDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandInfoInDto.cs#L3-L14)
- [CommandResolveDto.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/CommandResolveDto.cs#L3-L14)
- [OperationResult.cs](file://Sylas.RemoteTasks.Common/Dtos/OperationResult.cs#L8-L52)
- [RequestResult.cs](file://Sylas.RemoteTasks.Common/Dtos/RequestResult.cs#L6-L65)

### 与控制器的集成
- HostsController.ExecuteCommandAsync：以 Server-Sent Events 形式推送命令执行结果，支持并发匹配 CommandExecuteNo。

章节来源
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs#L85-L97)
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L294-L491)

## 依赖关系分析
- AnythingService 依赖仓储层完成数据访问，使用内存缓存减少重复查询与解析开销。
- 命令执行器通过 ICommandExecutor 抽象创建，支持静态类与 DI 注入两种方式。
- 控制器通过 ExecuteAsync 暴露流式执行接口，便于前端实时展示执行进度与日志。

```mermaid
graph LR
Controller["HostsController"] --> Service["AnythingService"]
Service --> Repo["RepositoryBase<T>"]
Service --> Cache["IMemoryCache"]
Service --> Exec["ICommandExecutor"]
Exec --> Attr["ExecutorAttribute"]
```

图表来源
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs#L85-L97)
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L30-L680)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L31-L73)
- [ExecutorAttribute.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ExecutorAttribute.cs#L18-L23)

章节来源
- [HostsController.cs](file://Sylas.RemoteTasks.App/Controllers/HostsController.cs#L85-L97)
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L30-L680)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L10-L233)
- [ICommandExecutor.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ICommandExecutor.cs#L31-L73)
- [ExecutorAttribute.cs](file://Sylas.RemoteTasks.Utils/CommandExecutor/ExecutorAttribute.cs#L18-L23)

## 性能考量
- 缓存策略
  - 全量 AnythingInfo 列表缓存（滑动过期），避免重复构建。
  - 单个 AnythingInfo 缓存（滑动过期），按设置ID索引。
  - 执行器查询缓存（短时过期），降低频繁解析成本。
- 异步与流式
  - ExecuteAsync 返回 IAsyncEnumerable，边执行边输出，降低等待时延。
- 数据访问
  - RepositoryBase<T> 支持分页查询与局部更新，减少不必要的网络与解析开销。

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L255-L277)
- [RepositoryBase.cs](file://Sylas.RemoteTasks.App/Infrastructure/RepositoryBase.cs#L20-L181)

## 故障排查指南
- 未知命令
  - 现象：抛出异常提示“未知的命令”。
  - 排查：确认 CommandId 是否存在，命令是否属于正确的 Anything 设置。
  - 参考：[AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L296)
- 无效的 Anything 或执行器
  - 现象：提示“无效的 Anything”或“无效的 AnythingExecutor”。
  - 排查：检查设置ID与执行器ID是否有效；确认执行器参数 JSON 是否正确。
  - 参考：[AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L506-L512)
- 命令解析失败
  - 现象：ResolveCommandSettingAsync 返回错误。
  - 排查：检查 Properties 中模板变量是否完整，CmdTxt 是否为空。
  - 参考：[AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L663-L677)
- 跨节点执行失败
  - 现象：授权失败或请求失败。
  - 排查：确认中心服务器地址、认证头传递、目标节点可达性。
  - 参考：[AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L336-L373)
- 命令执行超时
  - 现象：长时间无结果返回。
  - 排查：检查命令耗时、网络延迟、中心服务器负载；适当延长等待时间。
  - 参考：[AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L440-L491)

章节来源
- [AnythingService.cs](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L296-L677)

## 结论
AnythingService 通过清晰的配置-命令-执行器分层设计，结合内存缓存与异步流式执行，提供了灵活且高性能的远程命令执行能力。对于初学者，建议从配置与命令模板入手，逐步掌握执行器参数与跨节点调度；对于资深开发者，可关注缓存策略、执行器扩展点与异常恢复机制，以进一步提升系统的稳定性与可观测性。

## 附录
- 常用调用路径参考
  - 查询配置：[GetAnythingSettingsAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L45-L50)
  - 获取详情：[GetAnythingSettingDetailsByIdAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L68-L76)
  - 新增配置：[AddAnythingSettingAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L83-L87)
  - 删除配置与命令：[DeleteAnythingSettingByIdAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L94-L106)、[DeleteAnythingCommandByIdAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L112-L143)
  - 新增命令：[AddCommandAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L174-L197)
  - 更新配置/命令：[UpdateAnythingSettingAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L204-L208)、[UpdateCommandAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L214-L246)
  - 获取执行器列表：[ExecutorsAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L283-L287)
  - 执行命令：[ExecuteAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L294-L491)
  - 构建运行时信息：[BuildAnythingInfoAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L529-L631)
  - 解析命令模板：[ResolveCommandSettingAsync](file://Sylas.RemoteTasks.App/RemoteHostModule/Anything/AnythingService.cs#L663-L677)