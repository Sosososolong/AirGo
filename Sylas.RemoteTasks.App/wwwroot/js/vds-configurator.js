/**
 * VDS 可视化配置器
 * 用于创建和编辑VDS页面配置
 */
const VdsConfigurator = {
    // 当前编辑的页面ID（新建时为null）
    currentPageId: null,
    // 字段列表
    fields: [],
    // 模态框实例
    modal: null,
    fieldModal: null,

    /**
     * 初始化
     */
    init() {
        this.modal = new bootstrap.Modal(document.getElementById('vdsConfiguratorModal'));
        this.fieldModal = new bootstrap.Modal(document.getElementById('fieldEditModal'));
        
        // 让Modal可拖拽（使用 site.js 中的全局函数）
        makeModalDraggable(document.getElementById('vdsConfiguratorModal'));
        makeModalDraggable(document.getElementById('fieldEditModal'));
        
        // 监听Tab切换，同步JSON
        document.querySelectorAll('#configTabs button').forEach(btn => {
            btn.addEventListener('shown.bs.tab', (e) => {
                if (e.target.getAttribute('data-bs-target') === '#jsonTab') {
                    this.syncToJson();
                }
            });
        });
    },

    /**
     * 构建 reuseFrom 下拉选择框
     * 动态加载当前表格已配置的 dataSource 类型字段
     */
    buildReuseFromSelect(selectedValue = '') {
        // 从 fields 中筛选 dataSource 类型的字段（type 以 'dataSource' 开头）
        const dataSourceFields = this.fields.filter(f => f.type && f.type.startsWith('dataSource'));
        
        let optionsHtml = '<option value="">--选择数据源--</option>';
        dataSourceFields.forEach(f => {
            const selected = f.name === selectedValue ? 'selected' : '';
            optionsHtml += `<option value="${f.name}" ${selected}>${f.name}</option>`;
        });
        
        return `<select class="form-select form-select-sm" style="width:130px" data-field="reuseFrom">${optionsHtml}</select>`;
    },

    /**
     * 创建新页面
     */
    create() {
        this.currentPageId = null;
        this.fields = [];
        this.customActions = [];
        this.resetForm();
        this.renderFieldsList();
        this.renderCustomActionsList();
        this.modal.show();
    },

    /**
     * 编辑现有页面
     */
    async edit(pageId) {
        this.currentPageId = pageId;
        
        // 获取页面数据
        const response = await httpRequestDataAsync('/LowCode/Pages', null, 'POST', JSON.stringify({
            pageIndex: 1,
            pageSize: 1,
            filter: { filterItems: [{ fieldName: 'id', compareType: '=', value: pageId }] }
        }), 'application/json');
        
        if (!response || !response.data || response.data.length === 0) {
            showErrorBox('获取配置失败');
            return;
        }
        
        const page = response.data[0];
        this.loadPageData(page);
        this.modal.show();
    },

    /**
     * 加载页面数据到表单
     */
    loadPageData(page) {
        document.getElementById('cfg-name').value = page.name || '';
        document.getElementById('cfg-title').value = page.title || '';
        document.getElementById('cfg-description').value = page.description || '';
        document.getElementById('cfg-orderNo').value = page.orderNo || 0;
        document.getElementById('cfg-isEnabled').value = page.isEnabled ? 'true' : 'false';
        
        // 解析VDS配置
        let vdsConfig = {};
        try {
            vdsConfig = JSON.parse(page.vdsConfig || '{}');
        } catch (e) {
            console.error('解析VDS配置失败', e);
        }
        
        // 填充VDS配置
        document.getElementById('cfg-apiUrl').value = vdsConfig.apiUrl || '';
        document.getElementById('cfg-pageSize').value = vdsConfig.pageSize || 10;
        document.getElementById('cfg-idFieldName').value = vdsConfig.idFieldName || 'id';
        
        // modalSettings
        if (vdsConfig.modalSettings) {
            document.getElementById('cfg-modal-url').value = vdsConfig.modalSettings.url || '';
            document.getElementById('cfg-modal-method').value = vdsConfig.modalSettings.method || 'POST';
            document.getElementById('cfg-modal-updateUrl').value = vdsConfig.modalSettings.updateUrl || '';
            document.getElementById('cfg-modal-updateMethod').value = vdsConfig.modalSettings.updateMethod || 'POST';
        }
        
        // orderRules
        if (vdsConfig.orderRules && vdsConfig.orderRules.length > 0) {
            document.getElementById('cfg-orderField').value = vdsConfig.orderRules[0].fieldName || 'createTime';
            document.getElementById('cfg-orderAsc').value = vdsConfig.orderRules[0].isAsc ? 'true' : 'false';
        }
        
        // 字段列表
        this.fields = vdsConfig.ths || [];
        this.renderFieldsList();
        
        // 自定义操作
        this.customActions = vdsConfig.customActions || [];
        this.renderCustomActionsList();
        
        // 更新JSON
        document.getElementById('cfg-vdsConfigJson').value = JSON.stringify(vdsConfig, null, 2);
    },

    /**
     * 重置表单
     */
    resetForm() {
        document.getElementById('cfg-name').value = '';
        document.getElementById('cfg-title').value = '';
        document.getElementById('cfg-description').value = '';
        document.getElementById('cfg-orderNo').value = 0;
        document.getElementById('cfg-isEnabled').value = 'true';
        document.getElementById('cfg-apiUrl').value = '';
        document.getElementById('cfg-pageSize').value = 10;
        document.getElementById('cfg-idFieldName').value = 'id';
        document.getElementById('cfg-modal-url').value = '';
        document.getElementById('cfg-modal-method').value = 'POST';
        document.getElementById('cfg-modal-updateUrl').value = '';
        document.getElementById('cfg-modal-updateMethod').value = 'POST';
        document.getElementById('cfg-orderField').value = 'createTime';
        document.getElementById('cfg-orderAsc').value = 'false';
        document.getElementById('cfg-vdsConfigJson').value = '{}';
    },

    /**
     * 渲染字段列表
     */
    renderFieldsList() {
        const container = document.getElementById('fieldsList');
        if (this.fields.length === 0) {
            container.innerHTML = '<div class="text-center text-muted py-4">暂无字段，点击"添加字段"开始配置</div>';
            return;
        }
        
        let html = '';
        this.fields.forEach((field, index) => {
            const typeLabel = this.getFieldTypeLabel(field);
            const searchBadge = field.searchedByKeywords ? '<span class="badge bg-info ms-1">可搜索</span>' : '';
            const isButton = field.type === 'button';
            const typeBadgeClass = isButton ? 'bg-warning text-dark' : 'bg-secondary';
            
            html += `
                <div class="field-item d-flex align-items-center p-2 border-bottom" data-index="${index}" draggable="true">
                    <span class="me-2" style="cursor:move;">≡</span>
                    <span class="flex-grow-1">
                        <strong>${isButton ? '操作列' : field.name}</strong>
                        <span class="text-muted ms-2">${field.title}</span>
                        <span class="badge ${typeBadgeClass} ms-1">${typeLabel}</span>
                        ${searchBadge}
                    </span>
                    <button class="btn btn-sm btn-outline-primary me-1" onclick="VdsConfigurator.editField(${index})">编辑</button>
                    <button class="btn btn-sm btn-outline-danger" onclick="VdsConfigurator.removeField(${index})">删除</button>
                </div>
            `;
        });
        container.innerHTML = html;
        
        // 添加拖拽排序事件
        this.initDragSort();
    },

    /**
     * 获取字段类型显示标签
     */
    getFieldTypeLabel(field) {
        if (field.type === 'button') return '按钮';
        if (field.type === 'image') return '图片';
        if (field.type === 'media') return '媒体';
        if (field.type && field.type.startsWith('dataSource')) return '数据源';
        if (field.multiLines) return '多行文本';
        if (field.isNumber) return '数字';
        if (field.enumValus && field.enumValus.length > 0) return '枚举';
        return '文本';
    },

    /**
     * 初始化拖拽排序
     */
    initDragSort() {
        const container = document.getElementById('fieldsList');
        let draggedItem = null;
        
        container.querySelectorAll('.field-item').forEach(item => {
            item.addEventListener('dragstart', (e) => {
                draggedItem = item;
                item.classList.add('opacity-50');
            });
            
            item.addEventListener('dragend', () => {
                item.classList.remove('opacity-50');
                draggedItem = null;
            });
            
            item.addEventListener('dragover', (e) => {
                e.preventDefault();
                if (draggedItem && draggedItem !== item) {
                    const rect = item.getBoundingClientRect();
                    const midY = rect.top + rect.height / 2;
                    if (e.clientY < midY) {
                        container.insertBefore(draggedItem, item);
                    } else {
                        container.insertBefore(draggedItem, item.nextSibling);
                    }
                }
            });
        });
        
        container.addEventListener('dragend', () => {
            // 更新字段顺序
            const newFields = [];
            container.querySelectorAll('.field-item').forEach(item => {
                const index = parseInt(item.getAttribute('data-index'));
                newFields.push(this.fields[index]);
            });
            this.fields = newFields;
            this.renderFieldsList();
        });
    },

    /**
     * 添加字段
     */
    addField() {
        document.getElementById('field-index').value = '-1';
        document.getElementById('field-name').value = '';
        document.getElementById('field-title').value = '';
        document.getElementById('field-type').value = '';
        document.getElementById('field-searchedByKeywords').checked = false;
        document.getElementById('field-showPart').value = '';
        document.getElementById('field-align').value = '';
        document.getElementById('field-enumValus').value = '';
        document.getElementById('field-dataSourceApi').value = '';
        document.getElementById('field-displayField').value = '';
        document.getElementById('field-defaultValue').value = '';
        document.getElementById('field-tmpl').value = '';
        
        this.onFieldTypeChange();
        this.fieldModal.show();
    },

    /**
     * 编辑字段
     */
    editField(index) {
        const field = this.fields[index];
        document.getElementById('field-index').value = index;
        document.getElementById('field-name').value = field.name || '';
        document.getElementById('field-title').value = field.title || '';
        document.getElementById('field-searchedByKeywords').checked = field.searchedByKeywords || false;
        document.getElementById('field-showPart').value = field.showPart || '';
        document.getElementById('field-align').value = field.align || '';
        
        // 设置字段类型
        let type = '';
        if (field.type === 'button') type = 'button';
        else if (field.type === 'image') type = 'image';
        else if (field.type === 'media') type = 'media';
        else if (field.type && field.type.startsWith('dataSource')) type = 'dataSource';
        else if (field.multiLines) type = 'multiLines';
        else if (field.isNumber) type = 'number';
        else if (field.enumValus && field.enumValus.length > 0) type = 'enum';
        
        document.getElementById('field-type').value = type;
        
        // 特殊类型配置
        if (type === 'enum') {
            document.getElementById('field-enumValus').value = (field.enumValus || []).join('\n');
        }
        if (type === 'dataSource' && field.type) {
            const parts = field.type.split('|');
            parts.forEach(part => {
                if (part.startsWith('dataSourceApi=')) {
                    document.getElementById('field-dataSourceApi').value = part.replace('dataSourceApi=', '');
                }
                if (part.startsWith('displayField=')) {
                    document.getElementById('field-displayField').value = part.replace('displayField=', '');
                }
                if (part.startsWith('defaultValue=')) {
                    document.getElementById('field-defaultValue').value = part.replace('defaultValue=', '');
                }
            });
        }
        if (type === 'button') {
            document.getElementById('field-tmpl').value = field.tmpl || '';
        }
        
        this.onFieldTypeChange();
        this.fieldModal.show();
    },

    /**
     * 字段类型变化时显示/隐藏相关配置
     */
    onFieldTypeChange() {
        const type = document.getElementById('field-type').value;
        const nameGroup = document.getElementById('field-name-group');
        
        // 隐藏所有特殊配置
        document.getElementById('field-enum-group').classList.add('d-none');
        document.getElementById('field-dataSource-group').classList.add('d-none');
        document.getElementById('field-button-group').classList.add('d-none');
        document.getElementById('field-searchable-group').classList.remove('d-none');
        document.getElementById('field-display-group').classList.remove('d-none');
        if (nameGroup) nameGroup.classList.remove('d-none');
        
        // 根据类型显示配置
        if (type === 'enum') {
            document.getElementById('field-enum-group').classList.remove('d-none');
        } else if (type === 'dataSource') {
            document.getElementById('field-dataSource-group').classList.remove('d-none');
        } else if (type === 'button') {
            document.getElementById('field-button-group').classList.remove('d-none');
            document.getElementById('field-searchable-group').classList.add('d-none');
            document.getElementById('field-display-group').classList.add('d-none');
            if (nameGroup) nameGroup.classList.add('d-none');
            // 如果标题为空，设置默认值
            const titleInput = document.getElementById('field-title');
            if (!titleInput.value) titleInput.value = '操作';
            // 初始化按钮配置列表
            this.initButtonConfigs();
        }
    },

    // 按钮配置列表
    buttonConfigs: [],
    // 保存原有的模板内容
    existingTemplate: '',
    // 自定义操作配置列表
    customActions: [],

    /**
     * 初始化按钮配置
     */
    initButtonConfigs() {
        const existingTmpl = document.getElementById('field-tmpl').value;
        // 保存原有模板
        this.existingTemplate = existingTmpl || '';
        // 重置新添加的按钮配置
        this.buttonConfigs = [];
        this.renderButtonList();
    },

    /**
     * 添加预设按钮
     */
    addPresetButton(type) {
        const apiUrl = document.getElementById('cfg-apiUrl').value || '/api/data';
        let deleteUrl = apiUrl.replace(/\/?$/, '');
        if (deleteUrl.toLowerCase().includes('query') || deleteUrl.toLowerCase().includes('list') || deleteUrl.toLowerCase().includes('page')) {
            deleteUrl = deleteUrl.replace(/query|list|page/gi, 'delete');
        } else {
            deleteUrl = deleteUrl + '/delete';
        }
        
        let config = {};
        switch (type) {
            case 'edit':
                config = { type: 'edit', text: '编辑', style: 'primary' };
                break;
            case 'delete':
                config = { type: 'delete', text: '删除', style: 'danger', url: deleteUrl, confirmText: '确定要删除此记录吗？' };
                break;
            case 'view':
                config = { type: 'view', text: '查看', style: 'info', url: '/detail/{{id}}' };
                break;
            case 'custom':
                config = { 
                    type: 'custom', 
                    text: '操作', 
                    style: 'secondary', 
                    className: '',           // 用于事件绑定的class
                    executeUrl: '',          // 执行的API URL
                    executeMethod: 'POST',   // HTTP方法
                    needConfirm: false,      // 是否需要确认框
                    confirmText: '',         // 确认提示文字
                    useCallback: true        // 是否使用onDataAllLoaded回调绑定事件
                };
                break;
        }
        
        this.buttonConfigs.push(config);
        this.renderButtonList();
        this.generateButtonTemplate();
    },

    /**
     * 渲染按钮配置列表
     */
    renderButtonList() {
        const container = document.getElementById('button-list');
        if (!container) return;
        
        if (this.buttonConfigs.length === 0) {
            container.innerHTML = '';
            return;
        }
        
        let html = '';
        const typeLabels = { edit: '编辑表单', delete: '确认删除', view: '跳转页面', custom: '自定义' };
        this.buttonConfigs.forEach((btn, index) => {
            html += `
                <div class="d-flex align-items-center gap-2 p-2 border rounded mb-1 bg-white">
                    <span class="badge bg-${btn.style}">${btn.text}</span>
                    <input type="text" class="form-control form-control-sm" style="width:70px" value="${btn.text}" 
                        onchange="VdsConfigurator.updateButtonConfig(${index}, 'text', this.value)" title="按钮文字">
                    <select class="form-select form-select-sm" style="width:85px" 
                        onchange="VdsConfigurator.updateButtonConfig(${index}, 'style', this.value)" title="按钮样式">
                        <option value="primary" ${btn.style === 'primary' ? 'selected' : ''}>主要</option>
                        <option value="success" ${btn.style === 'success' ? 'selected' : ''}>成功</option>
                        <option value="danger" ${btn.style === 'danger' ? 'selected' : ''}>危险</option>
                        <option value="warning" ${btn.style === 'warning' ? 'selected' : ''}>警告</option>
                        <option value="info" ${btn.style === 'info' ? 'selected' : ''}>信息</option>
                        <option value="secondary" ${btn.style === 'secondary' ? 'selected' : ''}>次要</option>
                    </select>
                    <span class="text-muted small flex-grow-1">${typeLabels[btn.type] || ''}</span>
                    ${btn.type === 'delete' ? `
                        <input type="text" class="form-control form-control-sm" style="width:180px" placeholder="删除接口URL" value="${btn.url || ''}"
                            onchange="VdsConfigurator.updateButtonConfig(${index}, 'url', this.value)">
                    ` : ''}
                    ${btn.type === 'view' ? `
                        <input type="text" class="form-control form-control-sm" style="width:180px" placeholder="跳转URL" value="${btn.url || ''}"
                            onchange="VdsConfigurator.updateButtonConfig(${index}, 'url', this.value)">
                    ` : ''}
                    ${btn.type === 'custom' ? `
                        <input type="text" class="form-control form-control-sm" style="width:100px" placeholder="class名称" value="${btn.className || ''}"
                            onchange="VdsConfigurator.updateButtonConfig(${index}, 'className', this.value)" title="用于事件绑定的class">
                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="VdsConfigurator.showCustomButtonDetail(${index})" title="详细配置">⚙</button>
                    ` : ''}
                    <button type="button" class="btn btn-sm btn-outline-danger" onclick="VdsConfigurator.removeButton(${index})" title="删除">×</button>
                </div>
            `;
        });
        container.innerHTML = html;
    },

    /**
     * 更新按钮配置
     */
    updateButtonConfig(index, key, value) {
        if (this.buttonConfigs[index]) {
            this.buttonConfigs[index][key] = value;
            this.renderButtonList();
            this.generateButtonTemplate();
        }
    },

    /**
     * 删除按钮
     */
    removeButton(index) {
        this.buttonConfigs.splice(index, 1);
        this.renderButtonList();
        this.generateButtonTemplate();
    },

    /**
     * 显示自定义按钮详细配置弹窗
     */
    showCustomButtonDetail(index) {
        const btn = this.buttonConfigs[index];
        if (!btn) return;

        // 移除旧的modal
        const oldModal = document.querySelector('#custom-btn-modal');
        if (oldModal) oldModal.remove();

        const div = document.createElement('div');
        div.className = 'modal fade';
        div.id = 'custom-btn-modal';
        div.tabIndex = -1;
        div.innerHTML = `
            <div class="modal-dialog">
                <div class="modal-content" style="border: 2px solid #0d6efd; box-shadow: 0 0 20px rgba(13,110,253,0.4);">
                    <div class="modal-header">
                        <h5 class="modal-title">自定义按钮配置 - ${btn.text}</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="mb-3">
                            <label class="form-label">CSS Class名称 <small class="text-muted">(用于事件绑定)</small></label>
                            <input type="text" class="form-control form-control-sm" id="cbm-className" value="${btn.className || ''}" placeholder="如: restore, custom-action">
                        </div>
                        <div class="mb-3">
                            <label class="form-label">执行方式</label>
                            <div class="form-check">
                                <input class="form-check-input" type="radio" name="cbm-execType" id="cbm-useCallback" value="callback" ${btn.useCallback ? 'checked' : ''}>
                                <label class="form-check-label" for="cbm-useCallback">
                                    回调绑定 <small class="text-muted">(在onDataAllLoaded中绑定事件)</small>
                                </label>
                            </div>
                            <div class="form-check">
                                <input class="form-check-input" type="radio" name="cbm-execType" id="cbm-useExecute" value="execute" ${!btn.useCallback && btn.executeUrl ? 'checked' : ''}>
                                <label class="form-check-label" for="cbm-useExecute">
                                    直接执行API <small class="text-muted">(使用execute函数)</small>
                                </label>
                            </div>
                        </div>
                        <div class="mb-3 exec-api-group ${btn.useCallback ? 'd-none' : ''}">
                            <label class="form-label">API URL</label>
                            <input type="text" class="form-control form-control-sm" id="cbm-executeUrl" value="${btn.executeUrl || ''}" placeholder="如: /api/action?id={{id}}">
                        </div>
                        <div class="mb-3 exec-api-group ${btn.useCallback ? 'd-none' : ''}">
                            <label class="form-label">HTTP方法</label>
                            <select class="form-select form-select-sm" id="cbm-executeMethod">
                                <option value="POST" ${btn.executeMethod === 'POST' ? 'selected' : ''}>POST</option>
                                <option value="GET" ${btn.executeMethod === 'GET' ? 'selected' : ''}>GET</option>
                                <option value="PUT" ${btn.executeMethod === 'PUT' ? 'selected' : ''}>PUT</option>
                                <option value="DELETE" ${btn.executeMethod === 'DELETE' ? 'selected' : ''}>DELETE</option>
                            </select>
                        </div>
                        <div class="mb-3 exec-api-group ${btn.useCallback ? 'd-none' : ''}">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="cbm-needConfirm" ${btn.needConfirm ? 'checked' : ''}>
                                <label class="form-check-label" for="cbm-needConfirm">执行前需要确认</label>
                            </div>
                        </div>
                        <div class="mb-3 exec-api-group confirm-text-group ${btn.useCallback || !btn.needConfirm ? 'd-none' : ''}">
                            <label class="form-label">确认提示文字</label>
                            <input type="text" class="form-control form-control-sm" id="cbm-confirmText" value="${btn.confirmText || ''}" placeholder="确定要执行此操作吗？">
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                        <button type="button" class="btn btn-primary btn-sm" id="cbm-save">保存</button>
                    </div>
                </div>
            </div>`;
        
        document.body.appendChild(div);
        
        // 绑定事件
        const modal = new bootstrap.Modal(div);
        
        // 执行方式切换
        div.querySelectorAll('input[name="cbm-execType"]').forEach(radio => {
            radio.onchange = () => {
                const isCallback = div.querySelector('#cbm-useCallback').checked;
                div.querySelectorAll('.exec-api-group').forEach(el => {
                    el.classList.toggle('d-none', isCallback);
                });
            };
        });
        
        // 确认框切换
        div.querySelector('#cbm-needConfirm').onchange = function() {
            div.querySelector('.confirm-text-group').classList.toggle('d-none', !this.checked);
        };
        
        // 保存
        div.querySelector('#cbm-save').onclick = () => {
            btn.className = div.querySelector('#cbm-className').value;
            btn.useCallback = div.querySelector('#cbm-useCallback').checked;
            btn.executeUrl = div.querySelector('#cbm-executeUrl').value;
            btn.executeMethod = div.querySelector('#cbm-executeMethod').value;
            btn.needConfirm = div.querySelector('#cbm-needConfirm').checked;
            btn.confirmText = div.querySelector('#cbm-confirmText').value;
            
            this.renderButtonList();
            this.generateButtonTemplate();
            modal.hide();
        };
        
        makeModalDraggable(div);
        modal.show();
    },

    /**
     * 生成按钮模板HTML
     */
    generateButtonTemplate() {
        const newButtons = this.buttonConfigs.map(btn => {
            const btnClass = `btn btn-${btn.style} btn-sm`;
            switch (btn.type) {
                case 'edit':
                    return `<button type="button" class="${btnClass}" onclick="showAddPannel(tables['{{tableId}}'], {{id}})">${btn.text}</button>`;
                case 'delete':
                    return `<button type="button" class="${btnClass}" data-table-id="{{tableId}}" data-content="&quot;{{id}}&quot;" data-execute-url="${btn.url}" data-method="POST" onclick="showConfirmBox('${btn.confirmText}', () => execute(this))">${btn.text}</button>`;
                case 'view':
                    return `<button type="button" class="${btnClass}" onclick="window.open('${btn.url}', '_blank')">${btn.text}</button>`;
                case 'custom':
                    // 根据配置生成不同的模板
                    const classAttr = btn.className ? ` ${btn.className}` : '';
                    if (btn.useCallback) {
                        // 回调绑定模式：只生成带class的按钮，事件在onDataAllLoaded中绑定
                        return `<button type="button" class="${btnClass}${classAttr}" data-table-id="{{tableId}}" data-id="{{id}}">${btn.text}</button>`;
                    } else if (btn.executeUrl) {
                        // 直接执行API模式
                        const onclick = btn.needConfirm 
                            ? `showConfirmBox('${btn.confirmText || '确定要执行此操作吗？'}', () => execute(this))`
                            : `execute(this)`;
                        return `<button type="button" class="${btnClass}${classAttr}" data-table-id="{{tableId}}" data-content="&quot;{{id}}&quot;" data-execute-url="${btn.executeUrl}" data-method="${btn.executeMethod || 'POST'}" onclick="${onclick}">${btn.text}</button>`;
                    } else {
                        // 默认：只有class
                        return `<button type="button" class="${btnClass}${classAttr}" data-table-id="{{tableId}}" data-id="{{id}}">${btn.text}</button>`;
                    }
                default:
                    return '';
            }
        });
        
        // 将新按钮追加到原有模板后面
        const allButtons = [];
        if (this.existingTemplate.trim()) {
            allButtons.push(this.existingTemplate.trim());
        }
        allButtons.push(...newButtons);
        
        document.getElementById('field-tmpl').value = allButtons.join('\n');
    },

    // =====================================================
    // CustomActions 自定义操作配置
    // =====================================================

    /**
     * 添加自定义操作
     */
    addCustomAction() {
        const newAction = {
            className: '',
            modalTitle: '',
            executeUrl: '',
            executeMethod: 'POST',
            modalFields: [],
            dataContent: {}
        };
        this.customActions.push(newAction);
        this.renderCustomActionsList();
        // 立即编辑新添加的操作
        this.editCustomAction(this.customActions.length - 1);
    },

    /**
     * 编辑自定义操作
     */
    editCustomAction(index) {
        const action = this.customActions[index];
        if (!action) return;

        // 移除旧的 Modal
        const oldModal = document.querySelector('#custom-action-edit-modal');
        if (oldModal) oldModal.remove();

        // 构建 modalFields 列表 HTML
        let modalFieldsHtml = '';
        if (action.modalFields && action.modalFields.length > 0) {
            action.modalFields.forEach((field, i) => {
                modalFieldsHtml += `
                    <div class="d-flex gap-2 mb-2 align-items-center modal-field-row" data-index="${i}">
                        <input type="text" class="form-control form-control-sm" style="width:100px" value="${field.name || ''}" placeholder="字段名" data-field="name">
                        <input type="text" class="form-control form-control-sm" style="width:100px" value="${field.label || ''}" placeholder="标签" data-field="label">
                        <select class="form-select form-select-sm" style="width:100px" data-field="type">
                            <option value="text" ${field.type === 'text' ? 'selected' : ''}>文本</option>
                            <option value="textarea" ${field.type === 'textarea' ? 'selected' : ''}>多行文本</option>
                            <option value="dataSource" ${field.type === 'dataSource' ? 'selected' : ''}>dataSource</option>
                        </select>
                        ${this.buildReuseFromSelect(field.reuseFrom)}
                        <button type="button" class="btn btn-sm btn-outline-danger" onclick="VdsConfigurator.removeModalField(${index}, ${i})">×</button>
                    </div>
                `;
            });
        }

        // 构建 dataContent 列表 HTML
        let dataContentHtml = '';
        // 获取已配置的表单字段名称列表
        const formFieldNames = (action.modalFields || []).map(f => f.name).filter(n => n);
        if (action.dataContent) {
            Object.entries(action.dataContent).forEach(([key, value], i) => {
                dataContentHtml += `<div class="d-flex gap-2 mb-2 align-items-center data-content-row">${this.buildDataContentRowHtml(key, value, formFieldNames)}</div>`;
            });
        }

        const div = document.createElement('div');
        div.className = 'modal fade';
        div.id = 'custom-action-edit-modal';
        div.tabIndex = -1;
        div.innerHTML = `
            <div class="modal-dialog modal-lg">
                <div class="modal-content" style="border: 2px solid #0d6efd; box-shadow: 0 0 20px rgba(13,110,253,0.4);">
                    <div class="modal-header">
                        <h5 class="modal-title">编辑自定义操作</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row mb-3">
                            <div class="col-md-4">
                                <label class="form-label">CSS Class <small class="text-muted">(事件绑定)</small></label>
                                <input type="text" class="form-control form-control-sm" id="cae-className" value="${action.className || ''}" placeholder="如: restore">
                            </div>
                            <div class="col-md-5">
                                <label class="form-label">弹窗标题</label>
                                <input type="text" class="form-control form-control-sm" id="cae-modalTitle" value="${action.modalTitle || ''}" placeholder="如: 选择还原的数据库">
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">HTTP方法</label>
                                <select class="form-select form-select-sm" id="cae-executeMethod">
                                    <option value="POST" ${action.executeMethod === 'POST' ? 'selected' : ''}>POST</option>
                                    <option value="GET" ${action.executeMethod === 'GET' ? 'selected' : ''}>GET</option>
                                    <option value="PUT" ${action.executeMethod === 'PUT' ? 'selected' : ''}>PUT</option>
                                    <option value="DELETE" ${action.executeMethod === 'DELETE' ? 'selected' : ''}>DELETE</option>
                                </select>
                            </div>
                        </div>
                        <div class="mb-3">
                            <label class="form-label">执行 API URL</label>
                            <input type="text" class="form-control form-control-sm" id="cae-executeUrl" value="${action.executeUrl || ''}" placeholder="如: /Database/Restore">
                        </div>
                        
                        <hr>
                        <h6>弹窗表单字段 <button type="button" class="btn btn-sm btn-outline-primary ms-2" onclick="VdsConfigurator.addModalFieldRow(${index})">+ 添加</button></h6>
                        <div id="cae-modalFields">
                            ${modalFieldsHtml || '<div class="text-muted small">暂无表单字段</div>'}
                        </div>
                        
                        <hr>
                        <h6>请求参数 <button type="button" class="btn btn-sm btn-outline-primary ms-2" onclick="VdsConfigurator.addDataContentRow()">+ 添加</button></h6>
                        <div class="small text-muted mb-2">参数值可选择：当前行ID、上方已配置的表单字段、或手动输入固定值</div>
                        <div id="cae-dataContent">
                            ${dataContentHtml || '<div class="text-muted small">暂无请求参数</div>'}
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                        <button type="button" class="btn btn-primary btn-sm" id="cae-save">保存</button>
                    </div>
                </div>
            </div>`;

        document.body.appendChild(div);
        const modal = new bootstrap.Modal(div);
        this.currentEditingActionIndex = index;

        // 保存按钮事件
        document.getElementById('cae-save').onclick = () => {
            this.saveCustomAction(index);
            modal.hide();
        };

        makeModalDraggable(div);
        modal.show();
    },

    // 当前编辑的操作索引
    currentEditingActionIndex: -1,

    /**
     * 添加弹窗表单字段行
     */
    addModalFieldRow(actionIndex) {
        const container = document.getElementById('cae-modalFields');
        if (!container) return;
        
        // 清除占位文字
        const placeholder = container.querySelector('.text-muted');
        if (placeholder) placeholder.remove();
        
        const action = this.customActions[actionIndex];
        const newIndex = action.modalFields ? action.modalFields.length : 0;
        
        // 生成默认字段名
        const defaultFieldName = `field${newIndex + 1}`;
        
        const row = document.createElement('div');
        row.className = 'd-flex gap-2 mb-2 align-items-center modal-field-row';
        row.dataset.index = newIndex;
        row.dataset.fieldName = defaultFieldName; // 记录当前字段名用于关联
        row.innerHTML = `
            <input type="text" class="form-control form-control-sm" style="width:100px" value="${defaultFieldName}" placeholder="字段名" data-field="name" onchange="VdsConfigurator.onModalFieldNameChange(this)">
            <input type="text" class="form-control form-control-sm" style="width:100px" value="" placeholder="标签" data-field="label">
            <select class="form-select form-select-sm" style="width:100px" data-field="type">
                <option value="text" selected>文本</option>
                <option value="textarea">多行文本</option>
                <option value="dataSource">dataSource</option>
            </select>
            ${this.buildReuseFromSelect()}
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="VdsConfigurator.removeModalFieldRow(this)">×</button>
        `;
        container.appendChild(row);
        
        // 同时添加对应的请求参数行
        this.addLinkedDataContentRow(defaultFieldName);
    },

    /**
     * 添加与表单字段关联的请求参数行
     */
    addLinkedDataContentRow(fieldName) {
        const container = document.getElementById('cae-dataContent');
        if (!container) return;
        
        // 清除占位文字
        const placeholder = container.querySelector('.text-muted');
        if (placeholder) placeholder.remove();
        
        // 获取当前已配置的表单字段
        const formFieldNames = [];
        document.querySelectorAll('#cae-modalFields .modal-field-row').forEach(row => {
            const name = row.querySelector('[data-field="name"]')?.value;
            if (name) formFieldNames.push(name);
        });
        
        const row = document.createElement('div');
        row.className = 'd-flex gap-2 mb-2 align-items-center data-content-row';
        row.dataset.linkedField = fieldName; // 标记关联的表单字段
        row.innerHTML = this.buildDataContentRowHtml(fieldName, '$form', formFieldNames);
        container.appendChild(row);
    },

    /**
     * 表单字段名称变化时同步更新请求参数
     */
    onModalFieldNameChange(input) {
        const row = input.closest('.modal-field-row');
        const oldName = row.dataset.fieldName || '';
        const newName = input.value;
        
        // 更新记录
        row.dataset.fieldName = newName;
        
        // 查找关联的请求参数行并更新
        const linkedRow = document.querySelector(`.data-content-row[data-linked-field="${oldName}"]`);
        if (linkedRow) {
            linkedRow.dataset.linkedField = newName;
            // 更新参数名
            const keyInput = linkedRow.querySelector('[data-field="key"]');
            if (keyInput) keyInput.value = newName;
            // 更新下拉选项
            this.refreshDataContentSourceOptions();
        }
    },

    /**
     * 删除表单字段行，同时删除关联的请求参数行
     */
    removeModalFieldRow(btn) {
        const row = btn.closest('.modal-field-row');
        const fieldName = row.querySelector('[data-field="name"]')?.value;
        
        // 删除关联的请求参数行
        const linkedRow = document.querySelector(`.data-content-row[data-linked-field="${fieldName}"]`);
        if (linkedRow) linkedRow.remove();
        
        // 删除表单字段行
        row.remove();
        
        // 刷新请求参数的下拉选项
        this.refreshDataContentSourceOptions();
    },

    /**
     * 刷新所有请求参数行的下拉选项
     */
    refreshDataContentSourceOptions() {
        // 获取当前所有表单字段名
        const formFieldNames = [];
        document.querySelectorAll('#cae-modalFields .modal-field-row').forEach(row => {
            const name = row.querySelector('[data-field="name"]')?.value;
            if (name) formFieldNames.push(name);
        });
        
        // 更新每个请求参数行的下拉选项
        document.querySelectorAll('#cae-dataContent .data-content-row').forEach(row => {
            const select = row.querySelector('[data-field="source"]');
            if (!select) return;
            
            const currentValue = select.value;
            let options = '<option value="custom">手动输入</option>';
            options += '<option value="{{id}}">当前行ID</option>';
            formFieldNames.forEach(name => {
                options += `<option value="$form:${name}">表单: ${name}</option>`;
            });
            
            select.innerHTML = options;
            // 尝试恢复选中值
            if (currentValue && select.querySelector(`option[value="${currentValue}"]`)) {
                select.value = currentValue;
            }
        });
    },

    /**
     * 移除弹窗表单字段
     */
    removeModalField(actionIndex, fieldIndex) {
        const row = document.querySelector(`.modal-field-row[data-index="${fieldIndex}"]`);
        if (row) row.remove();
    },

    /**
     * 添加请求参数行
     */
    addDataContentRow() {
        const container = document.getElementById('cae-dataContent');
        if (!container) return;
        
        // 清除占位文字
        const placeholder = container.querySelector('.text-muted');
        if (placeholder) placeholder.remove();
        
        // 获取当前已配置的表单字段
        const formFieldNames = [];
        document.querySelectorAll('#cae-modalFields .modal-field-row').forEach(row => {
            const name = row.querySelector('[data-field="name"]')?.value;
            if (name) formFieldNames.push(name);
        });
        
        const row = document.createElement('div');
        row.className = 'd-flex gap-2 mb-2 align-items-center data-content-row';
        row.innerHTML = this.buildDataContentRowHtml('', '', formFieldNames);
        container.appendChild(row);
    },

    /**
     * 构建请求参数行 HTML
     */
    buildDataContentRowHtml(key, value, formFieldNames) {
        // 构建值来源下拉选项
        let valueSourceOptions = '<option value="custom">手动输入</option>';
        valueSourceOptions += '<option value="{{id}}">当前行ID</option>';
        formFieldNames.forEach(name => {
            valueSourceOptions += `<option value="$form:${name}">表单: ${name}</option>`;
        });
        
        // 判断当前值的来源
        let selectedSource = 'custom';
        let customValue = value;
        if (value === '$form' && formFieldNames.includes(key)) {
            selectedSource = `$form:${key}`;
            customValue = '';
        } else if (value && value.startsWith('{{') && value.endsWith('}}')) {
            selectedSource = value;
            customValue = '';
        }
        
        // 替换选中状态
        valueSourceOptions = valueSourceOptions.replace(`value="${selectedSource}"`, `value="${selectedSource}" selected`);
        
        return `
            <input type="text" class="form-control form-control-sm" style="width:120px" value="${key}" placeholder="参数名" data-field="key">
            <select class="form-select form-select-sm" style="width:130px" data-field="source" onchange="VdsConfigurator.onDataContentSourceChange(this)">
                ${valueSourceOptions}
            </select>
            <input type="text" class="form-control form-control-sm flex-grow-1 ${selectedSource !== 'custom' ? 'd-none' : ''}" value="${customValue}" placeholder="固定值或 {{xx}}" data-field="value">
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="this.closest('.data-content-row').remove()">×</button>
        `;
    },

    /**
     * 请求参数来源切换
     */
    onDataContentSourceChange(select) {
        const row = select.closest('.data-content-row');
        const valueInput = row.querySelector('[data-field="value"]');
        const keyInput = row.querySelector('[data-field="key"]');
        
        if (select.value === 'custom') {
            valueInput.classList.remove('d-none');
        } else {
            valueInput.classList.add('d-none');
            valueInput.value = '';
            
            // 如果选择了表单字段，自动填充参数名
            if (select.value.startsWith('$form:') && keyInput && !keyInput.value) {
                const fieldName = select.value.replace('$form:', '');
                keyInput.value = fieldName;
            }
            // 如果选择了当前行ID，参数名默认为id
            if (select.value === '{{id}}' && keyInput && !keyInput.value) {
                keyInput.value = 'id';
            }
        }
    },

    /**
     * 保存自定义操作配置
     */
    saveCustomAction(index) {
        const action = this.customActions[index];
        if (!action) return;

        // 基本信息
        action.className = document.getElementById('cae-className').value;
        action.modalTitle = document.getElementById('cae-modalTitle').value;
        action.executeUrl = document.getElementById('cae-executeUrl').value;
        action.executeMethod = document.getElementById('cae-executeMethod').value;

        // 收集 modalFields
        action.modalFields = [];
        document.querySelectorAll('#cae-modalFields .modal-field-row').forEach(row => {
            const name = row.querySelector('[data-field="name"]').value;
            if (name) {
                action.modalFields.push({
                    name: name,
                    label: row.querySelector('[data-field="label"]').value,
                    type: row.querySelector('[data-field="type"]').value,
                    reuseFrom: row.querySelector('[data-field="reuseFrom"]').value || undefined
                });
            }
        });

        // 收集 dataContent
        action.dataContent = {};
        document.querySelectorAll('#cae-dataContent .data-content-row').forEach(row => {
            const key = row.querySelector('[data-field="key"]').value;
            const source = row.querySelector('[data-field="source"]').value;
            const customValue = row.querySelector('[data-field="value"]').value;
            
            if (key) {
                if (source === 'custom') {
                    action.dataContent[key] = customValue;
                } else if (source.startsWith('$form:')) {
                    // 如果参数名和表单字段名相同，简化为 $form
                    const formFieldName = source.replace('$form:', '');
                    action.dataContent[key] = key === formFieldName ? '$form' : `$form:${formFieldName}`;
                } else {
                    // {{id}} 等
                    action.dataContent[key] = source;
                }
            }
        });

        this.renderCustomActionsList();
    },

    /**
     * 删除自定义操作
     */
    removeCustomAction(index) {
        this.customActions.splice(index, 1);
        this.renderCustomActionsList();
    },

    /**
     * 渲染自定义操作列表
     */
    renderCustomActionsList() {
        const container = document.getElementById('customActions-list');
        if (!container) return;

        if (this.customActions.length === 0) {
            container.innerHTML = '<div class="text-muted small p-2">暂无自定义操作，点击上方按钮添加</div>';
            return;
        }

        let html = '';
        this.customActions.forEach((action, index) => {
            html += `
                <div class="d-flex align-items-center gap-2 p-2 border rounded mb-1 bg-white">
                    <span class="badge bg-secondary">${action.className || '未命名'}</span>
                    <span class="flex-grow-1">${action.modalTitle || '操作'}</span>
                    <span class="text-muted small">${action.executeUrl || ''}</span>
                    <button type="button" class="btn btn-sm btn-outline-primary" onclick="VdsConfigurator.editCustomAction(${index})">编辑</button>
                    <button type="button" class="btn btn-sm btn-outline-danger" onclick="VdsConfigurator.removeCustomAction(${index})">删除</button>
                </div>
            `;
        });
        container.innerHTML = html;
    },

    /**
     * 保存字段
     */
    saveField() {
        const index = parseInt(document.getElementById('field-index').value);
        const type = document.getElementById('field-type').value;
        
        const field = {
            name: document.getElementById('field-name').value,
            title: document.getElementById('field-title').value
        };
        
        // 通用配置
        if (document.getElementById('field-searchedByKeywords').checked) {
            field.searchedByKeywords = true;
        }
        const showPart = document.getElementById('field-showPart').value;
        if (showPart) {
            field.showPart = parseInt(showPart);
        }
        const align = document.getElementById('field-align').value;
        if (align) {
            field.align = align;
        }
        
        // 类型特殊配置
        if (type === 'number') {
            field.isNumber = true;
        } else if (type === 'multiLines') {
            field.multiLines = true;
        } else if (type === 'enum') {
            const enumValues = document.getElementById('field-enumValus').value.split('\n').filter(v => v.trim());
            if (enumValues.length > 0) {
                field.enumValus = enumValues;
            }
        } else if (type === 'image') {
            field.type = 'image';
        } else if (type === 'media') {
            field.type = 'media';
        } else if (type === 'dataSource') {
            let typeStr = 'dataSource';
            const defaultValue = document.getElementById('field-defaultValue').value;
            const dataSourceApi = document.getElementById('field-dataSourceApi').value;
            const displayField = document.getElementById('field-displayField').value;
            if (defaultValue) typeStr += `|defaultValue=${defaultValue}`;
            if (dataSourceApi) typeStr += `|dataSourceApi=${dataSourceApi}`;
            if (displayField) typeStr += `|displayField=${displayField}`;
            field.type = typeStr;
        } else if (type === 'button') {
            field.type = 'button';
            field.name = '';
            // 只有通过预设按钮添加了配置时才重新生成，否则保留用户手动编辑的内容
            if (this.buttonConfigs.length > 0) {
                this.generateButtonTemplate();
            }
            field.tmpl = document.getElementById('field-tmpl').value;
        }
        
        // 更新或添加字段
        if (index >= 0) {
            this.fields[index] = field;
        } else {
            this.fields.push(field);
        }
        
        this.renderFieldsList();
        this.fieldModal.hide();
    },

    /**
     * 移除字段
     */
    removeField(index) {
        this.fields.splice(index, 1);
        this.renderFieldsList();
    },

    /**
     * 同步表单数据到JSON
     */
    syncToJson() {
        const vdsConfig = this.buildVdsConfig();
        document.getElementById('cfg-vdsConfigJson').value = JSON.stringify(vdsConfig, null, 2);
    },

    /**
     * 从JSON同步到表单
     */
    syncFromJson() {
        try {
            const json = document.getElementById('cfg-vdsConfigJson').value;
            const vdsConfig = JSON.parse(json);
            
            document.getElementById('cfg-apiUrl').value = vdsConfig.apiUrl || '';
            document.getElementById('cfg-pageSize').value = vdsConfig.pageSize || 10;
            document.getElementById('cfg-idFieldName').value = vdsConfig.idFieldName || 'id';
            
            if (vdsConfig.modalSettings) {
                document.getElementById('cfg-modal-url').value = vdsConfig.modalSettings.url || '';
                document.getElementById('cfg-modal-method').value = vdsConfig.modalSettings.method || 'POST';
                document.getElementById('cfg-modal-updateUrl').value = vdsConfig.modalSettings.updateUrl || '';
                document.getElementById('cfg-modal-updateMethod').value = vdsConfig.modalSettings.updateMethod || 'POST';
            }
            
            if (vdsConfig.orderRules && vdsConfig.orderRules.length > 0) {
                document.getElementById('cfg-orderField').value = vdsConfig.orderRules[0].fieldName || 'createTime';
                document.getElementById('cfg-orderAsc').value = vdsConfig.orderRules[0].isAsc ? 'true' : 'false';
            }
            
            this.fields = vdsConfig.ths || [];
            this.renderFieldsList();
            
            // 加载 customActions
            this.customActions = vdsConfig.customActions || [];
            this.renderCustomActionsList();
            
            showResultBox('应用成功');
        } catch (e) {
            showErrorBox('JSON格式错误: ' + e.message);
        }
    },

    /**
     * 格式化JSON
     */
    formatJson() {
        try {
            const json = document.getElementById('cfg-vdsConfigJson').value;
            const obj = JSON.parse(json);
            document.getElementById('cfg-vdsConfigJson').value = JSON.stringify(obj, null, 2);
        } catch (e) {
            showErrorBox('JSON格式错误: ' + e.message);
        }
    },

    /**
     * 构建VDS配置对象
     */
    buildVdsConfig() {
        const config = {
            apiUrl: document.getElementById('cfg-apiUrl').value,
            pageSize: parseInt(document.getElementById('cfg-pageSize').value) || 10,
            idFieldName: document.getElementById('cfg-idFieldName').value || 'id',
            ths: this.fields
        };
        
        // modalSettings
        const modalUrl = document.getElementById('cfg-modal-url').value;
        const modalUpdateUrl = document.getElementById('cfg-modal-updateUrl').value;
        if (modalUrl || modalUpdateUrl) {
            config.modalSettings = {
                url: modalUrl,
                method: document.getElementById('cfg-modal-method').value || 'POST',
                updateUrl: modalUpdateUrl,
                updateMethod: document.getElementById('cfg-modal-updateMethod').value || 'POST'
            };
        }
        
        // orderRules
        const orderField = document.getElementById('cfg-orderField').value;
        if (orderField) {
            config.orderRules = [{
                fieldName: orderField,
                isAsc: document.getElementById('cfg-orderAsc').value === 'true'
            }];
        }
        
        // customActions
        if (this.customActions && this.customActions.length > 0) {
            config.customActions = this.customActions;
        }
        
        return config;
    },

    /**
     * 保存配置
     */
    async save() {
        const name = document.getElementById('cfg-name').value.trim();
        const title = document.getElementById('cfg-title').value.trim();
        
        if (!name) {
            showErrorBox('请填写页面标识');
            return;
        }
        if (!title) {
            showErrorBox('请填写页面标题');
            return;
        }
        
        const vdsConfig = this.buildVdsConfig();
        if (!vdsConfig.apiUrl) {
            showErrorBox('请填写数据查询接口');
            return;
        }
        
        const formData = new FormData();
        formData.append('name', name);
        formData.append('title', title);
        formData.append('description', document.getElementById('cfg-description').value);
        formData.append('orderNo', document.getElementById('cfg-orderNo').value);
        formData.append('isEnabled', document.getElementById('cfg-isEnabled').value === 'true');
        formData.append('vdsConfig', JSON.stringify(vdsConfig));
        
        if (this.currentPageId) {
            formData.append('id', this.currentPageId);
        }
        
        const url = this.currentPageId ? '/LowCode/UpdatePage' : '/LowCode/AddPage';
        
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'authorization': `Bearer ${getAccessToken()}`
                },
                body: formData
            });
            
            const result = await response.json();
            if (result.succeed) {
                showResultBox(result);
                this.modal.hide();
                // 刷新列表
                tables['vdsPageTable'].loadData();
            } else {
                showErrorBox(result.message || '保存失败');
            }
        } catch (e) {
            showErrorBox('请求失败: ' + e.message);
        }
    }
};

// 页面加载完成后初始化（支持动态加载场景）
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        VdsConfigurator.init();
    });
} else {
    // DOM 已经准备好（动态加载场景）
    VdsConfigurator.init();
}

// 暴露到全局作用域（因为loadPage会将脚本设置为module类型）
window.VdsConfigurator = VdsConfigurator;
