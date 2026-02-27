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
     * 创建新页面
     */
    create() {
        this.currentPageId = null;
        this.fields = [];
        this.resetForm();
        this.renderFieldsList();
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
            
            html += `
                <div class="field-item d-flex align-items-center p-2 border-bottom" data-index="${index}" draggable="true">
                    <span class="me-2" style="cursor:move;">≡</span>
                    <span class="flex-grow-1">
                        <strong>${field.name || '(操作列)'}</strong>
                        <span class="text-muted ms-2">${field.title}</span>
                        <span class="badge bg-secondary ms-1">${typeLabel}</span>
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
        
        // 隐藏所有特殊配置
        document.getElementById('field-enum-group').classList.add('d-none');
        document.getElementById('field-dataSource-group').classList.add('d-none');
        document.getElementById('field-button-group').classList.add('d-none');
        document.getElementById('field-searchable-group').classList.remove('d-none');
        document.getElementById('field-display-group').classList.remove('d-none');
        
        // 根据类型显示配置
        if (type === 'enum') {
            document.getElementById('field-enum-group').classList.remove('d-none');
        } else if (type === 'dataSource') {
            document.getElementById('field-dataSource-group').classList.remove('d-none');
        } else if (type === 'button') {
            document.getElementById('field-button-group').classList.remove('d-none');
            document.getElementById('field-searchable-group').classList.add('d-none');
            document.getElementById('field-display-group').classList.add('d-none');
        }
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
                showResultBox(result.message || '保存成功');
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
