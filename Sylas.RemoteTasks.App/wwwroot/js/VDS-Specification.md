# VDS 规范（View Definition Specification）

## 1. 概述

VDS（View Definition Specification）是一种**声明式页面定义规范**，通过标准化的 JavaScript 配置对象描述数据管理页面的结构、字段、接口和行为。

### 1.1 设计理念

VDS 采用**契约式设计**：

```
┌─────────────────────────────────────────────────────┐
│                   VDS 规范定义                       │
│     (标准化的页面结构、字段、接口配置格式)             │
└─────────────────────────────────────────────────────┘
                          │
                     规范契约
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
   │  site.js    │ │  Vue 实现   │ │  React 实现 │
   │ (Bootstrap) │ │ (Element)   │ │ (Ant Design)│
   └─────────────┘ └─────────────┘ └─────────────┘
```

**核心特点**：
- **规范与实现分离**：VDS 只定义配置格式，具体渲染由实现层完成
- **跨框架复用**：同一份 VDS 配置可被不同 UI 框架的实现解析
- **契约保证**：实现层必须遵循 VDS 规范，确保配置的可移植性

---

## 2. 核心配置结构

```javascript
createTable({
    // ========== 必需配置 ==========
    apiUrl: string,                    // 数据查询接口地址
    tableId: string,                   // 页面唯一标识ID
    tableContainerSelector: string,    // 容器DOM选择器
    ths: FieldDefinition[],            // 字段定义数组
    idFieldName: string,               // 主键字段名（通常为 "id"）

    // ========== 分页配置 ==========
    pageIndex: number,                 // 默认页码，默认 1
    pageSize: number,                  // 每页条数，默认 10

    // ========== 弹窗/表单配置 ==========
    modalSettings: {
        url: string,                   // 添加数据接口
        method: string,                // 添加数据请求方法
        updateUrl: string,             // 更新数据接口
        updateMethod: string           // 更新数据请求方法
    },

    // ========== 可选配置 ==========
    primaryKeyIsInt: boolean,          // 主键是否为整数，默认 true
    addButtonSelector: string,         // 添加按钮挂载位置选择器
    wrapper: string,                   // 表格外层包装HTML
    data: array,                       // 静态数据（不走接口）
    
    // ========== 排序规则 ==========
    orderRules: [{
        fieldName: string,             // 排序字段名
        isAsc: boolean                 // 是否升序
    }],

    // ========== 回调函数 ==========
    onDataLoaded: function(row),       // 每行数据加载后回调
    onDataAllLoaded: function(data),   // 全部数据加载后回调

    // ========== 自定义视图 ==========
    dataViewBuilder: function(data)    // 自定义视图构建器（不使用表格时）
})
```

---

## 3. 字段定义（FieldDefinition）

```javascript
{
    name: string,                      // 字段名（对应API返回的数据字段）
    title: string,                     // 显示标题
    
    // ========== 搜索配置 ==========
    searchedByKeywords: boolean,       // 是否参与关键字搜索
    
    // ========== 显示配置 ==========
    showPart: number,                  // 截取显示长度（超长截断）
    align: 'left'|'center'|'right',    // 对齐方式
    formatter: function(value),        // 值格式化函数
    notShowInForm: boolean,            // 是否在表单中隐藏

    // ========== 输入类型配置 ==========
    multiLines: boolean,               // 多行文本（生成 textarea）
    isNumber: boolean,                 // 数字类型输入
    enumValus: string[],               // 枚举值（生成 select 下拉框）
    
    // ========== 特殊类型 ==========
    type: string,                      // 字段类型（见下方类型说明）
    tmpl: string,                      // 按钮模板（type='button'时使用）
}
```

### 3.1 字段类型（type）

| 类型 | 说明 | 配置示例 |
|------|------|----------|
| `button` | 操作按钮列 | `type: 'button', tmpl: '<button>...</button>'` |
| `image` | 图片上传 | `type: 'image'` |
| `media` | 多媒体（图片/音频/视频） | `type: 'media'` |
| `dataSource` | 远程数据源下拉框 | `type: 'dataSource\|defaultValue=0\|dataSourceApi=/api/xxx\|displayField=name'` |

### 3.2 dataSource 类型详细配置

```javascript
{
    name: 'executor',
    title: '执行者',
    type: 'dataSource|defaultValue=0|dataSourceApi=/Hosts/Executors?pageIndex=1&pageSize=1000|displayField=name'
}
// 参数说明：
// - defaultValue: 默认值
// - dataSourceApi: 数据源API地址
// - displayField: 显示字段名
```

---

## 4. 操作按钮模板语法

在 `tmpl` 中使用 `{{fieldName}}` 插值引用当前行数据：

