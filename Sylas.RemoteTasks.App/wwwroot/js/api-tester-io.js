/* ============================================================================
   API Tester - Task 7: 导入导出 JSON
   依赖: api-tester.js (state, atApi, atToast)
   ============================================================================ */
(function ($) {
    'use strict';

    // 防御性获取全局状态 (极端情况下 api-tester.js 可能加载失败)
    const state = window.apiTesterState = window.apiTesterState || {};
    function toast(s) { return window.atToast ? window.atToast(s) : console.warn(s); }

    // 使用 api-tester.js 提供的统一 atApi._post (基于 site.js httpRequestAsync, 自动 token/401/loading)
    // 惰性解析: 真正调用时才取, 避免加载时序竞态
    function _post(url, data) {
        if (window.atApi && window.atApi._post) return window.atApi._post(url, data);
        console.warn('[api-tester-io] atApi._post 未就绪, 请刷新页面');
        return $.Deferred().reject(new Error('atApi not ready')).promise();
    }
    window.atApi = window.atApi || {};
    Object.assign(window.atApi, {
        exportCollection: (id) => _post('/ApiTester/Export', { id: id }),
        importJson: (content) => _post('/ApiTester/ImportJson', { content: content })
    });

    // ---------- 导出 ----------
    async function exportActiveCollection() {
        const cid = state.activeCollectionId;
        if (!cid) { toast('请先选择集合'); return; }
        try {
            const data = await window.atApi.exportCollection(cid);
            const colName = (data && data.collection && data.collection.name) ? data.collection.name : ('collection-' + cid);
            const safeName = colName.replace(/[^a-zA-Z0-9_\u4e00-\u9fa5\-]/g, '_');
            const json = JSON.stringify(data, null, 2);
            const blob = new Blob([json], { type: 'application/json;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = safeName + '.json';
            document.body.appendChild(a);
            a.click();
            setTimeout(function () {
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }, 200);
            toast('导出成功');
        } catch (ex) {
            toast('导出失败: ' + (ex.message || ex));
        }
    }

    // ---------- 导入弹窗 ----------
    function openImportModal() {
        $('#atImportJsonContent').val('');
        $('#atImportJsonFile').val('');
        $('#atImportJsonModal').prop('hidden', false);
    }
    function closeImportModal() {
        $('#atImportJsonModal').prop('hidden', true);
    }

    async function submitImport() {
        const content = ($('#atImportJsonContent').val() || '').trim();
        if (!content) { toast('请粘贴或选择 JSON 内容'); return; }
        try {
            const data = await window.atApi.importJson(content);
            toast('导入成功 collectionId=' + data.collectionId);
            closeImportModal();
            // 触发集合刷新
            $(document).trigger('apiTester:collectionChanged');
            if (typeof window.atReloadCollections === 'function') {
                window.atReloadCollections(data.collectionId);
            } else {
                // 退化: 整页刷新
                setTimeout(function () { location.reload(); }, 500);
            }
        } catch (ex) {
            toast('导入失败: ' + (ex.message || ex));
        }
    }

    // 文件选择 → 自动填充 textarea
    $(document).on('change', '#atImportJsonFile', function (e) {
        const file = e.target.files && e.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = function (ev) {
            $('#atImportJsonContent').val(ev.target.result || '');
        };
        reader.onerror = function () { toast('读取文件失败'); };
        reader.readAsText(file, 'UTF-8');
    });

    // ---------- 事件绑定 ----------
    $(document).on('click', '[data-action="export"]', function () { exportActiveCollection(); });
    $(document).on('click', '[data-action="import-json"]', function () { openImportModal(); });
    $(document).on('click', '[data-action="close-import-json"]', function () { closeImportModal(); });
    $(document).on('click', '#atImportJsonSubmit', function () { submitImport(); });
    // 点击遮罩关闭
    $(document).on('click', '#atImportJsonModal', function (e) {
        if (e.target && e.target.id === 'atImportJsonModal') closeImportModal();
    });

})(jQuery);
