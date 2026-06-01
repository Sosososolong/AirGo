/* ============================================================================
   API Tester - Task 6: 批量测试 + 全局授权 + 全局校验 + 新建接口
   依赖: api-tester.js (state, atApi, atToast, atEscape)
   ============================================================================ */
(function ($) {
    'use strict';

    // 防御性获取全局状态 (极端情况下 api-tester.js 可能加载失败)
    const state = window.apiTesterState = window.apiTesterState || {};

    // 选中接口集合: { collectionId: Set<endpointId> }, 当前演示存于 state
    state.batchSelected = state.batchSelected || {};

    // ---------- 选中状态持久化 (localStorage, 同浏览器跨会话刷新不丢) ----------
    // key 设计: apiTester:batchSelected
    // value: { "<cid>": [endpointId,...] } —— Set 不能直接序列化, 转 Array
    const STORAGE_KEY = 'apiTester:batchSelected';
    function loadSelected() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return;
            const obj = JSON.parse(raw);
            if (!obj || typeof obj !== 'object') return;
            Object.keys(obj).forEach(function (cid) {
                const arr = obj[cid];
                if (Array.isArray(arr) && arr.length) {
                    state.batchSelected[cid] = new Set(arr);
                }
            });
        } catch (e) { /* 存储损坏则放弃 */ }
    }
    function saveSelected() {
        try {
            const obj = {};
            Object.keys(state.batchSelected).forEach(function (cid) {
                const set = state.batchSelected[cid];
                if (set && set.size > 0) obj[cid] = Array.from(set);
            });
            // 空集合 → 删除 key, 保持存储干净
            if (Object.keys(obj).length === 0) localStorage.removeItem(STORAGE_KEY);
            else localStorage.setItem(STORAGE_KEY, JSON.stringify(obj));
        } catch (e) { /* 超限或被禁用则放弃 */ }
    }
    // 启动时从 localStorage 恢复
    loadSelected();

    function escape(s) { return window.atEscape ? window.atEscape(s) : s; }
    function toast(s) { return window.atToast ? window.atToast(s) : console.warn(s); }

    // ---------- API 扩展 ----------
    // 惰性解析: 真正调用时才取 window.atApi._post, 避免加载时序竞态
    function _post(url, data) {
        if (window.atApi && window.atApi._post) return window.atApi._post(url, data);
        console.warn('[api-tester-batch] atApi._post 未就绪, 请刷新页面');
        return $.Deferred().reject(new Error('atApi not ready')).promise();
    }
    window.atApi = window.atApi || {};
    Object.assign(window.atApi, {
        saveCollection: (dto) => _post('/ApiTester/SaveCollection', dto),
        batchSend: (dto) => _post('/ApiTester/BatchSend', dto)
        // saveEndpoint 已在 api-tester.js 中提供, 不重复定义
    });

    // ---------- 批量计数徽标 ----------
    function activeSelectedSet() {
        const cid = state.activeCollectionId;
        if (!cid) return null;
        if (!state.batchSelected[cid]) state.batchSelected[cid] = new Set();
        return state.batchSelected[cid];
    }

    function refreshBatchBadge() {
        const set = activeSelectedSet();
        $('#batchCount').text(set ? set.size : 0);
        $('#atBatchSelectedInline').text(set ? set.size : 0);
    }

    // 获取当前搜索过滤后的可见接口 (与 renderEndpoints 中 filtered 的逻辑保持一致)
    function getVisibleEndpoints() {
        const list = state.endpoints || [];
        const kw = (state.searchKeyword || '').toLowerCase();
        if (!kw) return list;
        return list.filter(function (ep) {
            return (ep.name || '').toLowerCase().indexOf(kw) >= 0
                || (ep.path || '').toLowerCase().indexOf(kw) >= 0
                || (ep.method || '').toLowerCase().indexOf(kw) >= 0;
        });
    }

    // 刷新工具栏【全选可见】复选框三态 + 已选计数 (供 api-tester.js renderEndpoints 调用)
    window.atRefreshBatchToolbar = function () {
        const set = activeSelectedSet();
        // 清理孤儿 id: 当前集合下接口被删/不存在时, 从 set 中移除 (避免持久化进垃圾数据)
        if (set && Array.isArray(state.endpoints) && state.endpoints.length > 0) {
            const validIds = new Set(state.endpoints.map(function (ep) { return ep.id; }));
            let mutated = false;
            Array.from(set).forEach(function (id) {
                if (!validIds.has(id)) { set.delete(id); mutated = true; }
            });
            if (mutated) { /* 下面 saveSelected() 会同步落地 */ }
        }
        const visible = getVisibleEndpoints();
        let visibleSelected = 0;
        if (set) visible.forEach(function (ep) { if (set.has(ep.id)) visibleSelected++; });
        const $all = $('#atBatchAll');
        if ($all.length) {
            const allChecked = visible.length > 0 && visibleSelected === visible.length;
            const someChecked = visibleSelected > 0 && visibleSelected < visible.length;
            $all.prop('checked', allChecked);
            $all.prop('indeterminate', someChecked);
        }
        refreshBatchBadge();
        // 所有选中变更路径都会经过 atRefreshBatchToolbar (变更后重渲染 + 初渲染均会调用),
        // 在此一个点集中持久化到 localStorage, 避免在各个事件动作点重复写
        saveSelected();
    };

    // ---------- 复选框联动(注入到 sidebar 已渲染的列表) ----------
    function bindCheckboxes() {
        // 切换接口选中
        $(document).on('change', '.at-endpoint-checkbox', function (e) {
            e.stopPropagation();
            const $item = $(this).closest('.at-endpoint-item');
            const id = parseInt($item.data('id'), 10) || 0;
            const set = activeSelectedSet();
            if (!set) return;
            if (this.checked) set.add(id); else set.delete(id);
            // 重渲染以同步 Tag 分组三态 / Tag count 额外显示 / 全选复选框三态
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
            else atRefreshBatchToolbar();
        });

        // Tag 分组复选框: 全选/取消该分组下所有接口 (可见范围)
        // 先阻止 click 冒泡到 .at-tag-header 触发 toggle-tag 折叠
        $(document).on('click', '.at-tag-checkbox', function (e) { e.stopPropagation(); });
        $(document).on('change', '.at-tag-checkbox', function (e) {
            e.stopPropagation();
            const set = activeSelectedSet();
            if (!set) return;
            const tag = $(this).closest('.at-tag-group').data('tag');
            const visible = getVisibleEndpoints();
            const targets = visible.filter(function (ep) { return (ep.tag || 'default') === tag; });
            const checked = this.checked;
            targets.forEach(function (ep) { if (checked) set.add(ep.id); else set.delete(ep.id); });
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
            else atRefreshBatchToolbar();
        });

        // 工具栏: 全选可见
        $(document).on('change', '#atBatchAll', function () {
            const set = activeSelectedSet();
            if (!set) { this.checked = false; return; }
            const visible = getVisibleEndpoints();
            const checked = this.checked;
            visible.forEach(function (ep) { if (checked) set.add(ep.id); else set.delete(ep.id); });
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
            else atRefreshBatchToolbar();
        });

        // 工具栏: 可见范围内反选
        $(document).on('click', '#atBatchInvert', function () {
            const set = activeSelectedSet();
            if (!set) return;
            const visible = getVisibleEndpoints();
            visible.forEach(function (ep) { if (set.has(ep.id)) set.delete(ep.id); else set.add(ep.id); });
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
            else atRefreshBatchToolbar();
        });

        // 工具栏: 清空当前集合选中 (含不可见的, 避免“隐藏选中项”误导)
        $(document).on('click', '#atBatchClear', function () {
            const set = activeSelectedSet();
            if (!set) return;
            set.clear();
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
            else atRefreshBatchToolbar();
        });
    }

    // ---------- 集合级 currentCollection ----------
    function currentCollection() {
        const cid = state.activeCollectionId;
        return (state.collections || []).find(c => c.id === cid);
    }

    // ---------- 全局授权弹窗 ----------
    let globalAuthModel = null;
    function openGlobalAuth() {
        const col = currentCollection();
        if (!col) { toast('请先选择集合'); return; }
        let auth = { type: 'none', token: '', username: '', password: '', keyName: '', keyValue: '', keyIn: 'header', customHeaders: [] };
        try {
            const parsed = col.globalAuth ? JSON.parse(col.globalAuth) : null;
            if (parsed) auth = Object.assign(auth, parsed);
        } catch { /* ignore */ }
        globalAuthModel = auth;
        renderGlobalAuthBody();
        $('#atGlobalAuthModal').prop('hidden', false);
    }
    function closeGlobalAuth() { $('#atGlobalAuthModal').prop('hidden', true); }

    function renderGlobalAuthBody() {
        const a = globalAuthModel;
        const types = ['none', 'bearer', 'basic', 'apikey', 'custom'];
        const opts = types.map(x => '<option value="' + x + '"' + (x === a.type ? ' selected' : '') + '>' + x + '</option>').join('');
        const $body = $('#atGlobalAuthBody').empty();
        $body.append('<div class="at-form-row"><label>类型</label><select id="gaType">' + opts + '</select></div>');
        if (a.type === 'bearer') {
            $body.append('<div class="at-form-row"><label>Token</label><input type="text" id="gaToken" /></div>');
            $('#gaToken').val(a.token || '');
        } else if (a.type === 'basic') {
            $body.append('<div class="at-form-row"><label>Username</label><input type="text" id="gaUser" /></div>');
            $body.append('<div class="at-form-row"><label>Password</label><input type="text" id="gaPass" /></div>');
            $('#gaUser').val(a.username || ''); $('#gaPass').val(a.password || '');
        } else if (a.type === 'apikey') {
            $body.append('<div class="at-form-row"><label>Key 名</label><input type="text" id="gaKeyName" /></div>');
            $body.append('<div class="at-form-row"><label>Key 值</label><input type="text" id="gaKeyValue" /></div>');
            $body.append('<div class="at-form-row"><label>位置</label><select id="gaKeyIn"><option value="header"' + (a.keyIn === 'header' ? ' selected' : '') + '>header</option><option value="query"' + (a.keyIn === 'query' ? ' selected' : '') + '>query</option></select></div>');
            $('#gaKeyName').val(a.keyName || ''); $('#gaKeyValue').val(a.keyValue || '');
        } else if (a.type === 'custom') {
            $body.append('<div class="at-form-row"><label>自定义 Headers (JSON 数组)</label><textarea id="gaCustom" rows="6"></textarea></div>');
            $('#gaCustom').val(JSON.stringify(a.customHeaders || [], null, 2));
        }
    }

    function syncGlobalAuthFromUI() {
        const a = globalAuthModel;
        a.type = $('#gaType').val() || 'none';
        if (a.type === 'bearer') a.token = $('#gaToken').val() || '';
        else if (a.type === 'basic') { a.username = $('#gaUser').val() || ''; a.password = $('#gaPass').val() || ''; }
        else if (a.type === 'apikey') { a.keyName = $('#gaKeyName').val() || ''; a.keyValue = $('#gaKeyValue').val() || ''; a.keyIn = $('#gaKeyIn').val() || 'header'; }
        else if (a.type === 'custom') {
            try { a.customHeaders = JSON.parse($('#gaCustom').val() || '[]'); } catch { a.customHeaders = []; }
        }
    }

    async function saveGlobalAuth() {
        const col = currentCollection();
        if (!col) return;
        syncGlobalAuthFromUI();
        try {
            await window.atApi.saveCollection({
                id: col.id,
                name: col.name,
                baseUrl: col.baseUrl || '',
                description: col.description || '',
                globalAuth: JSON.stringify(globalAuthModel),
                globalHeaders: col.globalHeaders || '[]',
                globalValidators: col.globalValidators || '[]'
            });
            col.globalAuth = JSON.stringify(globalAuthModel);
            toast('已保存全局授权');
            closeGlobalAuth();
        } catch (e) { toast('保存失败: ' + e.message); }
    }

    // ---------- 全局校验弹窗 ----------
    let globalValModel = [];
    const VAL_OPS = [
        { v: 'eq', t: '等于' }, { v: 'ne', t: '不等于' },
        { v: 'gt', t: '大于' }, { v: 'lt', t: '小于' },
        { v: 'ge', t: '大于等于' }, { v: 'le', t: '小于等于' },
        { v: 'contains', t: '包含' }, { v: 'exists', t: '存在' }
    ];
    function openGlobalVal() {
        const col = currentCollection();
        if (!col) { toast('请先选择集合'); return; }
        try { globalValModel = col.globalValidators ? JSON.parse(col.globalValidators) : []; }
        catch { globalValModel = []; }
        if (!Array.isArray(globalValModel)) globalValModel = [];
        renderGlobalValBody();
        $('#atGlobalValModal').prop('hidden', false);
    }
    function closeGlobalVal() { $('#atGlobalValModal').prop('hidden', true); }

    function renderGlobalValBody() {
        const $body = $('#atGlobalValBody').empty();
        const $tbl = $('<table class="at-kv-table at-validate-table"><thead><tr>' +
            '<th>字段</th><th style="width:120px">条件</th><th>期望值</th><th style="width:30px"></th>' +
            '</tr></thead><tbody></tbody></table>');
        globalValModel.forEach(function (row, idx) {
            const opts = VAL_OPS.map(o => '<option value="' + o.v + '"' + (o.v === row.op ? ' selected' : '') + '>' + o.t + '</option>').join('');
            $tbl.find('tbody').append('<tr data-idx="' + idx + '">' +
                '<td><input type="text" class="gv-field" value="' + escape(row.field || '') + '" placeholder="data.code 或 status" /></td>' +
                '<td><select class="gv-op">' + opts + '</select></td>' +
                '<td><input type="text" class="gv-expected" value="' + escape(row.expected || '') + '" /></td>' +
                '<td><button class="at-icon-btn gv-del">×</button></td>' +
                '</tr>');
        });
        $body.append($tbl);
        $body.append('<button class="btn at-btn" id="gvAdd">+ 添加规则</button>');
    }

    function syncGlobalValFromUI() {
        const arr = [];
        $('#atGlobalValBody tbody tr').each(function () {
            arr.push({
                field: $(this).find('.gv-field').val() || '',
                op: $(this).find('.gv-op').val() || 'eq',
                expected: $(this).find('.gv-expected').val() || ''
            });
        });
        globalValModel = arr;
    }

    async function saveGlobalVal() {
        const col = currentCollection();
        if (!col) return;
        syncGlobalValFromUI();
        try {
            await window.atApi.saveCollection({
                id: col.id, name: col.name, baseUrl: col.baseUrl || '',
                description: col.description || '',
                globalAuth: col.globalAuth || '{}',
                globalHeaders: col.globalHeaders || '[]',
                globalValidators: JSON.stringify(globalValModel)
            });
            col.globalValidators = JSON.stringify(globalValModel);
            $('#globalValidatorCount').text(globalValModel.length);
            toast('已保存全局校验');
            closeGlobalVal();
        } catch (e) { toast('保存失败: ' + e.message); }
    }

    // ---------- 全局校验徽标随集合切换刷新 ----------
    function refreshGlobalValBadge() {
        const col = currentCollection();
        let n = 0;
        if (col && col.globalValidators) {
            try { const arr = JSON.parse(col.globalValidators); if (Array.isArray(arr)) n = arr.length; } catch { /* ignore */ }
        }
        $('#globalValidatorCount').text(n);
    }

    // ---------- 批量测试弹窗 ----------
    let batchOrder = []; // [endpointId,...]
    function openBatch() {
        const set = activeSelectedSet();
        if (!set || set.size === 0) { toast('请在左侧勾选至少 1 个接口'); return; }
        batchOrder = Array.from(set);
        renderBatchBody();
        $('#atBatchModal').prop('hidden', false);
    }
    function closeBatch() { $('#atBatchModal').prop('hidden', true); }

    function renderBatchBody() {
        const $tb = $('#atBatchSelected').empty();
        batchOrder.forEach(function (id, idx) {
            const ep = (state.endpoints || []).find(e => e.id === id);
            if (!ep) return;
            const m = (ep.method || 'GET').toUpperCase();
            $tb.append('<tr data-id="' + id + '" draggable="true">' +
                '<td>' + (idx + 1) + '</td>' +
                '<td><span class="at-method at-method-' + m + '">' + m + '</span> ' +
                '<span class="at-batch-name">' + escape(ep.name || ep.path || '') + '</span> ' +
                '<span class="at-batch-path">' + escape(ep.path || '') + '</span></td>' +
                '<td class="bt-status">-</td>' +
                '<td class="bt-duration">-</td>' +
                '<td class="bt-validate">-</td>' +
                '<td><button class="at-icon-btn bt-remove">×</button></td>' +
                '</tr>');
        });
    }

    async function runBatch() {
        if (!batchOrder.length) { toast('未选择接口'); return; }
        $('#atBatchRun').prop('disabled', true).text('执行中...');
        try {
            const data = await window.atApi.batchSend({ collectionId: state.activeCollectionId, endpointIds: batchOrder });
            const items = (data && data.items) || [];
            items.forEach(function (it) {
                const $tr = $('#atBatchSelected tr[data-id="' + it.endpointId + '"]');
                const cls = it.status >= 500 ? 'at-status-5xx' : it.status >= 400 ? 'at-status-4xx' : it.status >= 300 ? 'at-status-3xx' : it.status >= 200 ? 'at-status-2xx' : 'at-status-err';
                // 业务校验失败(HTTP 成功但规则不过) 时状态列加 ⚠ 提醒, 避免被误读为“请求成功即全部通过”
                const bizFailed = !it.error && it.allValidatorsPassed === false;
                $tr.find('.bt-status').html(it.error
                    ? '<span class="at-status-err">ERR</span>'
                    : '<span class="' + cls + '">' + it.status + (bizFailed ? ' ⚠' : '') + '</span>');
                $tr.find('.bt-duration').text(it.durationMs + ' ms');
                // 校验列: 出错显示 -, 全部通过显示 ✓ (含总数), 有失败显示 ✗ (可 hover 查看失败原因)
                const vrs = it.validatorResults || [];
                if (it.error) {
                    $tr.find('.bt-validate').html('-');
                } else if (vrs.length === 0) {
                    $tr.find('.bt-validate').html('<span class="at-vr-pass" title="无校验规则">✓ 0</span>');
                } else if (it.allValidatorsPassed) {
                    $tr.find('.bt-validate').html('<span class="at-vr-pass" title="全部通过">✓ ' + vrs.length + '</span>');
                } else {
                    const failed = vrs.filter(v => !v.passed);
                    const tip = failed.map(v => '[' + (v.source || '') + '] ' + (v.field || '') + ' ' + (v.op || '') + ' ' + (v.expected || '') + ' (actual=' + (v.actual || '') + ')').join('\n');
                    $tr.find('.bt-validate').html('<span class="at-vr-fail" title="' + escape(tip) + '">✗ ' + failed.length + '/' + vrs.length + '</span>');
                }
            });
            // 批量执行结束摘要: 区分“通过 / 校验失败 / 请求出错”, 避免用户误以为全部通过
            const total = items.length;
            const passedCount = items.filter(i => !i.error && i.allValidatorsPassed).length;
            const failedCount = items.filter(i => !i.error && !i.allValidatorsPassed).length;
            const errorCount = items.filter(i => !!i.error).length;
            const parts = [passedCount + '/' + total + ' 通过'];
            if (failedCount > 0) parts.push(failedCount + ' 校验失败');
            if (errorCount > 0) parts.push(errorCount + ' 请求出错');
            toast('批量执行完成: ' + parts.join(', '));
        } catch (e) { toast('批量执行失败: ' + e.message); }
        finally { $('#atBatchRun').prop('disabled', false).text('依次执行'); }
    }

    // ---------- 拖拽调整顺序 ----------
    let dragId = 0;
    function bindDrag() {
        $(document).on('dragstart', '#atBatchSelected tr', function (e) {
            dragId = parseInt($(this).data('id'), 10) || 0;
            e.originalEvent.dataTransfer.effectAllowed = 'move';
        });
        $(document).on('dragover', '#atBatchSelected tr', function (e) {
            e.preventDefault();
            e.originalEvent.dataTransfer.dropEffect = 'move';
        });
        $(document).on('drop', '#atBatchSelected tr', function (e) {
            e.preventDefault();
            const targetId = parseInt($(this).data('id'), 10) || 0;
            if (!dragId || dragId === targetId) return;
            const fromIdx = batchOrder.indexOf(dragId);
            const toIdx = batchOrder.indexOf(targetId);
            if (fromIdx < 0 || toIdx < 0) return;
            batchOrder.splice(fromIdx, 1);
            batchOrder.splice(toIdx, 0, dragId);
            renderBatchBody();
        });
        $(document).on('click', '.bt-remove', function () {
            const id = parseInt($(this).closest('tr').data('id'), 10) || 0;
            batchOrder = batchOrder.filter(x => x !== id);
            const set = activeSelectedSet();
            if (set) set.delete(id);
            refreshBatchBadge();
            renderBatchBody();
            // 同步持久化 (该路径不走 atRefreshBatchToolbar)
            saveSelected();
            // 同时刷新侧边栏以同步 checkbox 状态
            if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
        });
    }

    // ---------- 新建接口 ----------
    async function newEndpoint() {
        const cid = state.activeCollectionId;
        if (!cid) { toast('请先选择集合'); return; }
        const name = prompt('请输入接口名称', 'Untitled');
        if (!name) return;
        try {
            const data = await window.atApi.saveEndpoint({
                id: 0, collectionId: cid, tag: 'MANUAL', name: name,
                method: 'GET', path: '/', params: '[]', headers: '[]',
                body: '', bodyType: 'none', auth: JSON.stringify({ inherit: true, type: 'none' }),
                extractors: '[]', validators: '[]', overrideGlobalValidators: false, orderNo: 0
            });
            toast('已新建接口');
            // 刷新接口列表
            if (typeof window.atApi.getEndpoints === 'function') {
                const list = await window.atApi.getEndpoints(cid);
                state.endpoints = list || [];
                state.activeEndpointId = data && data.id || 0;
                // 触发 sidebar 重绘(由主控渲染)
                $(document).trigger('apiTester:endpointsRefreshed');
                if (window.atEditor && data && data.id) window.atEditor.loadEndpoint(data.id);
            }
        } catch (e) { toast('新建失败: ' + e.message); }
    }

    // 监听集合变化, 刷新全局校验徽标和批量徽标
    $(document).on('apiTester:collectionChanged', function () {
        refreshGlobalValBadge();
        refreshBatchBadge();
    });
    // 监听接口列表刷新, 让主控 renderEndpoints 重绘
    $(document).on('apiTester:endpointsRefreshed', function () {
        if (typeof window.atRenderEndpoints === 'function') window.atRenderEndpoints();
    });

    // ---------- 事件绑定 ----------
    $(function () {
        bindCheckboxes();
        bindDrag();

        $(document).on('click', '[data-action="global-auth"]', openGlobalAuth);
        $(document).on('click', '[data-action="close-global-auth"]', closeGlobalAuth);
        $(document).on('click', '#atGlobalAuthSave', saveGlobalAuth);
        $(document).on('change', '#gaType', function () { syncGlobalAuthFromUI(); renderGlobalAuthBody(); });

        $(document).on('click', '[data-action="global-validators"]', openGlobalVal);
        $(document).on('click', '[data-action="close-global-val"]', closeGlobalVal);
        $(document).on('click', '#atGlobalValSave', saveGlobalVal);
        $(document).on('click', '#gvAdd', function () {
            syncGlobalValFromUI();
            globalValModel.push({ field: '', op: 'eq', expected: '' });
            renderGlobalValBody();
        });
        $(document).on('click', '.gv-del', function () {
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            syncGlobalValFromUI();
            globalValModel.splice(idx, 1);
            renderGlobalValBody();
        });

        $(document).on('click', '[data-action="batch-test"]', openBatch);
        $(document).on('click', '[data-action="close-batch"]', closeBatch);
        $(document).on('click', '#atBatchRun', runBatch);

        $(document).on('click', '[data-action="new-endpoint"]', newEndpoint);

        // 集合切换时刷新徽标
        $(document).on('change', '#atCollectionSelect', function () {
            setTimeout(function () { refreshGlobalValBadge(); refreshBatchBadge(); }, 100);
        });
        // 启动时也刷新
        setTimeout(function () { refreshGlobalValBadge(); refreshBatchBadge(); }, 500);
    });

})(jQuery);