```javascript
{
    name: '',
    title: '操作',
    type: 'button',
    tmpl: `
        <button data-id="{{id}}" onclick="showUpdatePannel(this)">修改</button>
        <button data-content="&quot;{{id}}&quot;" data-execute-url="/api/delete" onclick="execute(this)">删除</button>
    `
}
```

### 按钮属性约定

| 属性 | 说明 |
|------|------|
| `data-table-id` | 表格ID，用于操作后刷新 |
| `data-id` | 记录ID |
| `data-content` | 请求体内容 |
| `data-fetch-url` | 获取数据的接口 |
| `data-execute-url` | 执行操作的接口 |
| `data-method` | 请求方法 |

---

## 5. 自定义视图构建器

当需要非表格展示（如卡片、列表等）时，使用 `dataViewBuilder`：

```javascript
dataViewBuilder: function(data) {
    let container = document.createElement('div');
    // 根据 data 构建自定义HTML
    data.forEach(record => {
        container.innerHTML += `<div class="card">${record.title}</div>`;
    });
    return container;  // 必须返回DOM元素
}
```

---

## 6. 完整使用示例

### 6.1 简单表格页面

```html
<!-- Views/Users/Index.cshtml -->
<div id="userContainer"></div>

<script>
createTable({
    apiUrl: "/Users/List",
    tableId: 'userTable',
    tableContainerSelector: "#userContainer",
    ths: [
        { name: 'username', title: '用户名', searchedByKeywords: true },
        { name: 'email', title: '邮箱', searchedByKeywords: true },
        { name: 'role', title: '角色', enumValus: ['Admin', 'User', 'Guest'] },
        { name: 'avatar', title: '头像', type: 'image' },
        { name: 'createTime', title: '创建时间', align: 'center' },
        { 
            name: '', 
            title: '操作', 
            type: 'button', 
            tmpl: `
                <button data-id="{{id}}" onclick="showUpdatePannel(this)">编辑</button>
                <button data-content="&quot;{{id}}&quot;" data-execute-url="/Users/Delete" onclick="execute(this)">删除</button>
            `
        }
    ],
    idFieldName: "id",
    pageSize: 20,
    modalSettings: { 
        url: '/Users/Add', 
        method: 'POST', 
        updateUrl: '/Users/Update', 
        updateMethod: 'POST' 
    },
    orderRules: [{ fieldName: 'createTime', isAsc: false }]
});
</script>
```

### 6.2 自定义卡片页面

```javascript
createTable({
    apiUrl: "/Projects/List",
    tableId: 'projectTable',
    tableContainerSelector: "#projectContainer",
    ths: [
        { name: 'name', title: '项目名', searchedByKeywords: true },
        { name: 'description', title: '描述', multiLines: true },
    ],
    idFieldName: "id",
    dataViewBuilder: function(data) {
        let container = document.createElement('div');
        container.className = 'row';
        data.forEach(project => {
            container.innerHTML += `
                <div class="col-md-4">
                    <div class="card mb-3">
                        <div class="card-body">
                            <h5>${project.name}</h5>
                            <p>${project.description}</p>
                        </div>
                    </div>
                </div>`;
        });
        return container;
    },
    modalSettings: { url: '/Projects/Add', method: 'POST' }
});
```

---

## 7. 规范特点总结

| 特性 | 说明 |
|------|------|
| **声明式** | 只需配置JSON对象，无需编写DOM操作代码 |
| **契约式** | 规范与实现分离，支持跨框架复用 |
| **自动生成** | 自动生成搜索表单、数据表格/视图、分页、添加/修改弹窗 |
| **灵活扩展** | 支持自定义视图构建器、回调函数 |
| **类型丰富** | 支持文本、数字、枚举、图片、多媒体、远程数据源等 |
| **CRUD完整** | 内置增删改查全套操作逻辑 |

---

## 8. 实现层

当前项目基于 Bootstrap 的实现：

| 文件 | 说明 |
|------|------|
| `wwwroot/js/site.js` | VDS 核心实现，`createTable()` 函数 |
| `wwwroot/js/anything.js` | 业务扩展实现 |

### 示例页面

| 页面 | 类型 | 路径 |
|------|------|------|
| 数据库管理 | 表格 | `Views/Database/Index.cshtml` |
| Anything管理 | 自定义卡片 | `Views/Hosts/AnythingInfos.cshtml` |

---

## 9. 扩展指南

如需在其他框架中实现 VDS 规范：

1. **解析配置对象**：读取 `apiUrl`、`ths`、`modalSettings` 等配置
2. **渲染数据视图**：根据 `ths` 字段定义生成表格/列表/卡片
3. **生成表单**：根据字段类型生成对应的表单控件
4. **绑定 CRUD 操作**：调用 `modalSettings` 中定义的接口
5. **处理回调**：在适当时机调用 `onDataLoaded`、`onDataAllLoaded`

实现层只需遵循 VDS 配置格式，即可实现配置的跨框架复用。
