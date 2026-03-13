/**
 * 可视化模板编辑器
 * 用于辅助编写 FileHelper 命令模板
 */
const TemplateEditor = {
    // 编辑器实例
    editor: null,
    // 环境变量列表
    envVars: [],
    // 内置函数列表
    builtinFunctions: [],
    // 操作类型列表
    operationTypes: [],
    // 当前 Setting ID
    settingId: null,
    // 模态框实例
    modal: null,

    /**
     * 初始化编辑器
     * @param {number} settingId - AnythingSetting ID
     * @param {string} editorId - 编辑器 textarea 的 ID
     */
    async init(envVarsJson, editorId) {
        this.editor = document.getElementById(editorId);
        
        // 加载数据（全部同步）
        this.loadBuiltinFunctions();
        this.loadOperationTypes();
        this.loadEnvVars(envVarsJson);
        
        // 设置自动补全
        this.setupAutoComplete();
    },

    /**
     * 加载环境变量（设置变量 + 内置变量）
     * @param {string} envVarsJson - 用户设置的变量 JSON
     * @param {object} resolvedProps - 解析后的所有变量（含内置变量）
     */
    loadEnvVars(envVarsJson, resolvedProps) {
        this.envVars = [];
        try {
            // 1. 解析用户设置的变量
            const userVars = new Set();
            if (envVarsJson && envVarsJson.trim()) {
                const cleanedJson = envVarsJson.replace(/,\s*([\]}])/g, '$1');
                const propsObj = JSON.parse(cleanedJson);
                Object.entries(propsObj).forEach(([key, value]) => {
                    this.envVars.push({
                        key,
                        value: String(value),
                        isBuiltin: false
                    });
                    userVars.add(key);
                });
            }
            
            // 2. 添加内置变量（排除用户已设置的）
            if (resolvedProps && typeof resolvedProps === 'object') {
                Object.entries(resolvedProps).forEach(([key, value]) => {
                    if (!userVars.has(key)) {
                        this.envVars.push({
                            key,
                            value: String(value),
                            isBuiltin: true
                        });
                    }
                });
            }
        } catch (e) {
            console.error('加载环境变量失败:', e);
        }
    },

    /**
     * 加载内置函数列表（硬编码）
     */
    loadBuiltinFunctions() {
        this.builtinFunctions = [
            {
                name: "GetSolutionDirectory",
                description: "获取解决方案根目录",
                params: [],
                returns: ["SolutionDir"]
            },
            {
                name: "GetDirectoriesUnderSolution",
                description: "获取解决方案下所有文件夹",
                params: [],
                returns: ["Directories"]
            },
            {
                name: "GetFileProjDirAndNamespace",
                description: "获取文件所属项目目录和根命名空间",
                params: [
                    { name: "workingDir", type: "string", hint: "工作目录，如 ${WorkingDir}" },
                    { name: "pattern", type: "string", hint: "文件名匹配模式，如 Service" }
                ],
                returns: ["ProjDir", "RootNS"]
            },
            {
                name: "GetDirectoryFileInfo",
                description: "扫描目录，用正则匹配文件内容",
                params: [
                    { name: "dir", type: "string", hint: "目录路径" },
                    { name: "patterns", type: "string", hint: "正则表达式，多个用逗号分隔" }
                ],
                returns: ["MatchedResults"]
            },
            {
                name: "ToCamelCase",
                description: "转换为驼峰命名",
                params: [
                    { name: "origin", type: "string", hint: "原始字符串" }
                ],
                returns: ["Result"]
            },
            {
                name: "Pluralize",
                description: "单词复数化",
                params: [
                    { name: "word", type: "string", hint: "单数单词" }
                ],
                returns: ["PluralWord"]
            },
            {
                name: "BuildEntityClassCodeAsync",
                description: "根据数据库表生成实体类代码",
                params: [
                    { name: "connectionString", type: "string", hint: "数据库连接字符串" },
                    { name: "table", type: "string", hint: "表名" }
                ],
                returns: ["EntityCode", "DtoCode", "PropsCode"]
            },
            {
                name: "BuildWhereIfStatement",
                description: "根据属性代码生成 WhereIf 链式语句",
                params: [
                    { name: "propsCode", type: "string", hint: "实体属性代码" }
                ],
                returns: ["WhereIfCode"]
            },
            {
                name: "GetDateTimePropAssignCode",
                description: "生成时间属性赋值代码",
                params: [
                    { name: "entityPropsCode", type: "string", hint: "实体属性代码" }
                ],
                returns: ["AddAssignCode", "UpdateAssignCode"]
            },
            {
                name: "UnformatJsonString",
                description: "压缩 JSON 字符串（去除换行、空格）",
                params: [
                    { name: "settings", type: "string", hint: "JSON 字符串" }
                ],
                returns: ["CompactJson"]
            }
        ];
    },

    /**
     * 加载操作类型列表（硬编码）
     */
    loadOperationTypes() {
        this.operationTypes = [
            { value: "Create", label: "Create (创建文件)", description: "创建新文件，如果文件已存在则覆盖" },
            { value: "Override", label: "Override (覆盖文件)", description: "覆盖整个文件内容" },
            { value: "Append", label: "Append (追加内容)", description: "在匹配行之后追加内容" },
            { value: "Prepend", label: "Prepend (前置内容)", description: "在匹配行之前插入内容" },
            { value: "Replace", label: "Replace (替换内容)", description: "替换匹配的内容" }
        ];
    },

    /**
     * 创建工具箱面板 HTML
     */
    createToolboxHtml() {
        // 分离用户变量和内置变量
        const userVars = this.envVars.filter(v => !v.isBuiltin);
        const builtinVars = this.envVars.filter(v => v.isBuiltin);
        
        // 用户设置的变量（蓝色）
        const userVarsHtml = userVars.map(v => `
            <div class="tmpl-var-item d-flex justify-content-between align-items-center py-1 px-2 border-bottom" style="color: #212529;">
                <div class="text-truncate" style="max-width: 150px;" title="${v.key}=${v.value}">
                    <span class="fw-bold" style="color: #0d6efd;">${v.key}</span>
                    <span style="color: #6c757d;" class="small">=</span>
                    <span style="color: #495057;" class="small">${v.value.substring(0, 15)}${v.value.length > 15 ? '...' : ''}</span>
                </div>
                <button class="btn btn-sm btn-outline-primary py-0 px-1 insert-var-btn" data-var="${v.key}" title="插入变量">+</button>
            </div>
        `).join('');
        
        // 内置变量（灰色/紫色）
        const builtinVarsHtml = builtinVars.map(v => `
            <div class="tmpl-var-item d-flex justify-content-between align-items-center py-1 px-2 border-bottom" style="color: #212529; background-color: #f8f9fa;">
                <div class="text-truncate" style="max-width: 150px;" title="${v.key}=${v.value}">
                    <span class="fw-bold" style="color: #6f42c1;">${v.key}</span>
                    <span style="color: #6c757d;" class="small">=</span>
                    <span style="color: #495057;" class="small">${v.value.substring(0, 15)}${v.value.length > 15 ? '...' : ''}</span>
                </div>
                <button class="btn btn-sm btn-outline-secondary py-0 px-1 insert-var-btn" data-var="${v.key}" title="插入内置变量">+</button>
            </div>
        `).join('');
        
        // 合并环境变量 HTML
        const envVarsHtml = userVarsHtml + (builtinVars.length > 0 ? 
            `<div class="px-2 py-1 small" style="color: #6f42c1; background-color: #f0f0f0;">📌 内置变量</div>${builtinVarsHtml}` : '');

        // 内置函数列表
        const functionsHtml = this.builtinFunctions.map(f => `
            <div class="tmpl-func-item d-flex justify-content-between align-items-center py-1 px-2 border-bottom" style="color: #212529;">
                <div class="text-truncate" style="max-width: 180px;" title="${f.description}">
                    <span class="fw-bold" style="color: #198754;">${f.name}</span>
                </div>
                <button class="btn btn-sm btn-outline-success py-0 px-1 insert-func-btn" data-func="${f.name}" title="配置并插入">+</button>
            </div>
        `).join('');

        // 操作类型列表
        const opTypesHtml = this.operationTypes.map(t => `
            <div class="tmpl-op-item py-1 px-2 border-bottom" style="color: #212529;">
                <div class="form-check">
                    <input class="form-check-input op-type-radio" type="radio" name="opType" value="${t.value}" id="op-${t.value}">
                    <label class="form-check-label small" for="op-${t.value}" title="${t.description}" style="color: #212529;">
                        ${t.label}
                    </label>
                </div>
            </div>
        `).join('');

        // 条件语法
        const syntaxHtml = `
            <div class="tmpl-syntax-item py-1 px-2 border-bottom" style="color: #212529;">
                <div class="d-flex justify-content-between align-items-center">
                    <span class="small">#IF...#IFEND</span>
                    <button class="btn btn-sm btn-outline-secondary py-0 px-1 insert-syntax-btn" data-syntax="if" title="插入条件语法">+</button>
                </div>
            </div>
            <div class="tmpl-syntax-item py-1 px-2 border-bottom" style="color: #212529;">
                <div class="d-flex justify-content-between align-items-center">
                    <span class="small">$for...$forend</span>
                    <button class="btn btn-sm btn-outline-secondary py-0 px-1 insert-syntax-btn" data-syntax="for" title="插入循环语法">+</button>
                </div>
            </div>
        `;

        // 节点模板
        const nodeTemplateHtml = `
            <div class="tmpl-node-item py-1 px-2">
                <button class="btn btn-sm btn-warning w-100 add-node-btn">
                    <i class="bi bi-plus-circle"></i> 添加操作节点
                </button>
            </div>
        `;

        return `
            <div class="template-toolbox" style="width: 260px; border-right: 1px solid #dee2e6; overflow-y: auto; max-height: 500px; background-color: #fff;">
                <!-- 环境变量 -->
                <div class="toolbox-panel">
                    <div class="panel-header px-2 py-1 fw-bold small d-flex justify-content-between align-items-center" 
                         onclick="TemplateEditor.togglePanel(this)" style="cursor: pointer; background-color: #e9ecef; color: #212529;">
                        <span><i class="bi bi-chevron-down"></i> 📦 环境变量 (${this.envVars.length})</span>
                    </div>
                    <div class="panel-body" style="max-height: 150px; overflow-y: auto; background-color: #fff;">
                        ${envVarsHtml || '<div class="text-muted small p-2">暂无环境变量</div>'}
                    </div>
                </div>
                
                <!-- 内置函数 -->
                <div class="toolbox-panel">
                    <div class="panel-header px-2 py-1 fw-bold small d-flex justify-content-between align-items-center"
                         onclick="TemplateEditor.togglePanel(this)" style="cursor: pointer; background-color: #e9ecef; color: #212529;">
                        <span><i class="bi bi-chevron-down"></i> 🔧 内置函数 (${this.builtinFunctions.length})</span>
                    </div>
                    <div class="panel-body" style="max-height: 200px; overflow-y: auto; background-color: #fff;">
                        ${functionsHtml}
                    </div>
                </div>
                
                <!-- 操作类型 -->
                <div class="toolbox-panel">
                    <div class="panel-header px-2 py-1 fw-bold small"
                         onclick="TemplateEditor.togglePanel(this)" style="cursor: pointer; background-color: #e9ecef; color: #212529;">
                        <span><i class="bi bi-chevron-down"></i> 📝 操作类型</span>
                    </div>
                    <div class="panel-body" style="background-color: #fff;">
                        ${opTypesHtml}
                    </div>
                </div>
                
                <!-- 条件语法 -->
                <div class="toolbox-panel">
                    <div class="panel-header px-2 py-1 fw-bold small"
                         onclick="TemplateEditor.togglePanel(this)" style="cursor: pointer; background-color: #e9ecef; color: #212529;">
                        <span><i class="bi bi-chevron-down"></i> 🔀 条件/循环</span>
                    </div>
                    <div class="panel-body" style="background-color: #fff;">
                        ${syntaxHtml}
                    </div>
                </div>
                
                <!-- 添加节点 -->
                <div class="toolbox-panel">
                    <div class="panel-header px-2 py-1 fw-bold small" style="background-color: #e9ecef; color: #212529;">
                        <span>➕ 快速添加</span>
                    </div>
                    <div class="panel-body" style="background-color: #fff;">
                        ${nodeTemplateHtml}
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * 折叠/展开面板
     */
    togglePanel(header) {
        const body = header.nextElementSibling;
        const icon = header.querySelector('i');
        if (body.style.display === 'none') {
            body.style.display = 'block';
            icon.className = 'bi bi-chevron-down';
        } else {
            body.style.display = 'none';
            icon.className = 'bi bi-chevron-right';
        }
    },

    /**
     * 显示模板编辑器模态框
     * @param {string} commandInputId - 命令输入框的 ID
     */
    async showEditor(commandInputId, envVarsJson, resolvedProps, settingId) {
        this.settingId = settingId; // 保存用于预览功能
        this.loadEnvVars(envVarsJson, resolvedProps);
        const commandInput = document.getElementById(commandInputId) || document.querySelector(`.${commandInputId}`);
        if (!commandInput) {
            console.error('找不到命令输入框:', commandInputId);
            return;
        }

        // 先立即显示 Modal（带加载状态），避免延迟感
        const modalHtml = `
            <div class="modal fade" id="templateEditorModal" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog modal-xl modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header py-2" style="cursor: move; user-select: none;">
                            <h5 class="modal-title">📝 模板编辑器</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body p-0">
                            <div class="d-flex" style="min-height: 500px;">
                                <div class="template-toolbox" id="toolboxContainer" style="width: 260px; border-right: 1px solid #dee2e6; overflow-y: auto; max-height: 500px;">
                                    <div class="p-3 text-center text-muted">
                                        <div class="spinner-border spinner-border-sm" role="status"></div>
                                        <div class="small mt-1">加载工具箱...</div>
                                    </div>
                                </div>
                                <div class="flex-grow-1 p-2">
                                    <div class="mb-2 d-flex justify-content-between align-items-center">
                                        <div class="btn-group btn-group-sm">
                                            <button class="btn btn-outline-primary syntax-btn" data-syntax="\${}" title="引用环境变量 (预处理阶段替换)">\${...}</button>
                                            <button class="btn btn-outline-primary syntax-btn" data-syntax="{}" title="引用函数返回值 (执行阶段替换)">{...}</button>
                                            <button class="btn btn-outline-primary syntax-btn" data-syntax="@Model" title="Razor 模板变量 (需在模板中声明)">@Model.</button>
                                        </div>
                                        <div class="form-check form-switch">
                                            <input class="form-check-input" type="checkbox" id="useRazorEngine">
                                            <label class="form-check-label small" for="useRazorEngine">使用 Razor 引擎</label>
                                        </div>
                                    </div>
                                    <textarea id="tmplEditorTextarea" class="form-control font-monospace" 
                                              style="height: 450px; font-size: 13px; line-height: 1.4;"
                                              placeholder="在此编写模板内容...&#10;&#10;示例:&#10;## 操作名称(\${WorkingDir})&#10;&#10;### 节点标题&#10;TargetFilePattern: src/**/*.cs&#10;OperationType: Append&#10;LinePattern: using System;&#10;Value:&#10;using MyNamespace;">${commandInput.value}</textarea>
                                    <div class="mt-2 small text-muted">
                                        💡 提示: 点击工具箱中的 [+] 按钮插入到光标位置 | 使用 Alt+/ 触发自动补全
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer py-2">
                            <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                            <button type="button" class="btn btn-info btn-sm" id="previewTemplateBtn">预览解析结果</button>
                            <button type="button" class="btn btn-primary btn-sm" id="applyTemplateBtn">应用到命令</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // 移除旧的模态框
        const oldModal = document.getElementById('templateEditorModal');
        if (oldModal) oldModal.remove();

        // 添加新模态框
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // 立即显示模态框和启用拖拽
        const modalEl = document.getElementById('templateEditorModal');
        this.modal = new bootstrap.Modal(modalEl);
        this.modal.show();
        makeModalDraggable(modalEl);

        // 绑定基础事件（语法按钮、Razor开关、应用按钮等）
        this.bindBasicEvents(commandInput);

        // 加载数据并更新工具箱（全部同步）
        this.loadBuiltinFunctions();
        this.loadOperationTypes();
        // envVars 已在方法开头加载

        // 更新工具箱内容
        const toolboxContainer = document.getElementById('toolboxContainer');
        if (toolboxContainer) {
            toolboxContainer.outerHTML = this.createToolboxHtml();
            // 绑定工具箱事件
            this.bindToolboxEvents(document.getElementById('tmplEditorTextarea'));
        }
    },

    /**
     * 绑定基础事件（不依赖工具箱数据的事件）
     */
    bindBasicEvents(commandInput) {
        const editorTextarea = document.getElementById('tmplEditorTextarea');

        // 语法按钮
        document.querySelectorAll('.syntax-btn').forEach(btn => {
            btn.onclick = () => {
                const syntax = btn.getAttribute('data-syntax');
                if (syntax === '${}') {
                    this.insertAtCursor(editorTextarea, '${VarName}');
                } else if (syntax === '{}') {
                    this.insertAtCursor(editorTextarea, '{VarName}');
                } else if (syntax === '@Model') {
                    this.insertAtCursor(editorTextarea, '@Model.VarName');
                }
            };
        });

        // Razor 引擎开关
        document.getElementById('useRazorEngine').onchange = (e) => {
            const content = editorTextarea.value;
            if (e.target.checked && !content.includes('ENGINE: Razor')) {
                editorTextarea.value = 'ENGINE: Razor\n' + content;
            } else if (!e.target.checked && content.startsWith('ENGINE: Razor')) {
                editorTextarea.value = content.replace(/^ENGINE:\s*Razor\s*\n?/, '');
            }
        };

        // 预览按钮
        document.getElementById('previewTemplateBtn').onclick = async () => {
            await this.previewTemplate(editorTextarea.value);
        };

        // 应用按钮
        document.getElementById('applyTemplateBtn').onclick = () => {
            commandInput.value = editorTextarea.value;
            commandInput.dispatchEvent(new Event('input', { bubbles: true }));
            this.modal.hide();
        };

        // 记录手动输入到撤销栈（防抖，避免每个字符都记录）
        let lastInputTime = 0;
        let lastValue = editorTextarea.value;
        let lastSelectionStart = editorTextarea.selectionStart;
        let lastSelectionEnd = editorTextarea.selectionEnd;
        
        // 在输入前记录光标位置（beforeinput 在内容改变前触发）
        editorTextarea.addEventListener('beforeinput', () => {
            lastSelectionStart = editorTextarea.selectionStart;
            lastSelectionEnd = editorTextarea.selectionEnd;
        });
        
        editorTextarea.addEventListener('input', () => {
            const now = Date.now();
            // 每500ms保存一次快照，或者内容变化超过20字符
            if (now - lastInputTime > 500 || Math.abs(editorTextarea.value.length - lastValue.length) > 20) {
                if (lastValue !== editorTextarea.value) {
                    this.undoStack.push({
                        value: lastValue,
                        selectionStart: lastSelectionStart,
                        selectionEnd: lastSelectionEnd
                    });
                    if (this.undoStack.length > this.maxUndoSize) {
                        this.undoStack.shift();
                    }
                    this.redoStack = [];
                }
                lastInputTime = now;
            }
            // 始终更新 lastValue（用于下次比较）
            lastValue = editorTextarea.value;
        });

        // 自动补全和撤销/重做快捷键
        editorTextarea.addEventListener('keydown', (e) => {
            if (e.altKey && e.key === '/') {
                e.preventDefault();
                this.showAutoCompleteMenu(editorTextarea);
            }
            // 自定义撤销
            if (e.ctrlKey && e.key === 'z' && !e.shiftKey) {
                if (this.undoStack.length > 0) {
                    e.preventDefault();
                    this.undo(editorTextarea);
                    lastValue = editorTextarea.value;
                }
            }
            // 自定义重做
            if (e.ctrlKey && e.key === 'y') {
                if (this.redoStack.length > 0) {
                    e.preventDefault();
                    this.redo(editorTextarea);
                    lastValue = editorTextarea.value;
                }
            }
        });
    },

    /**
     * 绑定工具箱事件（依赖工具箱数据的事件）
     */
    bindToolboxEvents(editorTextarea) {
        // 插入变量按钮
        document.querySelectorAll('.insert-var-btn').forEach(btn => {
            btn.onclick = () => {
                const varName = btn.getAttribute('data-var');
                this.insertAtCursor(editorTextarea, `\${${varName}}`);
            };
        });

        // 插入函数按钮
        document.querySelectorAll('.insert-func-btn').forEach(btn => {
            btn.onclick = () => {
                const funcName = btn.getAttribute('data-func');
                this.showFunctionWizard(funcName, editorTextarea);
            };
        });

        // 条件/循环语法按钮
        document.querySelectorAll('.insert-syntax-btn').forEach(btn => {
            btn.onclick = () => {
                const syntax = btn.getAttribute('data-syntax');
                if (syntax === 'if') {
                    this.insertAtCursor(editorTextarea, '#IF:VarName.Contains(value)\n内容\n#IFEND');
                } else if (syntax === 'for') {
                    this.insertAtCursor(editorTextarea, '$for item in collection\n${item}\n$forend');
                }
            };
        });

        // 添加节点按钮
        const addNodeBtn = document.querySelector('.add-node-btn');
        if (addNodeBtn) {
            addNodeBtn.onclick = () => {
                this.showNodeWizard(editorTextarea);
            };
        }

        // 操作类型 radio - 选中后插入 OperationType: xxx
        document.querySelectorAll('.op-type-radio').forEach(radio => {
            radio.onchange = () => {
                if (radio.checked) {
                    this.insertAtCursor(editorTextarea, `OperationType: ${radio.value}\n`);
                }
            };
        });
    },

    // 撤销栈
    undoStack: [],
    redoStack: [],
    maxUndoSize: 50,

    /**
     * 在光标位置插入文本（支持 Ctrl+Z 撤销）
     */
    insertAtCursor(textarea, text) {
        textarea.focus();
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        const oldValue = textarea.value;
        
        // 保存当前状态到撤销栈
        this.undoStack.push({
            value: oldValue,
            selectionStart: start,
            selectionEnd: end
        });
        if (this.undoStack.length > this.maxUndoSize) {
            this.undoStack.shift();
        }
        this.redoStack = []; // 清空重做栈
        
        // 插入文本
        textarea.value = oldValue.substring(0, start) + text + oldValue.substring(end);
        textarea.selectionStart = textarea.selectionEnd = start + text.length;
    },

    /**
     * 撤销操作
     */
    undo(textarea) {
        if (this.undoStack.length === 0) return;
        const state = this.undoStack.pop();
        
        // 保存当前状态到重做栈
        this.redoStack.push({
            value: textarea.value,
            selectionStart: textarea.selectionStart,
            selectionEnd: textarea.selectionEnd
        });
        
        textarea.value = state.value;
        textarea.selectionStart = state.selectionStart;
        textarea.selectionEnd = state.selectionEnd;
        textarea.focus();
    },

    /**
     * 重做操作
     */
    redo(textarea) {
        if (this.redoStack.length === 0) return;
        const state = this.redoStack.pop();
        
        this.undoStack.push({
            value: textarea.value,
            selectionStart: textarea.selectionStart,
            selectionEnd: textarea.selectionEnd
        });
        
        textarea.value = state.value;
        textarea.selectionStart = state.selectionStart;
        textarea.selectionEnd = state.selectionEnd;
        textarea.focus();
    },

    /**
     * 显示函数配置向导
     */
    showFunctionWizard(funcName, editorTextarea) {
        const func = this.builtinFunctions.find(f => f.name === funcName);
        if (!func) return;

        // 参数输入表单
        const paramsHtml = func.params.map((p, i) => `
            <div class="mb-2">
                <label class="form-label small mb-1">${p.name} <span class="text-muted">(${p.type})</span></label>
                <div class="input-group input-group-sm">
                    <input type="text" class="form-control func-param-input" id="funcParam${i}" placeholder="${p.hint}">
                    <button class="btn btn-outline-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown">
                        变量
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end" style="max-height: 200px; overflow-y: auto;">
                        ${this.envVars.map(v => `<li><a class="dropdown-item small" href="#" onclick="document.getElementById('funcParam${i}').value='\${${v.key}}'; return false;">${v.key}</a></li>`).join('')}
                    </ul>
                </div>
            </div>
        `).join('');

        // 返回值保存配置
        const returnsHtml = func.returns.map((r, i) => `
            <div class="col">
                <label class="form-label small mb-1">返回值[${i}]</label>
                <input type="text" class="form-control form-control-sm func-return-input" value="${r}" placeholder="保存到变量名">
            </div>
        `).join('');

        const wizardHtml = `
            <div class="modal fade" id="functionWizardModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header py-2">
                            <h6 class="modal-title">🔧 ${funcName}</h6>
                            <button type="button" class="btn-close btn-close-sm" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <p class="text-muted small mb-3">${func.description}</p>
                            ${paramsHtml}
                            <hr>
                            <label class="form-label small mb-1">保存返回值到变量:</label>
                            <div class="row g-2">
                                ${returnsHtml}
                            </div>
                            <hr>
                            <label class="form-label small mb-1">生成的代码:</label>
                            <pre id="generatedFuncCode" class="p-2 small rounded" style="white-space: pre-wrap; background-color: #f8f9fa; color: #212529;"></pre>
                        </div>
                        <div class="modal-footer py-2">
                            <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                            <button type="button" class="btn btn-primary btn-sm" id="insertFuncBtn">插入</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // 移除旧向导
        const oldWizard = document.getElementById('functionWizardModal');
        if (oldWizard) oldWizard.remove();

        document.body.insertAdjacentHTML('beforeend', wizardHtml);

        const wizardModalEl = document.getElementById('functionWizardModal');
        const wizardModal = new bootstrap.Modal(wizardModalEl);
        
        // 实时更新生成的代码
        const updateGeneratedCode = () => {
            const params = Array.from(document.querySelectorAll('.func-param-input'))
                .map(input => input.value || 'value')
                .join('|||');
            const returns = Array.from(document.querySelectorAll('.func-return-input'))
                .map(input => `"${input.value}"`)
                .join(',');
            const code = `${funcName}(${params})->${returns}->[0]`;
            document.getElementById('generatedFuncCode').textContent = code;
        };

        document.querySelectorAll('.func-param-input, .func-return-input').forEach(input => {
            input.addEventListener('input', updateGeneratedCode);
        });

        // 初始化显示
        setTimeout(updateGeneratedCode, 100);

        // 插入按钮
        document.getElementById('insertFuncBtn').onclick = () => {
            const code = document.getElementById('generatedFuncCode').textContent;
            this.insertAtCursor(editorTextarea, code);
            wizardModal.hide();
        };

        wizardModal.show();
        makeModalDraggable(wizardModalEl);
    },

    /**
     * 显示节点配置向导
     */
    showNodeWizard(editorTextarea) {
        const opTypesHtml = this.operationTypes.map(t => `
            <option value="${t.value}">${t.label}</option>
        `).join('');

        const wizardHtml = `
            <div class="modal fade" id="nodeWizardModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header py-2">
                            <h6 class="modal-title">➕ 添加操作节点</h6>
                            <button type="button" class="btn-close btn-close-sm" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="mb-3">
                                <label class="form-label small">节点标题</label>
                                <input type="text" class="form-control form-control-sm" id="nodeTitle" placeholder="如: 创建Service文件">
                            </div>
                            <div class="mb-3">
                                <label class="form-label small">目标文件 (TargetFilePattern)</label>
                                <div class="input-group input-group-sm">
                                    <input type="text" class="form-control" id="nodeTargetFile" placeholder="如: {ProjDir}Services/{ClassName}Service.cs">
                                    <button class="btn btn-outline-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown">变量</button>
                                    <ul class="dropdown-menu dropdown-menu-end">
                                        ${this.envVars.map(v => `<li><a class="dropdown-item small" href="#" onclick="document.getElementById('nodeTargetFile').value+='\${${v.key}}'; return false;">${v.key}</a></li>`).join('')}
                                    </ul>
                                </div>
                            </div>
                            <div class="mb-3">
                                <label class="form-label small">操作类型 (OperationType)</label>
                                <select class="form-select form-select-sm" id="nodeOpType">
                                    ${opTypesHtml}
                                </select>
                            </div>
                            <div class="mb-3" id="linePatternGroup">
                                <label class="form-label small">定位模式 (LinePattern) - 仅 Append/Prepend/Replace 需要</label>
                                <input type="text" class="form-control form-control-sm" id="nodeLinePattern" placeholder="如: using System;">
                            </div>
                            <div class="mb-3">
                                <label class="form-label small">内容 (Value)</label>
                                <textarea class="form-control form-control-sm" id="nodeValue" rows="5" placeholder="要写入的内容"></textarea>
                            </div>
                            <hr>
                            <label class="form-label small">预览:</label>
                            <pre id="generatedNodeCode" class="p-2 small rounded" style="white-space: pre-wrap; max-height: 150px; overflow-y: auto; background-color: #f8f9fa; color: #212529;"></pre>
                        </div>
                        <div class="modal-footer py-2">
                            <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                            <button type="button" class="btn btn-primary btn-sm" id="insertNodeBtn">插入</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // 移除旧向导
        const oldWizard = document.getElementById('nodeWizardModal');
        if (oldWizard) oldWizard.remove();

        document.body.insertAdjacentHTML('beforeend', wizardHtml);

        const nodeWizardModalEl = document.getElementById('nodeWizardModal');
        const wizardModal = new bootstrap.Modal(nodeWizardModalEl);

        // 操作类型变化时显示/隐藏 LinePattern
        const opTypeSelect = document.getElementById('nodeOpType');
        const linePatternGroup = document.getElementById('linePatternGroup');
        opTypeSelect.onchange = () => {
            const needLinePattern = ['Append', 'Prepend', 'Replace'].includes(opTypeSelect.value);
            linePatternGroup.style.display = needLinePattern ? 'block' : 'none';
        };

        // 实时更新预览
        const updatePreview = () => {
            const title = document.getElementById('nodeTitle').value || '节点标题';
            const targetFile = document.getElementById('nodeTargetFile').value || 'path/to/file';
            const opType = document.getElementById('nodeOpType').value;
            const linePattern = document.getElementById('nodeLinePattern').value;
            const value = document.getElementById('nodeValue').value || '内容';

            let code = `### ${title}\nTargetFilePattern: ${targetFile}\nOperationType: ${opType}`;
            if (['Append', 'Prepend', 'Replace'].includes(opType) && linePattern) {
                code += `\nLinePattern: ${linePattern}`;
            }
            code += `\nValue:\n${value}`;

            document.getElementById('generatedNodeCode').textContent = code;
        };

        document.querySelectorAll('#nodeTitle, #nodeTargetFile, #nodeOpType, #nodeLinePattern, #nodeValue').forEach(el => {
            el.addEventListener('input', updatePreview);
            el.addEventListener('change', updatePreview);
        });

        setTimeout(updatePreview, 100);

        // 插入按钮
        document.getElementById('insertNodeBtn').onclick = () => {
            const code = document.getElementById('generatedNodeCode').textContent;
            this.insertAtCursor(editorTextarea, '\n\n' + code);
            wizardModal.hide();
        };

        wizardModal.show();
        makeModalDraggable(nodeWizardModalEl);
    },

    /**
     * 预览模板解析结果
     */
    async previewTemplate(templateContent) {
        try {
            const response = await fetch('/Hosts/ResolveCommandSettting', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: this.settingId, cmdTxt: templateContent })
            });
            const result = await response.json();
            
            const previewHtml = `
                <div class="modal fade" id="previewModal" tabindex="-1">
                    <div class="modal-dialog modal-lg">
                        <div class="modal-content">
                            <div class="modal-header py-2">
                                <h6 class="modal-title">📋 解析预览</h6>
                                <button type="button" class="btn-close btn-close-sm" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <pre class="bg-light p-3 rounded" style="white-space: pre-wrap; max-height: 400px; overflow-y: auto;">${result.succeed ? result.data : result.message}</pre>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            const oldPreview = document.getElementById('previewModal');
            if (oldPreview) oldPreview.remove();

            document.body.insertAdjacentHTML('beforeend', previewHtml);
            new bootstrap.Modal(document.getElementById('previewModal')).show();
        } catch (e) {
            console.error('预览失败:', e);
            alert('预览失败: ' + e.message);
        }
    },

    /**
     * 显示自动补全菜单
     */
    showAutoCompleteMenu(textarea) {
        // 简单实现：显示变量列表
        const menuHtml = `
            <div id="autoCompleteMenu" class="dropdown-menu show" style="position: absolute; max-height: 200px; overflow-y: auto; background-color: #fff; border: 1px solid #dee2e6; z-index: 9999;">
                <h6 class="dropdown-header" style="color: #6c757d;">环境变量</h6>
                ${this.envVars.map(v => `<a class="dropdown-item small autocomplete-item" href="#" data-value="\${${v.key}}" style="color: #212529;">${v.key}</a>`).join('')}
                <div class="dropdown-divider"></div>
                <h6 class="dropdown-header" style="color: #6c757d;">内置函数</h6>
                ${this.builtinFunctions.slice(0, 5).map(f => `<a class="dropdown-item small autocomplete-item" href="#" data-value="${f.name}()" style="color: #212529;">${f.name}</a>`).join('')}
            </div>
        `;

        // 移除旧菜单
        const oldMenu = document.getElementById('autoCompleteMenu');
        if (oldMenu) oldMenu.remove();

        // 计算位置
        const rect = textarea.getBoundingClientRect();
        document.body.insertAdjacentHTML('beforeend', menuHtml);
        const menu = document.getElementById('autoCompleteMenu');
        menu.style.left = rect.left + 'px';
        menu.style.top = (rect.top + 20) + 'px';

        const items = menu.querySelectorAll('.autocomplete-item');
        let selectedIndex = 0;

        // 高亮选中项
        const highlightItem = (index) => {
            items.forEach((item, i) => {
                if (i === index) {
                    item.style.backgroundColor = '#0d6efd';
                    item.style.color = '#fff';
                    item.scrollIntoView({ block: 'nearest' });
                } else {
                    item.style.backgroundColor = '';
                    item.style.color = '#212529';
                }
            });
        };

        // 确认选中
        const confirmSelection = () => {
            if (items[selectedIndex]) {
                this.insertAtCursor(textarea, items[selectedIndex].getAttribute('data-value'));
            }
            menu.remove();
            document.removeEventListener('keydown', handleKeydown);
        };

        // 键盘事件处理
        const handleKeydown = (e) => {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                selectedIndex = (selectedIndex + 1) % items.length;
                highlightItem(selectedIndex);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                selectedIndex = (selectedIndex - 1 + items.length) % items.length;
                highlightItem(selectedIndex);
            } else if (e.key === 'Enter') {
                e.preventDefault();
                confirmSelection();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                menu.remove();
                document.removeEventListener('keydown', handleKeydown);
                textarea.focus();
            }
        };

        // 默认选中第一个
        if (items.length > 0) {
            highlightItem(0);
        }

        // 绑定键盘事件
        document.addEventListener('keydown', handleKeydown);

        // 绑定点击事件
        items.forEach((item, i) => {
            item.onclick = (e) => {
                e.preventDefault();
                selectedIndex = i;
                confirmSelection();
            };
            item.onmouseenter = () => {
                selectedIndex = i;
                highlightItem(i);
            };
        });

        // 点击其他地方关闭
        setTimeout(() => {
            document.addEventListener('click', function closeMenu(e) {
                if (!menu.contains(e.target)) {
                    menu.remove();
                    document.removeEventListener('click', closeMenu);
                    document.removeEventListener('keydown', handleKeydown);
                }
            });
        }, 100);
    },

    /**
     * 设置自动补全
     */
    setupAutoComplete() {
        if (!this.editor) return;
        
        this.editor.addEventListener('keydown', (e) => {
            if (e.altKey && e.key === '/') {
                e.preventDefault();
                this.showAutoCompleteMenu(this.editor);
            }
        });
    }
};

// 导出到全局
window.TemplateEditor = TemplateEditor;
