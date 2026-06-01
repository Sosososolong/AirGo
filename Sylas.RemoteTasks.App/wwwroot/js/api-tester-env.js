/* ============================================================================
   API Tester - 环境与变量管理弹窗
   依赖: api-tester.js (state, atToast, atEscape)
   ============================================================================ */
(function ($) {
    'use strict';

    // 防御性获取全局状态 (极端情况下 api-tester.js 可能加载失败)
    const state = window.apiTesterState = window.apiTesterState || {};

    // ---------- 模块本地状态 ----------
    let envs = [];           // 环境列表
    let activeEnvId = 0;     // 弹窗内当前选中的环境 Id (用于编辑)
    let variables = [];      // 当前环境的变量列表(可能含临时新增/修改, 未保存)

    // ---------- API 封装 ----------
    // 惰性解析: 在真正调用时才取 window.atApi._post, 而不是脚本加载时立即绑定
    // 这样即使加载顺序偶发异常, 用户操作时 (DOM ready 后) 也能正确获取到 atPost
    function _post(url, data) {
        if (window.atApi && window.atApi._post) return window.atApi._post(url, data);
        console.warn('[api-tester-env] atApi._post 未就绪, 请刷新页面');
        return $.Deferred().reject(new Error('atApi not ready')).promise();
    }
    const envApi = {
        getEnvironments: () => _post('/ApiTester/GetEnvironments'),
        saveEnvironment: (dto) => _post('/ApiTester/SaveEnvironment', dto),
        deleteEnvironment: (id) => _post('/ApiTester/DeleteEnvironment', { id }),
        setActiveEnvironment: (id) => _post('/ApiTester/SetActiveEnvironment', { id }),
        getVariables: (environmentId) => _post('/ApiTester/GetVariables', { id: environmentId }),
        saveVariable: (dto) => _post('/ApiTester/SaveVariable', dto),
        deleteVariable: (id) => _post('/ApiTester/DeleteVariable', { id })
    };
    // 暴露给其它模块复用
    window.atApi = window.atApi || {};
    Object.assign(window.atApi, envApi);

    // ---------- 打开 / 关闭弹窗 ----------
    async function open() {
        $('#atEnvModal').prop('hidden', false);
        await reloadEnvs();
    }

    function close() {
        $('#atEnvModal').prop('hidden', true);
    }

    async function reloadEnvs() {
        try {
            const data = await envApi.getEnvironments();
            envs = data || [];
            // 默认选中: 弹窗内已选 → 激活环境 → 第一个
            if (!envs.find(e => e.id === activeEnvId)) {
                const act = envs.find(e => e.isActive) || envs[0];
                activeEnvId = act ? act.id : 0;
            }
            // 同步主状态: 当前激活环境 Id
            const currentActive = envs.find(e => e.isActive);
            state.activeEnvId = currentActive ? currentActive.id : 0;
            renderEnvList();
            await loadVariables();
            updateEnvCountBadge();
        } catch (e) {
            window.atToast('加载环境失败: ' + e.message);
        }
    }

    function renderEnvList() {
        const $ul = $('#atEnvList').empty();
        if (!envs.length) {
            $ul.append('<li class="at-empty-tip" style="padding:12px">暂无环境, 请新建</li>');
            return;
        }
        envs.forEach(function (e) {
            const isActive = !!e.isActive;
            const isSelected = e.id === activeEnvId;
            $ul.append(
                '<li class="at-env-item' +
                (isSelected ? ' selected' : '') +
                (isActive ? ' active' : '') +
                '" data-id="' + e.id + '">' +
                '<span class="at-env-name">' + window.atEscape(e.name) + '</span>' +
                (isActive ? '<span class="at-env-tag">激活</span>' : '') +
                '<span class="at-env-actions">' +
                (isActive ? '' : '<button class="at-icon-btn at-env-activate" title="设为激活">★</button>') +
                '<button class="at-icon-btn at-env-rename" title="重命名">✎</button>' +
                '<button class="at-icon-btn at-env-del" title="删除">×</button>' +
                '</span>' +
                '</li>'
            );
        });
    }

    async function loadVariables() {
        if (!activeEnvId) {
            variables = [];
            renderVarTable();
            return;
        }
        try {
            const data = await envApi.getVariables(activeEnvId);
            variables = (data || []).map(v => ({
                id: v.id,
                environmentId: v.environmentId,
                name: v.name || '',
                value: v.value || '',
                description: v.description || '',
                isSecret: !!v.isSecret,
                _dirty: false,
                _new: false
            }));
            const env = envs.find(e => e.id === activeEnvId);
            $('#atEnvCurrentName').text(env ? ('变量列表 — ' + env.name) : '变量列表');
            renderVarTable();
        } catch (e) {
            window.atToast('加载变量失败: ' + e.message);
        }
    }

    function renderVarTable() {
        const $tb = $('#atVarTable tbody').empty();
        if (!variables.length) {
            $tb.append('<tr><td colspan="6" class="at-empty-tip" style="text-align:center;padding:16px">暂无变量, 点击 “+ 添加变量”</td></tr>');
            return;
        }
        variables.forEach(function (v, idx) {
            $tb.append(
                '<tr data-idx="' + idx + '">' +
                '<td><input type="text" class="var-name" value="' + window.atEscape(v.name) + '" placeholder="变量名" /></td>' +
                '<td><input type="text" class="var-value" value="' + window.atEscape(v.value) + '" placeholder="值" /></td>' +
                '<td><input type="text" class="var-desc" value="' + window.atEscape(v.description) + '" placeholder="描述" /></td>' +
                '<td style="text-align:center"><input type="checkbox" class="var-secret"' + (v.isSecret ? ' checked' : '') + ' /></td>' +
                '<td><button class="at-icon-btn var-copy" title="复制 {{变量名}} 到剪贴板">📋</button></td>' +
                '<td><button class="at-icon-btn var-del">×</button></td>' +
                '</tr>'
            );
        });
    }

    function syncVariablesFromUI() {
        $('#atVarTable tbody tr').each(function () {
            const idx = parseInt($(this).data('idx'), 10);
            if (isNaN(idx) || !variables[idx]) return;
            const v = variables[idx];
            const newName = $(this).find('.var-name').val() || '';
            const newValue = $(this).find('.var-value').val() || '';
            const newDesc = $(this).find('.var-desc').val() || '';
            const newSecret = $(this).find('.var-secret').is(':checked');
            if (v.name !== newName || v.value !== newValue || v.description !== newDesc || v.isSecret !== newSecret) {
                v.name = newName;
                v.value = newValue;
                v.description = newDesc;
                v.isSecret = newSecret;
                v._dirty = true;
            }
        });
    }

    function updateEnvCountBadge() {
        const active = envs.find(e => e.isActive);
        const count = active ? variables.filter(v => v.name).length : 0;
        $('#envCount').text(count);
    }

    // ---------- 操作: 环境 ----------
    async function addEnvironment() {
        const name = prompt('请输入新环境名称');
        if (!name) return;
        try {
            const data = await envApi.saveEnvironment({ id: 0, name: name, isActive: false });
            activeEnvId = data && data.id || 0;
            await reloadEnvs();
            window.atToast('已新建环境');
        } catch (e) {
            window.atToast('新建失败: ' + e.message);
        }
    }

    async function renameEnvironment(id) {
        const env = envs.find(e => e.id === id);
        if (!env) return;
        const name = prompt('修改环境名称', env.name);
        if (!name || name === env.name) return;
        try {
            await envApi.saveEnvironment({ id: id, name: name, isActive: env.isActive });
            await reloadEnvs();
        } catch (e) {
            window.atToast('重命名失败: ' + e.message);
        }
    }

    async function deleteEnvironment(id) {
        if (!confirm('确认删除该环境及其全部变量?')) return;
        try {
            await envApi.deleteEnvironment(id);
            if (activeEnvId === id) activeEnvId = 0;
            await reloadEnvs();
            window.atToast('已删除');
        } catch (e) {
            window.atToast('删除失败: ' + e.message);
        }
    }

    async function activateEnvironment(id) {
        try {
            await envApi.setActiveEnvironment(id);
            await reloadEnvs();
            window.atToast('已激活');
        } catch (e) {
            window.atToast('激活失败: ' + e.message);
        }
    }

    // ---------- 操作: 变量 ----------
    function addVariable() {
        if (!activeEnvId) {
            window.atToast('请先选择环境');
            return;
        }
        syncVariablesFromUI();
        variables.push({
            id: 0,
            environmentId: activeEnvId,
            name: '',
            value: '',
            description: '',
            isSecret: false,
            _dirty: true,
            _new: true
        });
        renderVarTable();
    }

    async function deleteVariable(idx) {
        const v = variables[idx];
        if (!v) return;
        if (v._new || !v.id) {
            variables.splice(idx, 1);
            renderVarTable();
            return;
        }
        if (!confirm('确认删除变量 "' + (v.name || '') + '"?')) return;
        try {
            await envApi.deleteVariable(v.id);
            variables.splice(idx, 1);
            renderVarTable();
            updateEnvCountBadge();
        } catch (e) {
            window.atToast('删除失败: ' + e.message);
        }
    }

    async function saveAllVariables() {
        syncVariablesFromUI();
        const dirty = variables.filter(v => v._dirty && v.name);
        if (!dirty.length) {
            window.atToast('无修改');
            return;
        }
        try {
            for (const v of dirty) {
                const saved = await envApi.saveVariable({
                    id: v.id || 0,
                    environmentId: activeEnvId,
                    name: v.name,
                    value: v.value,
                    description: v.description,
                    isSecret: !!v.isSecret
                });
                if (saved && saved.id) v.id = saved.id;
                v._dirty = false;
                v._new = false;
            }
            window.atToast('已保存 ' + dirty.length + ' 项');
            renderVarTable();
            updateEnvCountBadge();
        } catch (e) {
            window.atToast('保存失败: ' + e.message);
        }
    }

    function copyVarReference(idx) {
        const v = variables[idx];
        if (!v || !v.name) {
            window.atToast('请先填写变量名');
            return;
        }
        const text = '{{' + v.name + '}}';
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(
                () => window.atToast('已复制 ' + text),
                () => fallbackCopy(text)
            );
        } else {
            fallbackCopy(text);
        }
    }

    function fallbackCopy(text) {
        const $ta = $('<textarea style="position:fixed;top:-9999px"></textarea>').val(text).appendTo('body');
        $ta[0].select();
        try { document.execCommand('copy'); window.atToast('已复制 ' + text); }
        catch { window.atToast('复制失败, 请手动复制'); }
        $ta.remove();
    }

    // ---------- 启动时刷新 envCount 徽标 ----------
    async function refreshBadgeOnStartup() {
        try {
            const data = await envApi.getEnvironments();
            envs = data || [];
            const act = envs.find(e => e.isActive);
            state.activeEnvId = act ? act.id : 0;
            if (act) {
                const vs = await envApi.getVariables(act.id);
                $('#envCount').text((vs || []).length);
            } else {
                $('#envCount').text(0);
            }
        } catch { /* ignore */ }
    }

    // ---------- 事件绑定 ----------
    function bindEvents() {
        $(document).on('click', '[data-action="env-vars"]', open);
        $(document).on('click', '[data-action="close-env-modal"]', close);

        // 环境列表
        $(document).on('click', '#atEnvAdd', addEnvironment);
        $(document).on('click', '.at-env-item', function (e) {
            if ($(e.target).closest('button').length) return;
            syncVariablesFromUI();
            activeEnvId = parseInt($(this).data('id'), 10) || 0;
            renderEnvList();
            loadVariables();
        });
        $(document).on('click', '.at-env-activate', function (e) {
            e.stopPropagation();
            const id = parseInt($(this).closest('.at-env-item').data('id'), 10) || 0;
            activateEnvironment(id);
        });
        $(document).on('click', '.at-env-rename', function (e) {
            e.stopPropagation();
            const id = parseInt($(this).closest('.at-env-item').data('id'), 10) || 0;
            renameEnvironment(id);
        });
        $(document).on('click', '.at-env-del', function (e) {
            e.stopPropagation();
            const id = parseInt($(this).closest('.at-env-item').data('id'), 10) || 0;
            deleteEnvironment(id);
        });

        // 变量
        $(document).on('click', '#atVarAdd', addVariable);
        $(document).on('click', '#atVarSaveAll', saveAllVariables);
        $(document).on('click', '.var-del', function () {
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            deleteVariable(idx);
        });
        $(document).on('click', '.var-copy', function () {
            syncVariablesFromUI();
            const idx = parseInt($(this).closest('tr').data('idx'), 10);
            copyVarReference(idx);
        });
        $(document).on('input change', '#atVarTable tbody input', function () {
            syncVariablesFromUI();
        });
    }

    $(function () {
        bindEvents();
        refreshBadgeOnStartup();
    });

})(jQuery);
