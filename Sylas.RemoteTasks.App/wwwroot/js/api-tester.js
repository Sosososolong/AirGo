/* ============================================================================
   API Tester 前端主控
   - 状态: window.apiTesterState
   - 模块: 工具 / API / Toast / Sidebar(集合+接口列表) / Swagger 导入弹窗
   - 中右两栏的编辑器与响应面板将在 Task 3 / Task 4 实现
   ============================================================================ */
(function ($) {
    'use strict';

    // ---------- 全局状态 ----------
    const state = window.apiTesterState = window.apiTesterState || {
        collections: [],
        endpoints: [],
        activeCollectionId: 0,
        activeEndpointId: 0,
        activeEnvId: 0,
        variables: {},
        searchKeyword: '',
        collapsedTags: {} // {collectionId: {tagName: true}}
    };
    // 确保后加入的属性存在 (旧版本内联脚本可能缺少这些字段)
    state.collapsedTags = state.collapsedTags || {};
    state.batchSelected = state.batchSelected || {};

    // ---------- Toast ----------
    function toast(msg, ms) {
        const $t = $('<div class="at-toast"></div>').text(msg).appendTo(document.body);
        setTimeout(() => $t.fadeOut(200, () => $t.remove()), ms || 1800);
    }
    window.atToast = toast;

    // ---------- API 封装 ----------
    // 自包含 fetch 实现, 不依赖 site.js 的 httpRequestAsync (避免 Layout 内联函数缺失问题)
    // 后端统一返回 { code: 0, data, msg } 格式, code===0 为业务成功
    function atPost(url, body) {
        var token = (typeof getAccessToken === 'function' ? getAccessToken() : '') || localStorage.getItem('access_token') || '';
        if (!token) {
            toast('身份已过期, 请重新登录');
            return Promise.reject(new Error('身份已过期'));
        }
        return fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'authorization': 'Bearer ' + token
            },
            body: JSON.stringify(body == null ? {} : body)
        })
        .then(function (response) {
            if (!response.ok) {
                if (response.status === 401) {
                    toast('身份已过期, 请重新登录');
                    return Promise.reject(new Error('身份已过期'));
                }
                return Promise.reject(new Error('请求异常: ' + response.status + ' ' + response.statusText));
            }
            return response.json();
        })
        .then(function (resp) {
            if (!resp || resp.code !== 0) {
                var msg = (resp && (resp.msg || resp.message)) || '业务失败';
                return Promise.reject(new Error(msg));
            }
            return resp.data;
        });
    }
    window.atApi = {
        _post: atPost,
        getCollections: () => atPost('/ApiTester/GetCollections'),
        deleteCollection: (id) => atPost('/ApiTester/DeleteCollection', { id }),
        getEndpoints: (collectionId) => atPost('/ApiTester/GetEndpoints', { collectionId }),
        deleteEndpoint: (id) => atPost('/ApiTester/DeleteEndpoint', { id }),
        getEndpoint: (id) => atPost('/ApiTester/GetEndpoint', { id }),
        saveEndpoint: (dto) => atPost('/ApiTester/SaveEndpoint', dto),
        sendRequest: (dto) => atPost('/ApiTester/SendRequest', dto),
        importSwagger: (dto) => atPost('/ApiTester/ImportSwagger', dto),
        updateEndpointsOrder: (orders) => atPost('/ApiTester/UpdateEndpointsOrder', { orders })
    };

    // ---------- 工具方法 ----------
    function escapeHtml(s) {
        return (s == null ? '' : String(s))
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
    window.atEscape = escapeHtml;

    // ---------- Sidebar: 集合下拉 ----------
    function renderCollections() {
        const $sel = $('#atCollectionSelect').empty().append('<option value="">-- 选择集合 --</option>');
        state.collections.forEach(function (c) {
            $sel.append('<option value="' + c.id + '">' + escapeHtml(c.name) +
                ' (' + (c.endpointCount || 0) + ')</option>');
        });
        $sel.val(state.activeCollectionId || '');
    }

    async function loadCollections(autoSelectId) {
        try {
            const data = await window.atApi.getCollections();
            state.collections = (data && data.length) ? data : [];
            renderCollections();
            const targetId = autoSelectId
                || (state.activeCollectionId && state.collections.find(c => c.id === state.activeCollectionId) ? state.activeCollectionId : 0)
                || (state.collections[0] && state.collections[0].id) || 0;
            if (targetId) {
                state.activeCollectionId = targetId;
                $('#atCollectionSelect').val(targetId);
                await loadEndpoints(targetId);
            } else {
                state.endpoints = [];
                renderEndpoints();
            }
        } catch (e) {
            toast('加载集合失败: ' + e.message);
        }
    }

    // ---------- Sidebar: 接口列表 ----------
    async function loadEndpoints(collectionId) {
        if (!collectionId) {
            state.endpoints = [];
            renderEndpoints();
            return;
        }
        try {
            const data = await window.atApi.getEndpoints(collectionId);
            state.endpoints = data || [];
            renderEndpoints();
        } catch (e) {
            toast('加载接口失败: ' + e.message);
        }
    }
    // 暴露给其它模块(批量/新建接口 之后需重画)
    window.atRenderEndpoints = function () { renderEndpoints(); };
    // 暴露给导入后重载集合列表(Task 7)
    window.atReloadCollections = function (autoSelectId) { return loadCollections(autoSelectId); };

    function renderEndpoints() {
        const $list = $('#atEndpointList').empty();
        const eps = state.endpoints || [];
        if (!eps.length) {
            $list.append('<div class="at-empty-tip">该集合暂无接口</div>');
            return;
        }
        const kw = (state.searchKeyword || '').toLowerCase();
        const filtered = eps.filter(function (ep) {
            if (!kw) return true;
            return (ep.name || '').toLowerCase().indexOf(kw) >= 0
                || (ep.path || '').toLowerCase().indexOf(kw) >= 0
                || (ep.method || '').toLowerCase().indexOf(kw) >= 0;
        });
        if (!filtered.length) {
            $list.append('<div class="at-empty-tip">无匹配接口</div>');
            return;
        }

        // 按 Tag 分组
        const groups = {};
        filtered.forEach(function (ep) {
            const tag = ep.tag || 'default';
            (groups[tag] = groups[tag] || []).push(ep);
        });

        // 获取当前集合的选中集 (供 checkbox 恢复 checked 状态 / Tag 分组三态判断)
        const cid = state.activeCollectionId;
        const selectedSet = (state.batchSelected && state.batchSelected[cid]) || null;

        const collapsed = (state.collapsedTags || {})[state.activeCollectionId] || {};
        Object.keys(groups).sort().forEach(function (tag) {
            const list = groups[tag];
            const isCollapsed = !!collapsed[tag];
            // 计算 Tag 分组复选框三态: 全选 / 部分 / 未选
            let tagSelectedCount = 0;
            if (selectedSet) list.forEach(function (ep) { if (selectedSet.has(ep.id)) tagSelectedCount++; });
            const tagAll = tagSelectedCount > 0 && tagSelectedCount === list.length;
            const tagSome = tagSelectedCount > 0 && tagSelectedCount < list.length;
            const $g = $('<div class="at-tag-group ' + (isCollapsed ? 'collapsed' : '') + '"></div>')
                .attr('data-tag', tag);
            $g.append(
                '<div class="at-tag-header" data-action="toggle-tag">' +
                '<input type="checkbox" class="at-tag-checkbox" data-action="check-tag"' + (tagAll ? ' checked' : '') + ' title="全选/取消该分组" />' +
                '<span class="at-tag-arrow">▼</span>' +
                '<span>' + escapeHtml(tag) + '</span>' +
                '<span class="at-tag-count">' + (tagSelectedCount > 0 ? tagSelectedCount + '/' : '') + list.length + '</span>' +
                '</div>'
            );
            // 设置 indeterminate (jQuery 不能通过字符串属性设置, 需 DOM 属性)
            if (tagSome) $g.find('.at-tag-checkbox').prop('indeterminate', true);
            list.forEach(function (ep) {
                const m = (ep.method || 'GET').toUpperCase();
                const isActive = ep.id === state.activeEndpointId;
                const isChecked = selectedSet && selectedSet.has(ep.id);
                $g.append(
                    '<div class="at-endpoint-item ' + (isActive ? 'active' : '') + '" data-id="' + ep.id + '" data-tag="' + escapeHtml(tag) + '" data-action="select-endpoint" draggable="true">' +
                    '<input type="checkbox" class="at-endpoint-checkbox" data-action="check-endpoint"' + (isChecked ? ' checked' : '') + ' />' +
                    '<span class="at-method at-method-' + m + '">' + m + '</span>' +
                    '<div class="at-endpoint-info">' +
                    '<div class="at-endpoint-name">' + escapeHtml(ep.name || ep.path) + '</div>' +
                    '<div class="at-endpoint-path">' + escapeHtml(ep.path) + '</div>' +
                    '</div>' +
                    '</div>'
                );
            });
            $list.append($g);
        });
        // 渲染完后负责通知批量工具栏刷新【全选复选框三态 + 已选计数】
        if (typeof window.atRefreshBatchToolbar === 'function') window.atRefreshBatchToolbar();
    }

    // ---------- Swagger 导入弹窗 ----------
    function openSwaggerModal() {
        $('#swaggerCollectionName, #swaggerBaseUrl, #swaggerUrl, #swaggerContent').val('');
        $('#atSwaggerModal').prop('hidden', false);
    }
    function closeSwaggerModal() {
        $('#atSwaggerModal').prop('hidden', true);
    }

    async function submitSwaggerImport() {
        const dto = {
            content: $('#swaggerContent').val() || '',
            url: $('#swaggerUrl').val() || '',
            collectionName: $('#swaggerCollectionName').val() || '',
            baseUrl: $('#swaggerBaseUrl').val() || ''
        };
        if (!dto.content && !dto.url) {
            toast('请提供 swagger URL 或内容');
            return;
        }
        const $btn = $('#atSwaggerSubmit').prop('disabled', true).text('导入中...');
        try {
            const data = await window.atApi.importSwagger(dto);
            toast('导入成功');
            closeSwaggerModal();
            await loadCollections(data && data.collectionId);
        } catch (e) {
            toast('导入失败: ' + e.message);
        } finally {
            $btn.prop('disabled', false).text('开始导入');
        }
    }

    // ---------- 删除集合 ----------
    // 该功能已迁移为声明式按钮: Index.cshtml 中 #atDeleteCollection 使用
    // data-execute-url + showConfirmBox + execute(this) (参考 site.js 统一模式)
    // 原函数 deleteCurrentCollection 已废弃

    // ---------- 事件绑定 ----------
    function bindEvents() {
        // Header 工具栏
        $(document).on('click', '[data-action="import-swagger"]', openSwaggerModal);
        $(document).on('click', '[data-action="close-modal"]', closeSwaggerModal);
        $(document).on('click', '#atSwaggerSubmit', submitSwaggerImport);

        // 集合下拉
        $(document).on('change', '#atCollectionSelect', async function () {
            const id = parseInt(this.value, 10) || 0;
            state.activeCollectionId = id;
            state.activeEndpointId = 0;
            await loadEndpoints(id);
            $(document).trigger('apiTester:collectionChanged');
        });
        // #atDeleteCollection 已改为声明式 data-execute-url + onclick (见 Index.cshtml)

        // 搜索
        $(document).on('input', '#atSearchInput', function () {
            state.searchKeyword = this.value || '';
            renderEndpoints();
        });

        // Tag 折叠
        $(document).on('click', '[data-action="toggle-tag"]', function () {
            const $g = $(this).closest('.at-tag-group');
            const tag = $g.data('tag');
            const cid = state.activeCollectionId;
            const map = state.collapsedTags[cid] = state.collapsedTags[cid] || {};
            map[tag] = !map[tag];
            $g.toggleClass('collapsed');
        });

        // 选中接口 (Task 3 接管编辑器渲染)
        $(document).on('click', '[data-action="select-endpoint"]', function (e) {
            if ($(e.target).is('input[type=checkbox]')) return;
            const id = parseInt($(this).data('id'), 10) || 0;
            state.activeEndpointId = id;
            $('.at-endpoint-item').removeClass('active');
            $(this).addClass('active');
            // 后续 Task 在这里调用编辑器渲染:
            if (window.atEditor && typeof window.atEditor.loadEndpoint === 'function') {
                window.atEditor.loadEndpoint(id);
            }
        });

        // 阻止 checkbox 触发选中
        $(document).on('click', '[data-action="check-endpoint"]', function (e) {
            e.stopPropagation();
        });

        // ---------- 拖拽排序 (仅同 Tag 内) ----------
        let dragSrcId = 0;
        let dragSrcTag = '';

        $(document).on('dragstart', '.at-endpoint-item', function (e) {
            dragSrcId = parseInt($(this).data('id'), 10) || 0;
            dragSrcTag = String($(this).data('tag') || '');
            $(this).addClass('at-dragging');
            try { e.originalEvent.dataTransfer.effectAllowed = 'move'; e.originalEvent.dataTransfer.setData('text/plain', String(dragSrcId)); } catch (_) { }
        });
        $(document).on('dragend', '.at-endpoint-item', function () {
            $(this).removeClass('at-dragging');
            $('.at-drop-before, .at-drop-after').removeClass('at-drop-before at-drop-after');
        });
        $(document).on('dragover', '.at-endpoint-item', function (e) {
            if (!dragSrcId) return;
            const targetTag = String($(this).data('tag') || '');
            if (targetTag !== dragSrcTag) return; // 仅同 Tag 内拖拽
            e.preventDefault();
            try { e.originalEvent.dataTransfer.dropEffect = 'move'; } catch (_) { }
            const rect = this.getBoundingClientRect();
            const before = (e.originalEvent.clientY - rect.top) < rect.height / 2;
            $('.at-drop-before, .at-drop-after').removeClass('at-drop-before at-drop-after');
            $(this).addClass(before ? 'at-drop-before' : 'at-drop-after');
        });
        $(document).on('dragleave', '.at-endpoint-item', function () {
            $(this).removeClass('at-drop-before at-drop-after');
        });
        $(document).on('drop', '.at-endpoint-item', function (e) {
            if (!dragSrcId) return;
            const targetId = parseInt($(this).data('id'), 10) || 0;
            const targetTag = String($(this).data('tag') || '');
            const before = $(this).hasClass('at-drop-before');
            $('.at-drop-before, .at-drop-after').removeClass('at-drop-before at-drop-after');
            e.preventDefault();
            if (targetTag !== dragSrcTag || targetId === dragSrcId) { dragSrcId = 0; return; }

            // 重排 state.endpoints (同 Tag 范围内)
            const eps = state.endpoints;
            const srcIdx = eps.findIndex(x => x.id === dragSrcId);
            const tgtIdx = eps.findIndex(x => x.id === targetId);
            if (srcIdx < 0 || tgtIdx < 0) { dragSrcId = 0; return; }
            const [moved] = eps.splice(srcIdx, 1);
            // splice 后 tgtIdx 可能偏移
            let insertAt = eps.findIndex(x => x.id === targetId);
            if (!before) insertAt += 1;
            eps.splice(insertAt, 0, moved);

            // 重新分配同 Tag 内的 OrderNo (从 0 开始)
            const orders = [];
            let counter = 0;
            eps.forEach(function (ep) {
                if (ep.tag === dragSrcTag) {
                    ep.orderNo = counter++;
                    orders.push({ id: ep.id, orderNo: ep.orderNo });
                }
            });
            renderEndpoints();
            const srcId = dragSrcId; dragSrcId = 0;
            // 保存到后端
            if (window.atApi && window.atApi.updateEndpointsOrder) {
                window.atApi.updateEndpointsOrder(orders).catch(function () { /* 已 toast */ });
            }
        });
    }

    // ---------- 启动 ----------
    $(function () {
        bindEvents();
        loadCollections();
    });

})(jQuery);
