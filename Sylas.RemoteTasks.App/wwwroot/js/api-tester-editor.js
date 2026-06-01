/* ============================================================================
   API Tester - 编辑器(中间栏) + 响应面板(右侧栏)
   依赖: api-tester.js (state, atApi, atToast, atEscape)
   ============================================================================ */
(function ($) {
    'use strict';

    // 防御性获取全局状态 (极端情况下 api-tester.js 可能加载失败)
    const state = window.apiTesterState = window.apiTesterState || {};

    // ---------- 编辑器: 当前接口模型(内存) ----------
    let editorModel = null;
    // editorModel = { id, collectionId, baseUrl, name, method, path,
    //   params:[KvRow], headers:[KvRow], body, bodyType,
    //   auth:Auth, extractors:[], validators:[], overrideGlobalValidators:false }

    const METHOD_OPTIONS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS'];
    const BODY_TYPES = [
        { v: 'none', t: 'none' },
        { v: 'json', t: 'json (application/json)' },
        { v: 'form-urlencoded', t: 'x-www-form-urlencoded' },
        { v: 'form-data', t: 'multipart/form-data' },
        { v: 'text', t: 'text/plain' },
        { v: 'xml', t: 'text/xml' }
    ];
    const TABS = [
        { id: 'params', t: 'Params' },
        { id: 'headers', t: 'Headers' },
        { id: 'body', t: 'Body' },
        { id: 'auth', t: 'Auth' },
        { id: 'extract', t: '(x) 提取' },
        { id: 'validate', t: '校验' }
    ];
    let activeTab = 'params';

    // ---------- 加载接口到编辑器 ----------
    async function loadEndpoint(id) {
        try {
            // 调用统一封装: 自动 token/401/loading、code非0自动 toast 提示
            const data = await window.atApi.getEndpoint(id);
            if (!data) return;
            const ep = data.endpoint;
            const col = data.collection;
            editorModel = {
                id: ep.id,
                collectionId: ep.collectionId,
                tag: ep.tag || 'MANUAL',
                orderNo: ep.orderNo || 0,
                baseUrl: (col && col.baseUrl) || '',
                name: ep.name || '',
                method: ep.method || 'GET',
                path: ep.path || '',
                params: normalizeKvRows(parseJsonSafe(ep.params, [])),
                headers: normalizeKvRows(parseJsonSafe(ep.headers, [])),
                body: ep.body || '',
                bodyType: ep.bodyType || 'none',
                auth: normalizeAuth(parseJsonSafe(ep.auth, { inherit: true, type: 'none' })),
                extractors: normalizeExtractors(parseJsonSafe(ep.extractors, [])),
                validators: normalizeValidators(parseJsonSafe(ep.validators, [])),
                overrideGlobalValidators: !!ep.overrideGlobalValidators
            };
            renderEditor();
        } catch (e) {
            window.atToast('加载接口失败: ' + (e.message || e));
        }
    }

    function parseJsonSafe(s, fallback) {
        if (s == null || s === '') return fallback;
        if (typeof s !== 'string') return s;
        try { return JSON.parse(s); } catch { return fallback; }
    }

    // 列表型字段归一化: 历史数据可能是 PascalCase (Name/Value/Enabled/Description)
    // 新数据是 camelCase (name/value/enabled/description), 这里统一转为前端识别的 camelCase
    function normalizeKvRows(arr) {
        if (!Array.isArray(arr)) return [];
        return arr.map(r => ({
            enabled: r && (r.enabled !== undefined ? r.enabled : (r.Enabled !== undefined ? r.Enabled : true)),
            name: (r && (r.name ?? r.Name)) || '',
            value: (r && (r.value ?? r.Value)) || '',
            description: (r && (r.description ?? r.Description)) || ''
        }));
    }
    // Extractor 行归一化 (varName / dataPath / field / filter)
    function normalizeExtractors(arr) {
        if (!Array.isArray(arr)) return [];
        return arr.map(r => {
            r = r || {};
            const f = r.filter || r.Filter || {};
            return {
                varName: r.varName ?? r.VarName ?? '',
                dataPath: r.dataPath ?? r.DataPath ?? '',
                field: r.field ?? r.Field ?? '',
                filter: {
                    fieldName: f.fieldName ?? f.FieldName ?? '',
                    matchValue: f.matchValue ?? f.MatchValue ?? ''
                }
            };
        });
    }
    // Validator 行归一化 (field / op / expected)
    function normalizeValidators(arr) {
        if (!Array.isArray(arr)) return [];
        return arr.map(r => ({
            field: (r && (r.field ?? r.Field)) || '',
            op: (r && (r.op ?? r.Op)) || 'eq',
            expected: (r && (r.expected ?? r.Expected)) ?? ''
        }));
    }
    // Auth 对象归一化
    function normalizeAuth(o) {
        o = o || {};
        return {
            inherit: o.inherit ?? o.Inherit ?? true,
            type: o.type ?? o.Type ?? 'none',
            token: o.token ?? o.Token ?? '',
            username: o.username ?? o.Username ?? '',
            password: o.password ?? o.Password ?? '',
            apiKeyName: o.apiKeyName ?? o.ApiKeyName ?? '',
            apiKeyValue: o.apiKeyValue ?? o.ApiKeyValue ?? '',
            apiKeyIn: o.apiKeyIn ?? o.ApiKeyIn ?? 'header',
            customHeaders: normalizeKvRows(o.customHeaders ?? o.CustomHeaders ?? [])
        };
    }

    // ---------- 渲染编辑器 ----------
    function renderEditor() {
        const $e = $('#atEditor').empty();
        if (!editorModel) {
            $e.html(
                '<div class="at-empty-state">' +
                '<div class="at-empty-icon">🔌</div>' +
                '<div class="at-empty-title">选择一个接口开始测试</div>' +
                '<div class="at-empty-desc">从左侧列表选择接口, 或导入 Swagger 文档</div>' +
                '</div>'
            );
            return;
        }
        const m = editorModel;

        // 顶部 method + url + 发送
        const methodOptions = METHOD_OPTIONS
            .map(x => '<option value="' + x + '"' + (x === m.method ? ' selected' : '') + '>' + x + '</option>')
            .join('');
        const $top = $(
            '<div class="at-editor-top">' +
            '<select class="at-method-select at-method-' + m.method + '" id="atEditorMethod">' + methodOptions + '</select>' +
            '<input type="text" class="at-url-input" id="atEditorUrl" />' +
            '<button class="btn at-btn at-btn-primary" id="atSendBtn">发送</button>' +
            '<button class="btn at-btn" id="atSaveBtn" title="保存当前修改(快捷键 Ctrl+S)">保存接口</button>' +
            '</div>'
        );
        const fullUrl = (m.baseUrl || '') + (m.path || '');
        $top.find('#atEditorUrl').val(fullUrl);
        $e.append($top);

        // 接口名称行
        $e.append('<div class="at-editor-name"><input type="text" id="atEditorName" placeholder="接口名称" /></div>');
        $('#atEditorName', $e).val(m.name);

        // Tabs 头
        const $tabsHead = $('<div class="at-tabs-head"></div>');
        TABS.forEach(function (t) {
            const count = countOfTab(t.id);
            const badge = count > 0 ? ' <span class="at-tab-badge">' + count + '</span>' : '';
            $tabsHead.append(
                '<div class="at-tab ' + (t.id === activeTab ? 'active' : '') + '" data-tab="' + t.id + '">' +
                t.t + badge + '</div>'
            );
        });
        $e.append($tabsHead);

        // Tab 内容容器
        const $body = $('<div class="at-tabs-body" id="atTabsBody"></div>');
        $e.append($body);
        renderActiveTab();

        // 保存按钮已搼到顶部 (与发送并排), 底部 footer 保留为空占位
    }

    function countOfTab(tabId) {
        if (!editorModel) return 0;
        switch (tabId) {
            case 'params': return (editorModel.params || []).filter(p => p.enabled !== false && p.name).length;
            case 'headers': return (editorModel.headers || []).filter(h => h.enabled !== false && h.name).length;
            case 'body': return editorModel.bodyType !== 'none' ? 1 : 0;
            case 'auth': return (editorModel.auth && !editorModel.auth.inherit && editorModel.auth.type !== 'none') ? 1 : 0;
            case 'extract': return (editorModel.extractors || []).length;
            case 'validate': return (editorModel.validators || []).length;
        }
        return 0;
    }

    function renderActiveTab() {
        const $b = $('#atTabsBody').empty();
        switch (activeTab) {
            case 'params': renderKvTab($b, 'params'); break;
            case 'headers': renderKvTab($b, 'headers'); break;
            case 'body': renderBodyTab($b); break;
            case 'auth': renderAuthTab($b); break;
            case 'extract': renderExtractTab($b); break;
            case 'validate': renderValidateTab($b); break;
        }
    }

    function renderKvTab($b, key) {
        const list = editorModel[key] || (editorModel[key] = []);
        const $tbl = $(
            '<table class="at-kv-table">' +
            '<thead><tr><th style="width:30px"></th><th>名称</th><th>值</th><th>描述</th><th style="width:30px"></th></tr></thead>' +
            '<tbody></tbody></table>'
        );
        list.forEach(function (row, idx) {
            const enabled = row.enabled !== false;
            $tbl.find('tbody').append(
                '<tr data-key="' + key + '" data-idx="' + idx + '">' +
                '<td><input type="checkbox" class="kv-enabled" ' + (enabled ? 'checked' : '') + ' /></td>' +
                '<td><input type="text" class="kv-name" value="' + window.atEscape(row.name || '') + '" /></td>' +
                '<td><input type="text" class="kv-value" value="' + window.atEscape(row.value || '') + '" /></td>' +
                '<td><input type="text" class="kv-desc" value="' + window.atEscape(row.description || '') + '" /></td>' +
                '<td><button class="at-icon-btn kv-del">×</button></td>' +
                '</tr>'
            );
        });
        $b.append($tbl);
        $b.append('<button class="btn at-btn at-add-row" data-key="' + key + '">+ 添加</button>');
    }

    function renderBodyTab($b) {
        const opts = BODY_TYPES.map(t =>
            '<option value="' + t.v + '"' + (t.v === editorModel.bodyType ? ' selected' : '') + '>' + t.t + '</option>'
        ).join('');
        const $row = $(
            '<div class="at-body-toolbar">' +
            '<select id="atBodyType">' + opts + '</select>' +
            '<button class="btn at-btn" id="atBodyFormat" ' + (editorModel.bodyType === 'json' ? '' : 'disabled') + '>格式化</button>' +
            '</div>'
        );
        $b.append($row);
        if (editorModel.bodyType === 'none') {
            $b.append('<div class="at-empty-tip">该请求不带 body</div>');
            return;
        }
        $b.append('<textarea class="at-body-area" id="atBodyArea"></textarea>');
        $('#atBodyArea').val(editorModel.body || '');
    }

    function renderAuthTab($b) {
        const a = editorModel.auth || (editorModel.auth = { inherit: true, type: 'none' });
        $b.append(
            '<div class="at-auth-row">' +
            '<label><input type="checkbox" id="atAuthInherit" ' + (a.inherit ? 'checked' : '') + ' /> 继承全局授权</label>' +
            '</div>'
        );
        if (a.inherit) {
            $b.append('<div class="at-empty-tip">将使用集合的全局 Auth(Task 6 提供配置入口)</div>');
            return;
        }
        const types = ['none', 'bearer', 'basic', 'apikey', 'custom'];
        const opts = types.map(x => '<option value="' + x + '"' + (x === a.type ? ' selected' : '') + '>' + x + '</option>').join('');
        $b.append('<div class="at-auth-row"><label>类型 </label><select id="atAuthType">' + opts + '</select></div>');

        if (a.type === 'bearer') {
            $b.append('<div class="at-auth-row"><label>Token </label><input type="text" id="atAuthToken" /></div>');
            $('#atAuthToken').val(a.token || '');
        } else if (a.type === 'basic') {
            $b.append('<div class="at-auth-row"><label>Username </label><input type="text" id="atAuthUser" /></div>');
            $b.append('<div class="at-auth-row"><label>Password </label><input type="text" id="atAuthPass" /></div>');
            $('#atAuthUser').val(a.username || '');
            $('#atAuthPass').val(a.password || '');
        } else if (a.type === 'apikey') {
            $b.append('<div class="at-auth-row"><label>Key 名 </label><input type="text" id="atAuthKeyName" /></div>');
            $b.append('<div class="at-auth-row"><label>Key 值 </label><input type="text" id="atAuthKeyValue" /></div>');
            $b.append('<div class="at-auth-row"><label>位置 </label>' +
                '<select id="atAuthKeyIn">' +
                '<option value="header"' + (a.keyIn === 'header' ? ' selected' : '') + '>header</option>' +
                '<option value="query"' + (a.keyIn === 'query' ? ' selected' : '') + '>query</option>' +
                '</select></div>');
            $('#atAuthKeyName').val(a.keyName || '');
            $('#atAuthKeyValue').val(a.keyValue || '');
        } else if (a.type === 'custom') {
            $b.append('<div class="at-auth-row">自定义 Header (可多个, 支持 <code>{{var}}</code> 模板)</div>');
            const list = a.customHeaders || (a.customHeaders = []);
            const $tbl = $(
                '<table class="at-kv-table at-auth-custom-table">' +
                '<thead><tr>' +
                '<th style="width:30px"></th>' +
                '<th>Header 名</th>' +
                '<th>Value</th>' +
                '<th>描述</th>' +
                '<th style="width:30px"></th>' +
                '</tr></thead><tbody></tbody></table>'
            );
            list.forEach(function (row, idx) {
                const enabled = row.enabled !== false;
                $tbl.find('tbody').append(
                    '<tr data-idx="' + idx + '">' +
                    '<td><input type="checkbox" class="ah-enabled" ' + (enabled ? 'checked' : '') + ' /></td>' +
                    '<td><input type="text" class="ah-name" value="' + window.atEscape(row.name || '') + '" /></td>' +
                    '<td><input type="text" class="ah-value" value="' + window.atEscape(row.value || '') + '" /></td>' +
                    '<td><input type="text" class="ah-desc" value="' + window.atEscape(row.description || '') + '" /></td>' +
                    '<td><button class="at-icon-btn ah-del">×</button></td>' +
                    '</tr>'
                );
            });
            $b.append($tbl);
            $b.append('<button class="btn at-btn at-add-auth-header">+ 添加 Header</button>');
        }
    }

    function syncAuthCustomFromUI() {
        const a = editorModel.auth || (editorModel.auth = { inherit: true, type: 'none' });
        const arr = [];
        $('#atTabsBody .at-auth-custom-table tbody tr').each(function () {
            arr.push({
                enabled: $(this).find('.ah-enabled').is(':checked'),
                name: $(this).find('.ah-name').val() || '',
                value: $(this).find('.ah-value').val() || '',
                description: $(this).find('.ah-desc').val() || ''
            });
        });
        a.customHeaders = arr;
    }

    function renderExtractTab($b) {
        const list = editorModel.extractors || (editorModel.extractors = []);
        const $tbl = $(
            '<table class="at-kv-table at-extract-table">' +
            '<thead><tr>' +
            '<th>变量名</th>' +
            '<th>数据路径</th>' +
            '<th>提取字段</th>' +
            '<th>过滤字段</th>' +
            '<th>=</th>' +
            '<th>匹配值</th>' +
            '<th style="width:30px"></th>' +
            '</tr></thead><tbody></tbody></table>'
        );
        list.forEach(function (row, idx) {
            const f = row.filter || {};
            $tbl.find('tbody').append(
                '<tr data-idx="' + idx + '">' +
                '<td><input type="text" class="ex-var" value="' + window.atEscape(row.varName || '') + '" placeholder="newId" /></td>' +
                '<td><input type="text" class="ex-path" value="' + window.atEscape(row.dataPath || '') + '" placeholder="data.list" /></td>' +
                '<td><input type="text" class="ex-field" value="' + window.atEscape(row.field || '') + '" placeholder="id" /></td>' +
                '<td><input type="text" class="ex-fname" value="' + window.atEscape(f.fieldName || '') + '" placeholder="name" /></td>' +
                '<td style="text-align:center;color:#9ca3af">=</td>' +
                '<td><input type="text" class="ex-fval" value="' + window.atEscape(f.matchValue || '') + '" placeholder="TEST 或 {{var}}" /></td>' +
                '<td><button class="at-icon-btn ex-del">×</button></td>' +
                '</tr>'
            );
        });
        $b.append($tbl);
        $b.append('<button class="btn at-btn at-add-extract">+ 添加提取</button>');
        $b.append('<div class="at-extract-tip">提取结果将自动写入当前激活环境的变量, 后续接口可用 <code>{{变量名}}</code> 引用</div>');
    }

    function syncExtractorsFromUI() {
        const arr = [];
        $('#atTabsBody .at-extract-table tbody tr').each(function () {
            const fname = $(this).find('.ex-fname').val() || '';
            const fval = $(this).find('.ex-fval').val() || '';
            const item = {
                varName: $(this).find('.ex-var').val() || '',
                dataPath: $(this).find('.ex-path').val() || '',
                field: $(this).find('.ex-field').val() || ''
            };
            if (fname || fval) {
                item.filter = { fieldName: fname, matchValue: fval };
            }
            arr.push(item);
        });
        editorModel.extractors = arr;
    }

    function renderValidateTab($b) {
        const m = editorModel;
        const list = m.validators || (m.validators = []);
        $b.append(
            '<div class="at-validate-toolbar">' +
            '<label><input type="checkbox" id="atValOverride" ' + (m.overrideGlobalValidators ? 'checked' : '') + ' /> 覆盖全局规则(不勾选则与集合全局校验合并执行)</label>' +
            '</div>'
        );
        const OPS = [
            { v: 'eq', t: '等于' },
            { v: 'ne', t: '不等于' },
            { v: 'gt', t: '大于' },
            { v: 'lt', t: '小于' },
            { v: 'ge', t: '大于等于' },
            { v: 'le', t: '小于等于' },
            { v: 'contains', t: '包含' },
            { v: 'exists', t: '存在' }
        ];
        const $tbl = $(
            '<table class="at-kv-table at-validate-table">' +
            '<thead><tr>' +
            '<th>字段(JSON 路径/status/headers.X)</th>' +
            '<th style="width:120px">条件</th>' +
            '<th>期望值</th>' +
            '<th style="width:30px"></th>' +
            '</tr></thead><tbody></tbody></table>'
        );
        list.forEach(function (row, idx) {
            const opts = OPS.map(o => '<option value="' + o.v + '"' + (o.v === row.op ? ' selected' : '') + '>' + o.t + '</option>').join('');
            $tbl.find('tbody').append(
                '<tr data-idx="' + idx + '">' +
                '<td><input type="text" class="vd-field" value="' + window.atEscape(row.field || '') + '" placeholder="data.code 或 status" /></td>' +
                '<td><select class="vd-op">' + opts + '</select></td>' +
                '<td><input type="text" class="vd-expected" value="' + window.atEscape(row.expected || '') + '" placeholder="期望值, exists 可为空" /></td>' +
                '<td><button class="at-icon-btn vd-del">×</button></td>' +
                '</tr>'
            );
        });
        $b.append($tbl);
        $b.append('<button class="btn at-btn at-add-validate">+ 添加规则</button>');
    }

    function syncValidatorsFromUI() {
        const arr = [];
        $('#atTabsBody .at-validate-table tbody tr').each(function () {
            arr.push({
                field: $(this).find('.vd-field').val() || '',
                op: $(this).find('.vd-op').val() || 'eq',
                expected: $(this).find('.vd-expected').val() || ''
            });
        });
        editorModel.validators = arr;
        editorModel.overrideGlobalValidators = $('#atValOverride').is(':checked');
    }

    // ---------- 同步表单到模型 ----------
    function syncFromUI() {
        if (!editorModel) return;
        const m = editorModel;
        m.method = $('#atEditorMethod').val() || m.method;
        const url = $('#atEditorUrl').val() || '';
        // url 拆分为 baseUrl + path: 若以 baseUrl 开头则保留 baseUrl, 否则把整段当作 path
        if (m.baseUrl && url.indexOf(m.baseUrl) === 0) {
            m.path = url.substring(m.baseUrl.length);
        } else {
            m.path = url;
        }
        m.name = $('#atEditorName').val() || m.name;

        if (activeTab === 'params' || activeTab === 'headers') {
            const arr = [];
            $('#atTabsBody tbody tr').each(function () {
                arr.push({
                    enabled: $(this).find('.kv-enabled').is(':checked'),
                    name: $(this).find('.kv-name').val() || '',
                    value: $(this).find('.kv-value').val() || '',
                    description: $(this).find('.kv-desc').val() || ''
                });
            });
            m[activeTab] = arr;
        } else if (activeTab === 'body') {
            m.bodyType = $('#atBodyType').val() || m.bodyType;
            m.body = $('#atBodyArea').val() || '';
        } else if (activeTab === 'auth') {
            const a = m.auth = m.auth || { inherit: true, type: 'none' };
            a.inherit = $('#atAuthInherit').is(':checked');
            a.type = $('#atAuthType').val() || a.type;
            a.token = $('#atAuthToken').val() || '';
            a.username = $('#atAuthUser').val() || '';
            a.password = $('#atAuthPass').val() || '';
            a.keyName = $('#atAuthKeyName').val() || '';
            a.keyValue = $('#atAuthKeyValue').val() || '';
            a.keyIn = $('#atAuthKeyIn').val() || 'header';
            if (a.type === 'custom') syncAuthCustomFromUI();
        } else if (activeTab === 'extract') {
            syncExtractorsFromUI();
        } else if (activeTab === 'validate') {
            syncValidatorsFromUI();
        }
    }

    // ---------- 发送请求 ----------
    async function doSend() {
        if (!editorModel) return;
        syncFromUI();
        const m = editorModel;
        const finalUrl = $('#atEditorUrl').val() || '';
        const dto = {
            endpointId: m.id,
            collectionId: m.collectionId,
            method: m.method,
            url: finalUrl,
            params: m.params || [],
            headers: m.headers || [],
            body: m.body || '',
            bodyType: m.bodyType || 'none',
            auth: m.auth || { inherit: true, type: 'none' },
            extractors: m.extractors || [],
            validators: m.validators || [],
            overrideGlobalValidators: !!m.overrideGlobalValidators
        };
        $('#atSendBtn').prop('disabled', true).text('发送中...');
        try {
            const data = await window.atApi.sendRequest(dto);
            renderResponse(data);
        } catch (e) {
            window.atToast('发送失败: ' + e.message);
        } finally {
            $('#atSendBtn').prop('disabled', false).text('发送');
        }
    }

    // ---------- 保存接口 ----------
    async function doSave() {
        if (!editorModel) return;
        syncFromUI();
        const m = editorModel;
        const dto = {
            id: m.id,
            collectionId: m.collectionId,
            tag: m.tag || 'MANUAL',
            name: m.name,
            method: m.method,
            path: m.path,
            params: JSON.stringify(m.params || []),
            headers: JSON.stringify(m.headers || []),
            body: m.body || '',
            bodyType: m.bodyType || 'none',
            auth: JSON.stringify(m.auth || { inherit: true, type: 'none' }),
            extractors: JSON.stringify(m.extractors || []),
            validators: JSON.stringify(m.validators || []),
            overrideGlobalValidators: !!m.overrideGlobalValidators,
            orderNo: m.orderNo || 0
        };
        const $btn = $('#atSaveBtn');
        $btn.prop('disabled', true).text('保存中...');
        try {
            // 调用统一封装: 后端参数名是 PascalCase, dto 字段名会被默认 JSON 序列化为 camelCase, ASP.NET binder 允许
            await window.atApi.saveEndpoint(dto);
            window.atToast('已保存');
            // 保存后刷新左侧接口名称可能变化
            if (window.apiTesterState && window.atRenderEndpoints) {
                const list = window.apiTesterState.endpoints || [];
                const ep = list.find(x => x.id === m.id);
                if (ep) {
                    ep.name = m.name;
                    ep.method = m.method;
                    ep.path = m.path;
                    window.atRenderEndpoints();
                }
            }
        } catch (e) {
            // _post 已统一 toast, 这里仅补上上下文说明
            window.atToast('保存失败: ' + (e.message || e));
        } finally {
            $btn.prop('disabled', false).text('保存接口');
        }
    }

    // ---------- 响应面板 ----------
    function renderResponse(r) {
        const $r = $('#atResponse').empty();
        if (!r) {
            $r.html('<div class="at-empty-state"><div class="at-empty-title">无响应数据</div></div>');
            return;
        }
        const status = r.status || 0;
        const statusClass = status >= 500 ? 'at-status-5xx'
            : status >= 400 ? 'at-status-4xx'
                : status >= 300 ? 'at-status-3xx'
                    : status >= 200 ? 'at-status-2xx' : 'at-status-err';

        $r.append(
            '<div class="at-resp-status-bar">' +
            '<span class="at-resp-status ' + statusClass + '">' + status + ' ' + window.atEscape(r.statusText || '') + '</span>' +
            '<span class="at-resp-meta">' + (r.durationMs || 0) + ' ms</span>' +
            '<span class="at-resp-meta">' + (r.size || 0) + ' B</span>' +
            (r.error ? '<span class="at-resp-error">' + window.atEscape(r.error) + '</span>' : '') +
            '</div>'
        );

        // 提取/校验结果(Task 4/5 数据存在时显示)
        if (r.extractedVars && r.extractedVars.length) {
            const items = r.extractedVars
                .map(v => '<div>' + window.atEscape(v.name) + ' = ' + window.atEscape(v.value) + '</div>').join('');
            $r.append('<div class="at-resp-section"><div class="at-resp-section-title">提取变量</div>' + items + '</div>');
        }
        if (r.validatorResults && r.validatorResults.length) {
            const items = r.validatorResults.map(v => {
                const ico = v.passed ? '✓' : '✗';
                const cls = v.passed ? 'at-vr-pass' : 'at-vr-fail';
                return '<div class="' + cls + '">' + ico + ' ' + window.atEscape(v.field) + ' ' +
                    window.atEscape(v.op) + ' ' + window.atEscape(v.expected) +
                    ' (实际: ' + window.atEscape(v.actual) + ')</div>';
            }).join('');
            $r.append('<div class="at-resp-section"><div class="at-resp-section-title">校验结果</div>' + items + '</div>');
        }

        // 子 Tab: Body / Headers
        $r.append(
            '<div class="at-resp-tabs-head">' +
            '<div class="at-resp-tab active" data-rtab="body">响应体</div>' +
            '<div class="at-resp-tab" data-rtab="headers">响应头</div>' +
            '</div>' +
            '<div class="at-resp-tabs-body" id="atRespTabsBody"></div>'
        );
        // 缓存数据
        $r.data('respData', r);
        renderRespTab('body');
    }

    function renderRespTab(tab) {
        const $b = $('#atRespTabsBody').empty();
        const r = $('#atResponse').data('respData') || {};
        if (tab === 'body') {
            const body = r.body || '';
            let pretty = body;
            try {
                if (body && body.trim().startsWith('{') || body.trim().startsWith('[')) {
                    pretty = JSON.stringify(JSON.parse(body), null, 2);
                }
            } catch { /* keep raw */ }
            $b.append('<pre class="at-resp-pre">' + window.atEscape(pretty) + '</pre>');
        } else {
            const headers = r.headers || {};
            const rows = Object.keys(headers)
                .map(k => '<tr><td>' + window.atEscape(k) + '</td><td>' + window.atEscape(headers[k]) + '</td></tr>').join('');
            $b.append('<table class="at-resp-headers"><tbody>' + rows + '</tbody></table>');
        }
    }

    // ---------- 事件绑定 ----------
    function bindEvents() {
        // Tab 切换
        $(document).on('click', '.at-tab', function () {
            syncFromUI();
            activeTab = $(this).data('tab');
            $('.at-tab').removeClass('active');
            $(this).addClass('active');
            renderActiveTab();
            // 刷新计数
            $('.at-tab').each(function () {
                const id = $(this).data('tab');
                const t = TABS.find(x => x.id === id);
                const c = countOfTab(id);
                $(this).html(t.t + (c > 0 ? ' <span class="at-tab-badge">' + c + '</span>' : ''));
            });
        });

        // KV 行操作
        $(document).on('click', '.at-add-row', function () {
            const key = $(this).data('key');
            editorModel[key] = editorModel[key] || [];
            editorModel[key].push({ enabled: true, name: '', value: '', description: '' });
            renderActiveTab();
        });
        $(document).on('click', '.kv-del', function () {
            const $tr = $(this).closest('tr');
            const key = $tr.data('key');
            const idx = parseInt($tr.data('idx'), 10);
            editorModel[key].splice(idx, 1);
            renderActiveTab();
        });

        // Body 类型切换
        $(document).on('change', '#atBodyType', function () {
            syncFromUI();
            editorModel.bodyType = $(this).val();
            renderActiveTab();
        });
        $(document).on('click', '#atBodyFormat', function () {
            const v = $('#atBodyArea').val();
            try {
                $('#atBodyArea').val(JSON.stringify(JSON.parse(v), null, 2));
            } catch { window.atToast('Body 不是有效 JSON'); }
        });

        // Auth 切换
        $(document).on('change', '#atAuthInherit, #atAuthType', function () {
            syncFromUI();
            renderActiveTab();
        });

        // 提取行操作
        $(document).on('click', '.at-add-extract', function () {
            syncExtractorsFromUI();
            (editorModel.extractors = editorModel.extractors || []).push({
                varName: '', dataPath: '', field: '', filter: { fieldName: '', matchValue: '' }
            });
            renderActiveTab();
        });
        $(document).on('click', '.ex-del', function () {
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            syncExtractorsFromUI();
            editorModel.extractors.splice(idx, 1);
            renderActiveTab();
        });

        // Auth 自定义 Header 行操作
        $(document).on('click', '.at-add-auth-header', function () {
            syncAuthCustomFromUI();
            const a = editorModel.auth = editorModel.auth || { inherit: false, type: 'custom', customHeaders: [] };
            a.customHeaders = a.customHeaders || [];
            a.customHeaders.push({ enabled: true, name: '', value: '', description: '' });
            renderActiveTab();
        });
        $(document).on('click', '.ah-del', function () {
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            syncAuthCustomFromUI();
            editorModel.auth.customHeaders.splice(idx, 1);
            renderActiveTab();
        });

        // 校验规则行操作
        $(document).on('click', '.at-add-validate', function () {
            syncValidatorsFromUI();
            (editorModel.validators = editorModel.validators || []).push({ field: '', op: 'eq', expected: '' });
            renderActiveTab();
        });
        $(document).on('click', '.vd-del', function () {
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            syncValidatorsFromUI();
            editorModel.validators.splice(idx, 1);
            renderActiveTab();
        });

        // Method 颜色联动
        $(document).on('change', '#atEditorMethod', function () {
            const v = $(this).val();
            $(this).removeClass(function (_, c) {
                return (c.match(/at-method-\S+/g) || []).join(' ');
            }).addClass('at-method-' + v);
        });

        // 顶部按钮
        $(document).on('click', '#atSendBtn', doSend);
        $(document).on('click', '#atSaveBtn', doSave);
        
        // Ctrl+S 快捷保存 (仅在编辑器可见时)
        $(document).on('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
                if (editorModel && $('#atSaveBtn').length) {
                    e.preventDefault();
                    doSave();
                }
            }
        });

        // 响应子 Tab
        $(document).on('click', '.at-resp-tab', function () {
            $('.at-resp-tab').removeClass('active');
            $(this).addClass('active');
            renderRespTab($(this).data('rtab'));
        });
    }

    // ---------- 暴露给 sidebar 调用 ----------
    window.atEditor = {
        loadEndpoint: loadEndpoint
    };

    // ---------- 扩展 atApi (历史遗留: sendRequest 已在 api-tester.js 中提供，这里仅保留事件绑定) ----------
    $(function () {
        bindEvents();
    });

})(jQuery);
