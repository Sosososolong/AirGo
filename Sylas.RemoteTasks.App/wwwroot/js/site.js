// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

/**
 * 让 Bootstrap Modal 可拖拽（通用函数）
 * @param {HTMLElement} modalEl - Modal 元素
 */
function makeModalDraggable(modalEl) {
    if (!modalEl) return;
    
    const dialog = modalEl.querySelector('.modal-dialog');
    const header = modalEl.querySelector('.modal-header');
    if (!dialog || !header) return;
    
    let isDragging = false;
    let startX, startY, initialX = 0, initialY = 0;
    let animationId = null;
    let currentX, currentY;
    
    // 设置拖拽样式和GPU加速
    header.style.cursor = 'move';
    header.style.userSelect = 'none';
    dialog.style.willChange = 'transform';
    
    const onMouseDown = (e) => {
        if (e.target.closest('.btn-close')) return;
        
        isDragging = true;
        startX = e.clientX;
        startY = e.clientY;
        
        // 禁用过渡动画，让拖拽即时响应
        dialog.style.transition = 'none';
        
        // 获取当前位置
        const transform = dialog.style.transform;
        if (transform) {
            const match = transform.match(/translate\(([\d.-]+)px,\s*([\d.-]+)px\)/);
            if (match) {
                initialX = parseFloat(match[1]);
                initialY = parseFloat(match[2]);
            }
        }
        
        document.body.style.cursor = 'move';
        e.preventDefault();
    };
    
    const onMouseMove = (e) => {
        if (!isDragging) return;
        
        currentX = initialX + e.clientX - startX;
        currentY = initialY + e.clientY - startY;
        
        // 使用 requestAnimationFrame 优化渲染
        if (!animationId) {
            animationId = requestAnimationFrame(() => {
                dialog.style.transform = `translate(${currentX}px, ${currentY}px)`;
                animationId = null;
            });
        }
    };
    
    const onMouseUp = () => {
        if (isDragging) {
            isDragging = false;
            initialX = currentX || initialX;
            initialY = currentY || initialY;
            document.body.style.cursor = '';
            
            // 恢复过渡动画
            dialog.style.transition = '';
            
            if (animationId) {
                cancelAnimationFrame(animationId);
                animationId = null;
            }
        }
    };
    
    header.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove, { passive: true });
    document.addEventListener('mouseup', onMouseUp);
    
    // Modal关闭时重置
    modalEl.addEventListener('hidden.bs.modal', () => {
        dialog.style.transform = '';
        dialog.style.willChange = '';
        initialX = 0;
        initialY = 0;
    });
}

const VIDEO_RE = /\.(mp4|mov|avi|mkv|webm|flv|wmv|m4v|3gp)$/i;
const AUDIO_RE = /\.(mp3|wav|flac|aac|ogg|m4a|wma|ape)$/i;
const IMAGE_RE = /\.(jpg|jpeg|png|gif|bmp|webp|svg)$/i;

const tables = {};
const dataSources = {};

/**
 * 
 * @param {String} apiUrl 查询api地址
 * @param {Number} pageIndex 当前页
 * @param {Number} pageSize 每页多少条数据
 * @param {String} tableId 数据表table标签的id属性值
 * @param {String} tableContainerSelector 数据表table标签的父元素选择器
 * @param {Array} ths 数据表对应的数据列的配置, 如果dataViewBuilder不为空, 则ths的作用为配置关键字搜索的字段
 * @param {String} idFieldName 主键字段名
 * @param {Array} filterItems 过滤项集合
 * @param {Array} data 初始化数据表的数据
 * @param {Function} onDataLoaded 当数据加载完毕执行的回调函数
 * @param {String} wrapper table标签的外部html
 * @param {Object} modalSettings 添加数据弹窗的配置
 * @param {Boolean} primaryKeyIsInt 主键是否是整型
 * @param {String} addButtonSelector 添加按钮的选择器, 不传将创建添加按钮
 * @param {Function} dataViewBuilder 不使用table标签展示数据时使用, 该函数根据传入的数据集返回展示数据的元素对象
 * @param {Object} orderRules 排序规则
 * apiUrl, pageIndex, pageSize, tableId, tableContainerSelector, ths, idFieldName, filterItems = null, data = null, onDataLoaded = undefined, wrapper = '', modalSettings = {}, primaryKeyIsInt = true, addButtonSelector = '', dataViewBuilder = null, orderRules = null
 */
async function createTable(customOptions) {
    const options = {
        apiUrl: '',
        pageIndex: 1,
        pageSize: 10,
        tableId: '',
        tableContainerSelector: '',
        ths: [],
        idFieldName: '',
        filterItems: [
            {
                fieldName: '',
                compareType: '',
                value: null
            }
        ],
        data: null,
        onDataLoaded: undefined,
        wrapper: '',
        modalSettings: {},
        primaryKeyIsInt: true,
        addButtonSelector: '',
        dataViewBuilder: null,
        orderRules: [{ fieldName: 'updateTime', isAsc: false }],
        onDataAllLoaded: undefined,
    }
    for (const key in customOptions) {
        if (Object.prototype.hasOwnProperty.call(customOptions, key)) {
            options[key] = customOptions[key];
        }
    }
    tables[options.tableId] = {
        apiUrl: options.apiUrl,
        tableId: options.tableId,
        tableContainerSelector: options.tableContainerSelector,
        primaryKeyIsInt: options.primaryKeyIsInt,
        pageIndex: options.pageIndex,
        pageSize: options.pageSize,
        totalPages: 0,
        orderRules: !options.orderRules ? [{ fieldName: 'updateTime', isAsc: false }] : options.orderRules,
        dataFilter: {
            filterItems: options.filterItems,
            keywords: {
                fields: [],
                value: ''
            }
        },
        onDataLoaded: options.onDataLoaded,
        wrapper: options.wrapper,
        formItemIds: [], // `${this.tableId}FormInput_${th.name}`
        formItemIdsForAddPannel: [], // 比formItems少一个Id字段的表单项dom的id
        formItemIdsMapper: {},
        modalSettings: options.modalSettings, // 添加数据面板
        ths: options.ths,
        dataSourceField: {
            // xxFieldName: [
            //     { id: 1, value: 'xxx' }
            // ]
        },
        modalId: '',
        modal: {}, // 当前table的bootstrap.Modal对象
        addOptions: {
            button: '',
            modalHtml: '',
        },
        tableForm: {
            formHtml: '',
            formHtmlFieldPk: '',
        },
        hasCustomDataViewBuilder: options.dataViewBuilder && typeof (options.dataViewBuilder) === 'function',
        onDataAllLoaded: options.onDataAllLoaded,
    }
    const targetTable = tables[options.tableId]

    if (options.addButtonSelector) {
        targetTable.addOptions.button = document.querySelector(options.addButtonSelector);
    }

    //if (filterItems) {
    //    targetTable.dataFilter.filterItems = filterItems;
    //}
    targetTable.dataFilter.keywords.fields = targetTable.ths.filter(th => th.searchedByKeywords).map(th => th.name);

    targetTable.renderBody = async function (data) {
        const tid = this.tableId
        this.renderedData = data;
        // BOOKMARK: sitejs 3 loadData -> renderBody 渲染数据到页面
        if (this.hasCustomDataViewBuilder) {
            let dataPannel = options.dataViewBuilder(data);
            dataPannel.id = tid;
            const t = document.querySelector(`#${tid}`);
            t.parentNode.replaceChild(dataPannel, t);
            return;
        }
        var tbody = $(`#${tid} tbody`);
        tbody.empty();
        for (let j = 0; j < data.length; j++) {
            var row = data[j];
            var tr = $('<tr>');
            // 设置行的 data-id 属性，用于 customActions 获取当前行 ID
            tr.attr('data-id', row[options.idFieldName]);

            for (let i = 0; i < this.ths.length; i++) {
                var th = this.ths[i]
                if (th.type === 'button') {
                    // 替换 {{id}} 为实际主键值
                    var buttonHtml = th.tmpl.replace(/{{id}}/g, row[options.idFieldName]);
                    // 替换 {{tableId}} 为表格ID
                    buttonHtml = buttonHtml.replace(/\{\{tableId\}\}/g, options.tableId);
                    // 替换所有其他字段
                    for (const key in row) {
                        const regex = new RegExp(`\\{\\{${key}\\}\\}`, 'g');
                        buttonHtml = buttonHtml.replace(regex, row[key] ?? '');
                    }
                    tr.append(`<td align="center">${buttonHtml}</td>`);
                }
                if (th.name) {
                    // 字段值
                    var tdValue = row[th.name];
                    if (th.title.indexOf('时间') > -1) {
                        tdValue = tdValue.replace('T', ' ');
                    }
                    if (th.formatter) {
                        tdValue = th.formatter(tdValue)
                    }
                    if (th.type && th.type.indexOf('dataSource') === 0) {
                        if (!this.dataSourceField || !this.dataSourceField[th.name]) {
                            await this.resolveDataSourceField(th);
                        }
                        for (let dataSourceIndex = 0; dataSourceIndex < this.dataSourceField[th.name].length; dataSourceIndex++) {
                            let dataSource = this.dataSourceField[th.name][dataSourceIndex];
                            // 字段值由id转为name
                            if (tdValue === dataSource.id) {
                                tdValue = dataSource.value;
                                break;
                            }
                        }
                    }
                    if (th.type && th.type === 'media') {
                        if (tdValue && tdValue.length > 0) {
                            let tagsHtml = '';
                            const mediaUrls = tdValue.split(';')
                            for (let k = 0; k < mediaUrls.length; k++) {
                                const itemUrl = mediaUrls[k];
                                if (VIDEO_RE.test(itemUrl)) {
                                    tagsHtml += `<video style="width:200px;" controls muted>
                                                   <source src="${itemUrl}" type="video/mp4">
                                                   Your browser does not support the video tag.
                                                 </video>`;
                                } else if (AUDIO_RE.test(itemUrl)) {
                                    tagsHtml += `<audio style="width:200px;" controls muted>
                                                       <source src="${itemUrl}" type="audio/mpeg">
                                                       Your browser does not support the audio tag.
                                                     </audio>`;
                                } else {
                                    tagsHtml += `<img style="width:100px;" src="${itemUrl}">`
                                }
                            }
                            tdValue = `<div style="display:flex;align-items:center;flex-wrap:wrap;">${tagsHtml}</div>`;
                        }
                    }

                    // 单元格只显示部分值
                    if (th.showPart && tdValue && tdValue.length > 12) {
                        tdValue = tdValue.substring(0, th.showPart) + '...'
                    }

                    const align = th.align ? ` align="${th.align}"` : ''
                    tr.append(`<td${align}>${tdValue}</td>`);
                }
            }
            tbody.append(tr);
        }
        for (var j = 0; j < data.length; j++) {
            var row = data[j];
            if (this.onDataLoaded) {
                await this.onDataLoaded(row);
            }
        }
        if (this.onDataAllLoaded) {
            this.onDataAllLoaded(data);
        }
        // 处理 customActions 自定义操作绑定
        processCustomActions(options);
    }

    // this指向的是window对象, 因为它的父作用域就是createTable()方法, createTable中的this就是window对象
    targetTable.test = () => console.log(this);

    targetTable.loadData = async function () {
        // 分页条
        var pagination = $(`#page-${this.tableId}`);

        if (options.data) {
            await this.renderBody(options.data);
            pagination.hide();
            data = null;
            return;
        }

        // 发送 AJAX 请求获取数据
        const method = 'POST';

        async function onSuccess(response) {
            if (!response) {
                showErrorBox('请求失败');
                return;
            }
            if (response && !response.data) {
                showErrorBox(response.message ?? '请求失败');
                return;
            }
            var data = response.data.data;
            // 暂时没用上
            var totalCount = response.data.count;
            targetTable.totalPages = response.data.totalPages;
            // 将数据添加到表格中
            await targetTable.renderBody(data);

            // 更新分页导航
            renderPageBar();

            // 记录Id主键字段是否是整型
            if (data.length > 0 && isNaN(data[0].id) && targetTable.primaryKeyIsInt) {
                targetTable.primaryKeyIsInt = false;
            }
        }
        function renderPageBar() {
            pagination.empty();
            const maxPage = 10;
            let firstPage;
            let lastPage;
            const halfPage = Math.floor(maxPage / 2);
            const rightPage = maxPage - halfPage - 1;
            const totalPage = targetTable.totalPages;

            if (totalPage > maxPage) {
                firstPage = this.pageIndex - halfPage;
                lastPage = this.pageIndex + rightPage;

                if (firstPage <= 0) {
                    // 需要向右偏移至1(即firstPage + rightOffset = 1), 计算偏移
                    const rightOffset = 1 - firstPage;

                    firstPage += rightOffset;
                    lastPage += rightOffset;
                    // 防止lastPage超出totalPage
                    lastPage = lastPage > totalPage ? totalPage : lastPage;
                }
                if (lastPage > totalPage) {
                    // 需要向左偏移至totalPage(即lastPage - leftOffset = totalPage)
                    const leftOffset = lastPage - totalPage;

                    lastPage -= leftOffset;
                    firstPage -= leftOffset;
                    // 防止firstPage小于1
                    firstPage = firstPage < 1 ? 1 : firstPage;
                }
            }
            else {
                firstPage = 1;
                lastPage = totalPage;
            }

            pagination.append('<li class="page-item ' + (targetTable.pageIndex == 1 ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex - 1) + '">Previous</a></li>');
            if (firstPage > 1) {
                pagination.append(`<li class="page-item"><a class="page-link" href="#" data-page="1">1</a></li> <li class="page-item disabled"><a class="page-link" href="#">...</a></li>`);
                // 额外显示第一页占据了两个位置, 所以第一页再向后偏移两个
                firstPage += 2;
            }
            for (var i = firstPage; i <= lastPage; i++) {
                pagination.append('<li class="page-item ' + (i == targetTable.pageIndex ? 'active' : '') + '"><a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>');
            }
            if (lastPage < totalPage) {
                pagination.append(`<li class="page-item disabled"><a class="page-link" href="#">...</a></li> <li class="page-item"><a class="page-link" href="#" data-page="${totalPage}">${totalPage}</a></li>`);
                // 额外显示最后一页占据了两个位置, 所以最后一页再向左偏移两个
                lastPage -= 2;
            }
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == targetTable.totalPages ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex + 1) + '">Next</a></li>');
        }
        
        var response = await httpRequestPagedDataAsync(this.apiUrl, method, this.pageIndex, this.pageSize, targetTable.dataFilter, this.orderRules, this.tableId);
        if (response) {
            await onSuccess(response);
        }
    };

    /**
     * 构建一个表单项的html
     * @param {Object} th 字段配置
     * @param {String} formItemId 表单项的id
     * @param {String} formItemComponent 表单组件的html, 如input/select等
     * @returns
     */
    targetTable.buildFormItemHtml = function (th, formItemId, formItemComponent) {
        let formItemHtml = `<div class="mb-3">
<label for="${formItemId}" class="col-form-label">${th.title}:</label>
${formItemComponent}
</div>`;

        // 追加到表单html中
        this.tableForm.formHtml += formItemHtml;

        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.3表单项 除Id字段外的其他表单项 - 记录表单项的元素Id
        if (this.formItemIds.indexOf(formItemId) === -1) {
            this.formItemIds.push(formItemId);
            this.formItemIdsForAddPannel.push(formItemId);
            this.formItemIdsMapper[formItemId] = th.name;
        }
    }

    targetTable.createModal = async function () {
        // BOOKMARK: 前端/frontend封装site.js - 创建模态框
        if (this.modalId && this.modal) {
            // 已经创建过了
            return;
        }

        const tableDataModalId = `modelForTable${this.tableId}`;
        this.modalId = tableDataModalId;

        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 2.表单项 除Id字段外的其他表单项
        for (var i = 0; i < this.ths.length; i++) {
            var th = this.ths[i]
            if (th.name) {
                if (th.name === 'createTime' || th.name === 'updateTime' || th.notShowInForm) {
                    continue;
                }
                let formItemId = `${this.tableId}FormInput_${th.name}`;
                if (!th.type) {
                    if (th.enumValus) {
                        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.1表单项 除Id字段外的其他表单项 - 拉选框/数据源/枚举
                        let dataSourceOptions = '';
                        th.enumValus.forEach(val => {
                            dataSourceOptions += `<option value="${val}">${val}</option>`
                        })
                        const formItemComponent = `<select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>`;
                        this.buildFormItemHtml(th, formItemId, formItemComponent);
                    } else {
                        const intputType = th.isNumber ? 'number' : 'text'
                        const formItemComponent = th.multiLines
                            ? `<textarea class="form-control form-control-sm" rows="5" placeholder="${th.title}" name="${th.name}" id="${formItemId}" ondblclick="textareaDbClicked(this)"></textarea>`
                            : `<input class="form-control form-control-sm" type="${intputType}" placeholder="${th.title}" name="${th.name}" id="${formItemId}">`
                        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.2表单项 除Id字段外的其他表单项 - 普通字段
                        this.buildFormItemHtml(th, formItemId, formItemComponent);
                    }
                } else if (th.type.indexOf('dataSource') === 0) {
                    let dataSourceOptions = await this.resolveDataSourceField(th);
                    const formItemComponent = `<select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>`;
                    this.buildFormItemHtml(th, formItemId, formItemComponent);
                } else if (th.type == 'image' || th.type == 'media') {
                    // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.2表单项 除Id字段外的其他表单项 - 图片字段
                    const formItemComponent = `<input type="hidden" name="${th.name}" id="${formItemId}" /><input class="form-control form-control-sm" type="file" multiple name="${th.name}_files" onchange="showMedias(event, '.img-preview-container')" id="${formItemId}_files"><div class="img-preview-container" style="display:flex;align-items:flex-end;flex-wrap:wrap;"></div>`;
                    this.buildFormItemHtml(th, formItemId, formItemComponent);
                    const fileInputId = `${formItemId}_files`;
                    if (this.formItemIds.indexOf(fileInputId) === -1) {
                        this.formItemIds.push(fileInputId);
                        this.formItemIdsForAddPannel.push(fileInputId);
                        this.formItemIdsMapper[fileInputId] = `${th.name}_files`;
                    }
                }
            }
        }
        let pkInputId = `${this.tableId}FormInput_id`;
        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 表单项 - Id字段对应的隐藏域
        this.tableForm.formHtmlFieldPk = `<input name="id" type="hidden" id="${pkInputId}" value="0" />`;
        this.tableForm.formHtml += this.tableForm.formHtmlFieldPk;
        if (this.formItemIds.indexOf(pkInputId) === -1) {
            this.formItemIds.push(pkInputId);
            this.formItemIndexOfPkField = this.formItemIds.indexOf(pkInputId);
            this.formItemIdsMapper[pkInputId] = 'id'; // 默认所有表的主键字段名都是id, 如果出现例外可以考虑给table添加一个属性pkFieldName存储主键的字段名
        }

        this.addOptions.modalHtml = `<div class="modal fade" tabindex="-1" id="${tableDataModalId}">
    <div class="modal-dialog">
    <div class="modal-content">
        <div class="modal-header">
        <h5 class="modal-title">添加数据</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
        </div>
        <div class="modal-body">
            <form id="${this.tableId}Form">
            ${this.tableForm.formHtml}
            </form>
        </div>
        <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">关闭</button>
        <button type="button" class="btn btn-primary" data-btn-type="submit" data-form-type="add" onclick="handleDataForm(tables['${this.tableId}'], this)">提交</button>
        </div>
    </div>
    </div>
</div>`;
    }

    /**
     * 处理下拉选择框的数据源
     * @param {any} thDataSource
     * @returns
     */
    targetTable.resolveDataSourceField = async function (thDataSource) {
        let dataSourceApi = /dataSourceApi=([^|]+)/.exec(thDataSource.type)[1];

        // 下拉框的数据源的显示字段, 如title, 就显示以数据源的title字段显示
        let displayField = 'id'
        let displayFieldPattern = /displayField=([^|]+)/.exec(thDataSource.type);
        if (displayFieldPattern && displayFieldPattern.length > 1) {
            displayField = displayFieldPattern[1]
        }

        // 下拉框的数据源过滤参数
        let bodyDataFilter = {};
        let bodyContentPattern = /body=([^|]+)/.exec(thDataSource.type);
        if (bodyContentPattern && bodyContentPattern.length > 1) {
            bodyDataFilter = JSON.parse(bodyContentPattern[1])
        }

        // 下拉框的默认值
        let defaultValue = '';
        let defaultValuePattern = /defaultValue=([^|]+)/.exec(thDataSource.type);
        if (defaultValuePattern && defaultValuePattern.length > 1) {
            defaultValue = defaultValuePattern[1]
        }

        let url = `${dataSourceApi}`;

        // 获取数据源数据(缓存)
        let dataSourceData = [];
        const dataSourceKeys = Object.keys(dataSources);
        const currentKey = thDataSource.type;
        if (dataSourceKeys.indexOf(currentKey) > -1) {
            dataSourceData = dataSources[currentKey]
        } else {
            let response = await httpRequestPagedDataAsync(url, 'POST', 1, 100, bodyDataFilter, []);
            if (response && response.code === 1) {
                dataSourceData = response.data.data;
                dataSources[currentKey] = dataSourceData;
            }
        }

        // 数据源选项集对应的下拉项集合
        let dataSourceOptions = `<option value="${defaultValue}">请选择</option>`;
        dataSourceData.forEach(row => {
            dataSourceOptions += `<option value="${row['id']}">${row[displayField]}</option>`

            if (!this.dataSourceField) {
                this.dataSourceField = {};
            }
            if (!this.dataSourceField[thDataSource.name]) {
                this.dataSourceField[thDataSource.name] = []
            }
            // HttpRequestProcessor的id和title(id: 1, value: 同步应用和流程数据 - zcmu)
            this.dataSourceField[thDataSource.name].push({ id: row['id'], value: row[displayField] })
        });
        this.dataSourceField[`${thDataSource.name}_options`] = dataSourceOptions;
        return dataSourceOptions;
    }

    targetTable.initSearchForm = function initSearchForm() {
        let searchFormHtmlBase = `<form id="search-form" style="margin-bottom:10px;">
    <div class="row g-3">
        <div class="col-sm-3">
            <input type="text" placeholder="关键字" class="form-control form-control-sm" id="search-input">
        </div>
        {{othersFormItems}}
        <div class="col-sm">
            <button type="submit" class="btn btn-outline-primary btn-sm">搜索</button>
            {{others}}
        </div>
    </div>
</form>`;
        
        let dataSourceFormItems = '';
        if (this.dataSourceField) {
            const dataSourceFields = Object.keys(this.dataSourceField);
            dataSourceFields.forEach(key => {
                if (key.endsWith('_options')) {
                    return;
                }
                const field = key;
                const data = this.dataSourceField[key];
                const dataOptionsHtml = this.dataSourceField[`${key}_options`];
                dataSourceFormItems += `<div class="col-sm-3">
                    <select class="form-control form-select-sm" aria-label="Default select" name="${field}" id="${field}">${dataOptionsHtml}</select>
                </div>`;
            })
        }
        
        searchFormHtmlBase = searchFormHtmlBase.replace('{{othersFormItems}}', dataSourceFormItems);
        if (this.modalSettings) {
            // BOOKMARK: 前端/frontend封装site.js - 创建模态框 1."添加"按钮(弹出表单)
            //data-bs-toggle="modal" data-bs-target="#${tableDataModalId}"
            if (!this.addOptions.button) {
                this.addOptions.button = `<button type="button" class="btn btn-primary btn-sm" onclick="showAddPannel(tables['${this.tableId}'])">添加</button>`;
                searchFormHtmlBase = searchFormHtmlBase.replace('{{others}}', this.addOptions.button);
            } else {
                this.addOptions.button.onclick = function(event) {
                    showAddPannel(targetTable);
                };
                searchFormHtmlBase = searchFormHtmlBase.replace('{{others}}', '');
            }
        }
        const searchForm = document.querySelector(this.tableContainerSelector).querySelector("#search-form");
        if (!searchForm) {
            $(this.tableContainerSelector).prepend(searchFormHtmlBase);
        }

        // 搜索表单提交事件
        $('#search-form').on('submit', function (event) {
            event.preventDefault();
            targetTable.dataFilter.keywords.value = $('#search-input').val().trim();
            
            const filterItems = [];
            document.querySelector('#search-form').querySelectorAll('input,select').forEach(item => {
                if (item.id !== 'search-input') {
                    // itemValue如果直接使用表单项的值(item.value)得到的都是字符串; 实际上id的值有可能是数字, 所以需要从dataSourceField中找到原始的数据项
                    const itemValue = targetTable.dataSourceField[item.name].find(x => x.id == item.value)?.id; // item.value;
                    if (itemValue) {
                        filterItems.push({
                            fieldName: item.name,
                            compareType: '=',
                            value: itemValue
                        });
                    }
                }
            });
            targetTable.dataFilter.filterItems = filterItems;
            targetTable.pageIndex = 1;
            targetTable.loadData();
        });

        let interval = 0;
        document.querySelector('#search-input').addEventListener('input', function (event) {
            if (interval) {
                clearTimeout(interval);
            }
            interval = setTimeout(() => {
                targetTable.dataFilter.keywords.value = $('#search-input').val().trim();
                targetTable.pageIndex = 1;
                targetTable.loadData();
            }, 500);
        });
    }

    /**
     * 初始化数据展示的容器(表格或者自定义), 数据分页栏和数据管理表单的html基本结构
     * @returns {void}
     */
    targetTable.initDataViewStructs = async function () {
        const tid = this.tableId
        if ($(`#${tid}`).length) {
            return;
        }

        if (this.modalSettings) {
            await this.createModal();
        }

        // BOOKMARK: sitejs 1. initDataViewStructs 构建数据展示的容器结构
        if (this.hasCustomDataViewBuilder) {
            $(this.tableContainerSelector).append(`<div id="${this.tableId}" style="margin-top:50px;"></div>${this.addOptions.modalHtml}`);
        } else {
            var tableHtml = `<div style="overflow:auto;">
        <table class="table table-sm table-hover table-bordered mt-3" style="border-color:#414243;" id="${this.tableId}">
            <thead>
                <tr>
                </tr>
            </thead>
            <tbody>
            </tbody>
        </table>
    </div>
    <nav aria-label="Page navigation">
        <ul class="pagination mt-3" id="page-${this.tableId}">
            <li class="page-item disabled">
                <a class="page-link" href="#" tabindex="-1" aria-disabled="true">Previous</a>
            </li>
            <li class="page-item active"><a class="page-link" href="#">1</a></li>
            <li class="page-item"><a class="page-link" href="#">2</a></li>
            <li class="page-item"><a class="page-link" href="#">3</a></li>
            <li class="page-item">
                <a class="page-link" href="#">Next</a>
            </li>
        </ul>
    </nav>
    ${this.addOptions.modalHtml}`;

            if (this.wrapper) {
                tableHtml = $(this.tableContainerSelector).append(this.wrapper.replace('{{tableHtml}}', tableHtml));
            }

            // 初始化数据表格结构
            $(this.tableContainerSelector).append(tableHtml);

            // 设置表头(及样式)
            this.ths.forEach(th => {
                if (th.width) {
                    $(`#${this.tableId} thead tr`).append(`<th width="${th.width}">${th.title}</th>`);
                } else {
                    $(`#${this.tableId} thead tr`).append(`<th>${th.title}</th>`);
                }
            });
        }

        // 获取对应的modal
        if (this.modalId) {
            // BOOKMARK: 前端/frontend封装site.js - table对象的 bootstrap.Modal对象
            this.modal = new bootstrap.Modal(`#${this.modalId}`);
        }

        // 分页导航点击事件
        $(`#page-${this.tableId}`).on('click', 'a[data-page]', function (event) {
            event.preventDefault();
            var page = parseInt($(this).data('page'));
            if (page >= 1 && page <= targetTable.totalPages) {
                targetTable.pageIndex = page;
                targetTable.loadData();
            }
        });
    }

    // BOOKMARK: 1. 渲染数据 - 开始
    targetTable.render = async function render() {
        // 初始化表格结构
        await this.initDataViewStructs();
        // 初始化表格搜索栏表单
        this.initSearchForm();
        // 加载第一页数据
        await this.loadData();
    }

    targetTable.searchKeywords = function() {
        // 获取表单元素
        var form = document.querySelector('#search-form');
        // 触发搜索表单提交事件重新查询数据
        form.submit();
    }

    await targetTable.render();
}

/**
 * 分页查询数据
 * @param {any} url
 * @param {any} method
 * @param {any} pageIndex
 * @param {any} pageSize
 * @param {any} dataFilter
 * @param {any} orderRules
 * @param {any} renderElementId
 * @param {any} finallyAction
 * @returns
 */
async function httpRequestPagedDataAsync(url, method, pageIndex, pageSize, dataFilter, orderRules, renderElementId, finallyAction) {
    let search = {
    };
    if (pageIndex) {
        search.pageIndex = pageIndex;
    }
    if (pageSize) {
        search.pageSize = pageSize;
    }
    if (dataFilter) {
        search.filter = dataFilter;
    }
    if (orderRules) {
        search.rules = orderRules;
    }
    try {
        let response = httpRequestAsync(url, document.querySelector(`#${renderElementId}`), method, JSON.stringify(search), 'application/json')
        return response;
    } catch (e) {
        if (e.status === 500) {
            alert('数据异常, 请联系系统管理员')
        } else {
            alert(e.textStatus)
        }
    } finally {
        if (finallyAction) {
            finallyAction();
        }
    }
}

const tokenKey = 'access_token';
const expiresTimeKey = 'access_token_expires_time';
function getAccessToken() {
    const token = localStorage.getItem(tokenKey);
    const expiresTime = localStorage.getItem(expiresTimeKey);
    if (!expiresTime || new Date() >= new Date(expiresTime)) {
        localStorage.removeItem("tokenKey");
        localStorage.removeItem("expiresTimeKey");
        return '';
    }
    return token;
}
function clearAccessToken() {
    localStorage.removeItem(tokenKey);
    localStorage.removeItem(expiresTimeKey);
}

async function httpRequestAsync(url, spinnerEle = null, method = 'POST', body = '', contentType = '') {
    let o;
    try {
        if (spinnerEle) {
            //showSpinner(spinnerEle);
            o = addOverlay(spinnerEle);
        }
        const accessToken = getAccessToken();
        if (!accessToken) {
            showWarningBox('身份已过期, 请重新登录', () => location.href = `/Home/Login?redirect_path=${location.pathname}`);
            return null;
        }
        const headers = {
            'X-Requested-With': 'XMLHttpRequest',
            'authorization': `Bearer ${accessToken}`
        }
        if (contentType) {
            headers['Content-Type'] = contentType
        }
        const response = await fetch(url, {
            method: method,
            headers: headers,
            body: body
        })

        if (!response.ok) {
            if (response.status === 401) {
                showWarningBox('身份已过期, 请重新登录', () => location.href = `/Home/Login?redirect_path=${location.pathname}`);
                return null;
            }
            else if (response.status === 404) {
                showErrorBox('接口不存在, 请确认请求方式和参数');
                return null;
            } else {
                showErrorBox(`请求异常:${response.statusText}`);
            }
        }

        //var rspJson = await response.json();
        var rspJson = await response.text();
        var rspJson = JSON.parse(rspJson);

        return rspJson;
    } catch (e) {
        showErrorBox(e.message);
        return null;
    } finally {
        if (spinnerEle) {
            //closeSpinner(spinnerEle);
            if (o) {
                removeOverlay(o);
            }
        }
    };
}

const errorHandlerType = {
    showError: 0,
    returnErrorMessage: 1
}
/**
 * 根据平台的接口响应数据的格式, 获取请求数据(根据自定义协议如code值判断请求是否成功, 失败情况下显示错误提示框或者返回错误信息)
 * @param {any} url
 * @param {any} spinnerEle
 * @param {any} method
 * @param {any} body
 * @param {any} contentType
 * @returns
 */
async function httpRequestDataAsync(url, spinnerEle = null, method = 'POST', body = '', contentType = 'application/json', errorHandlerTypeVal = 0) {
    var response = await httpRequestAsync(url, spinnerEle, method, body, contentType);
    if (response) {
        if (response.code === 1) {
            return response.data;
        } else {
            if (errorHandlerTypeVal === errorHandlerType.returnErrorMessage) {
                return `<span class="text-warning">${response.message}</span>`;
            } else {
                showErrorBox(response.message ? response.message : "请求失败");
            }
        }   
    }
    return null;
}

function showAddPannel(table) {
    let submitButton = document.querySelector(`#${table.modalId} button[data-btn-type="submit"]`);
    submitButton.setAttribute("data-form-type", "add");

    let modalTitle = document.querySelector(`#${table.modalId} .modal-title`);
    modalTitle.innerHTML = modalTitle.innerHTML.replace("更新", "添加");
    showModal(table);
}

function showModal(table) {
    table.modal.show();
}

/**
 * 删除formData中的空值字段; 将缓存的文件添加到formData中
 * @param {any} formData
 */
function checkFormData(formData) {
    const keys = [...formData.keys()]; // 获取所有字段的键
    for (const key of keys) {
        if (formData.get(key) === null) { // 如果字段值为空
            formData.delete(key); // 删除该字段
        }
    }

    // 遍历 cachedFiles 对象的每个属性, 添加缓存的文件到 FormData 对象中
    for (var key in cachedFiles) {
        if (cachedFiles.hasOwnProperty(key)) {
            var files = cachedFiles[key]; // 获取当前属性的文件数组
            formData.delete(key);
            for (var i = 0; i < files.length; i++) {
                formData.append(key, files[i]); // 将文件添加到 FormData 对象中
            }
        }
    }
}

/**
 * 提交表单数据, 提交的接口地址和请求方法由触发事件的元素上的自定义属性值决定
 */
async function handleDataForm(table, eventTrigger) {
    // handleType为"add"或者"update"
    let handleType = eventTrigger.getAttribute("data-form-type");
    let url = handleType === "add" ? table.modalSettings.url : table.modalSettings.updateUrl;
    let method = handleType === "add" ? table.modalSettings.method : table.modalSettings.updateMethod;

    let response = null;
    showSpinner();

    const currentForm = document.querySelector(`#${table.tableId}Form`);
    let showMessage = null;

    if (table.onDataReloading && typeof table.onDataReloading === 'function') {
        table.onDataReloading();
    }
    if (currentForm.querySelector('input[type="file"]')) {
        var formData = new FormData(currentForm);
        checkFormData(formData);

        const accessToken = getAccessToken();
        if (!accessToken) {
            closeSpinner();
            showWarningBox('身份已过期, 请重新登录', () => location.href = `/Home/Login?redirect_path=${location.pathname}`);
            return null;
        }
        await httpRequestAsync(url, null, method, formData)
            .then(data => {
                if (data) {
                    showMessage = () => showResultBox(data)
                }
            })
            .catch(error => {
                if (e.status === 500) {
                    showMessage = () => showErrorBox('接口异常, 请联系系统管理员')
                } else if (e.status === 404) {
                    showMessage = () => showErrorBox(`接口不存在: ${url}`)
                } else {
                    showMessage = () => showErrorBox(e.textStatus)
                }
                console.log(e)
            })
            .finally(() => {
                closeSpinner()
                showMessage()
            });
    } else {
        // 需要提交的数据对应的所有表单项(添加时不需要Id字段, 如果带上了值为""的Id字段, 会因为转为int类型失败从而导致参数自动绑定失败)
        let formItemIds = handleType === "add" ? table.formItemIdsForAddPannel : table.formItemIds;

        let data = getFormData(formItemIds);
        let dataJsonString = JSON.stringify(data);

        try {
            response = await httpRequestAsync(url, null, method, dataJsonString, 'application/json')
            showMessage = () => showResultBox(response);
        } catch (e) {
            if (e.status === 500) {
                showMessage = () => showErrorBox('接口异常, 请联系系统管理员')
            } else if (e.status === 404) {
                showMessage = () => showErrorBox(`接口不存在: ${url}`)
            } else {
                showMessage = () => showErrorBox(e.textStatus);
            }
            console.log(e);
        } finally {
            closeSpinner();
            showMessage();
        }
    }
    
    function showResultBox(response) {
        if (response && (response.code === 1 || response.succeed)) {
            table.modal.hide();
            showMsgBox('操作成功', reloadTableData);

            function reloadTableData() {
                table.loadData();
                if (table.onDataReloaded && typeof table.onDataReloaded === 'function') {
                    setTimeout(table.onDataReloaded, 500)
                }
            }
        } else {
            if (response) {
                let errMsg = response.errMsg ?? response.message;
                if (!errMsg && response.data) {
                    errMsg = response.data instanceof Array ? response.data.join('\n') : response.data.toString();
                }
                if (!errMsg) {
                    errMsg = "操作异常";
                }
                showErrorBox(errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
            }
        }
    }
}

function showResultBox(response, table) {
    if (!response || response.code === 1 || response.succeed) {
        showMsgBox('操作成功', () => {
            if (table) {
                table.loadData();
            }
        });
    } else {
        let errMsg = response.errMsg;
        if (!errMsg && response.data) {
            errMsg = response.data instanceof Array ? response.data.join('\n') : response.data.toString();
        }
        if (!errMsg) {
            errMsg = "操作异常";
        }
        showErrorBox(errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}

/**
 * 获取表单/模态框提交的数据
 */
function getFormData(formItemIds) {
    var formData = {};
    for (var i = 0; i < formItemIds.length; i++) {
        let formItemId = formItemIds[i];
        let formItemVal = $(`#${formItemId}`).val();
        if (formItemVal === "true") {
            formItemVal = true;
        }
        if (formItemVal == "false") {
            formItemVal = false;
        }
        let name = $(`#${formItemId}`).prop('name');
        formData[name] = formItemVal;
    }
    return formData;
}

/**
 * 显示更新数据面板: 分页查询接口, 使用id作为条件查询出当前数据, 再把当前数据的属性值复制到对应的表单项上
 * @param {any} eventTrigger 触发事件的元素, 可以获取元素上的自定义属性值
 * @param {Function} onDataReloading 重新加载数据前需要执行的逻辑
 * @param {Function} onDataReloaded 重新加载数据后需要执行的逻辑
 */
async function showUpdatePannel(eventTrigger, onDataReloading, onDataReloaded) {
    let tableId = eventTrigger.getAttribute('data-table-id');
    let table = tables[tableId];
    table.onDataReloading = onDataReloading;
    table.onDataReloaded = onDataReloaded;
    const dataIdStr = eventTrigger.getAttribute('data-id');
    let dataId = table.primaryKeyIsInt ? Number(dataIdStr) : dataIdStr;
    let fetchUrl = eventTrigger.getAttribute('data-fetch-url');
    let method = eventTrigger.getAttribute('data-method');

    let findByIdFilter = {
        filterItems: [
            {
                fieldName: 'id',
                compareType: '=',
                value: dataId
            }
        ],
        keywords: {
            fields: [],
            value: ''
        }
    };
    var fetchedData = await httpRequestPagedDataAsync(fetchUrl, method, 1, 1, findByIdFilter, []);
    if (fetchedData && fetchedData.data) {
        await table.createModal();
        let record = fetchedData.data.data[0];
        table.formItemIds.forEach(formItemId => {
            let formItem = document.querySelector(`#${formItemId}`);

            let field = table.formItemIdsMapper[formItemId];
            let fieldValue;
            try {
                if (formItem.type === 'file') {
                    cachedFiles[field] = [];
                    field = field.replace('_files', '');
                }
                fieldValue = record[field] ? record[field].toString() : '';
            } catch (e) {
                console.log(e);
            }
            if (formItem.type === 'file') {
                // 直接使用imageUrl字段的图片相对路径显示所有图片
                const imgContainer = document.querySelector('.img-preview-container');
                imgContainer.innerHTML = '';
                if (fieldValue) {
                    formItem.value = '';
                    fieldValue.split(';').forEach(function (url) {
                        if (url) {
                            showMedia(imgContainer, url);
                        }
                    });
                }
            } else {
                formItem.value = fieldValue;
            }
        })

        let submitButton = document.querySelector(`#${table.modalId} button[data-btn-type="submit"]`);
        submitButton.setAttribute("data-form-type", "update");

        let modalTitle = document.querySelector(`#${table.modalId} .modal-title`);
        modalTitle.innerHTML = modalTitle.innerHTML.replace("添加", "更新");
        showModal(table);
    }
}

async function deleteData(eventTrigger) {
    let tableId = eventTrigger.getAttribute('data-table-id');
    let table = tables[tableId];
    let dataId = Number(eventTrigger.getAttribute('data-id'));

    let url = eventTrigger.getAttribute('data-delete-url');
    let method = eventTrigger.getAttribute('data-method');
    let response = null;
    showSpinner();
    try {
        response = await $.ajax({
            url: url,
            method: method,
            data: "\"" + dataId + "\"",
            contentType: 'application/json',
            dataType: 'json',
        });
    } catch (e) {
        showErrorBox('操作失败');
        console.log(e);
    } finally {
        closeSpinner();
    }

    if (response && response.succeed) {
        window.table = table;
        showMsgBox('操作成功', () => table.loadData());
    } else {
        showErrorBox(response.message, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}
/**
 * 请求一个api执行对应的操作
 * @param {any} eventTrigger 包含请求信息的触发按钮或对象
 * @param {any} callback 回调函数
 * @param {any} useSpinner 是否启用spinner, 是则显示加载动画
 * @returns
 */
async function execute(eventTrigger, callback = null, useSpinner = true, showBox) {
    const isEle = eventTrigger instanceof HTMLElement;
    let tableId = isEle ? eventTrigger.getAttribute('data-table-id') : eventTrigger['dataTableId'];
    let table = tableId ? tables[tableId] : null;
    let dataContent = isEle ? eventTrigger.getAttribute('data-content') : eventTrigger['dataContent'];
    if (dataContent && typeof(dataContent) === 'string' && dataContent.indexOf('formItemIds') > -1) {
        const regex = /\s*formItemIds\s*:\s*/;
        const formItemIds = dataContent.replace(regex, '');
        const formItemIdArr = formItemIds.split(';');
        const dataContentObj = {};
        // 使用formItemIdArr对应的表单项name和value, 构建dataContentObj对象
        formItemIdArr.forEach(formItemId => {
            if (formItemId) {
                const formItem = document.querySelector(`#${formItemId}`);
                dataContentObj[formItem.name] = formItem.value;
            }
        });
        dataContent = JSON.stringify(dataContentObj);
    }

    let url = isEle ? eventTrigger.getAttribute('data-execute-url') : eventTrigger['dataExecuteUrl'];
    let method = isEle ? eventTrigger.getAttribute('data-method') : eventTrigger['dataMethod'];
    let response = null;
    const spinnerEle = useSpinner ? (isEle ? eventTrigger : eventTrigger['trigger']) : null;
    const contentType = typeof (dataContent) === 'object' ? null : 'application/json'
    response = await httpRequestAsync(url, spinnerEle, method, dataContent, contentType);
    if (response) {
        if (callback) {
            callback(response)
        }

        if (showBox) {
            showResultBox(response, table)
        } else {
            // 不显示的情况下, 如果触发元素带了tableid那么就刷新data
            if (table) {
                await table.loadData();
            }
        }
    }
}

let cachedFiles = {};
function showMedias(event, imgContainer) {
    var files = event.target.files;
    var filesName = event.target.name;
    //const allImg = document.querySelectorAll('.img-preview');
    //if (allImg) {
    //    allImg.forEach(x => x.remove());
    //}
    // 遍历预览所有图片
    for (var i = 0; i < files.length; i++) {
        const file = files[i];
        // file.type: video/mp4
        if (file.type.indexOf('image') === 0 && file.type.indexOf('video') === 0 ) {
            continue;
        }
        // 缓存所有图片
        const urlsField = filesName.replace('_files', '');
        const imgField = document.querySelector(`input[name="${urlsField}"]`);
        const urls = imgField.value;
        if (!cachedFiles[filesName]) {
            cachedFiles[filesName] = [];
        }
        if (cachedFiles[filesName].findIndex(x => x.name === file.name && x.lastModified === file.lastModified && x.size === file.size) === -1 && urls.indexOf(file.name) === -1) {
            // 1. 给"urlsField"添加文件名
            imgField.value = imgField.value ? `${imgField.value};${file.name}` : file.name;

            // 2. 给缓存添加文件对象
            cachedFiles[filesName].push(file);
            showMedia(imgContainer, file);
        }
    }
}

function showMedia(mediaContainer, mediaFileObj) {
    let div = document.createElement('div');
    div.style.display = 'inline-block';
    div.style.position = 'relative';
    div.style.margin = '5px 5px 0 0';

    let mediaEle;
    const mediaTypeIsUri = typeof mediaFileObj === 'string';
    if (mediaFileObj.type?.indexOf('video') > -1 || (mediaTypeIsUri && VIDEO_RE.test(mediaFileObj))) {
        // 视频对象
        mediaEle = document.createElement('video');
        // 只设置宽度, 高度自适应
        mediaEle.width = 200;
        mediaEle.controls = true;               // 显示控制条
        mediaEle.muted = true;                  // 按需静音，自动播放更友好
        mediaEle.classList.add('img-preview');
        // 统一处理：可能是 File/Blob 或普通 URL 字符串
        if (mediaTypeIsUri) {
            mediaEle.src = mediaFileObj;
        } else {
            mediaEle.src = URL.createObjectURL(mediaFileObj);
            mediaEle.setAttribute('data-filename', mediaFileObj.name);
            mediaEle.onloadeddata = () => URL.revokeObjectURL(mediaEle.src);   
        }
    } else if (mediaFileObj.type?.indexOf('image') > -1 || (mediaTypeIsUri && IMAGE_RE.test(mediaFileObj))) {
        mediaEle = document.createElement('img');
        // 只设置宽度, 高度自适应
        mediaEle.width = 100;
        // 添加class
        mediaEle.classList.add('img-preview');
        if (mediaTypeIsUri) {
            mediaEle.src = mediaFileObj;
        } else {
            mediaEle.src = URL.createObjectURL(mediaFileObj);
            mediaEle.setAttribute('data-filename', mediaFileObj.name);
            mediaEle.onload = function () {
                URL.revokeObjectURL(mediaEle.src); // 释放内存
            };
        }
    } else if (mediaFileObj.type?.indexOf('audio') > -1 || (mediaTypeIsUri && AUDIO_RE.test(mediaFileObj))) {
        mediaEle = document.createElement('img');
        mediaEle = document.createElement('video');
        // 只设置宽度, 高度自适应
        mediaEle.width = 100;
        mediaEle.controls = true;
        mediaEle.muted = true;
        if (mediaFileObj) {
            mediaEle.src = mediaFileObj;
        } else {
            mediaEle.src = URL.createObjectURL(mediaFileObj);
            mediaEle.onloadeddata = () => URL.revokeObjectURL(mediaEle.src);
        }
    }

    if (mediaEle) {
        div.appendChild(mediaEle);
    }

    if (typeof (mediaContainer) === 'object') {
        mediaContainer.appendChild(div);
    } else {
        document.querySelector(mediaContainer).appendChild(div);
    }

    // 删除按钮
    div.innerHTML += `<div style="width:100%;border-radius:20px;position:absolute;bottom:0;right:0;cursor:pointer;padding:3px;font-size:18px;line-height:15px;text-align:center;background:rgba(255,0,0,0.3);color:#fff;backdrop-filter: blur(5px);" onclick="deleteImg(this)">-</div>`;
}
function deleteImg(node) {
    const imgUrl = node.previousElementSibling.getAttribute('src');
    // 遍历 cachedFiles 对象的每个属性, 更新字段所存的Url信息
    for (var key in cachedFiles) {
        // key: imageUrl_files; imgField: imageUrl
        const imgField = key.replace('_files', '');
        if (cachedFiles.hasOwnProperty(key)) {
            if (imgUrl.indexOf('blob:') === -1) {
                const imgInput = document.querySelector(`input[name="${imgField}"]`);
                imgInput.value = imgInput.value.replace(imgUrl + ';', '').replace(imgUrl, '');
            } else {
                // 删除缓存的文件
                const files = cachedFiles[key];
                // imgName: xxx.png
                const imgName = node.previousElementSibling.getAttribute('data-filename');
                const index = files.findIndex(x => x.name === imgName);
                if (index > -1) {
                    files.splice(index, 1);
                }

                const hiddenInput = document.querySelector(`input[name="${imgField}"]`);
                if (hiddenInput.value === imgName) {
                    hiddenInput.value = '';
                }
            }
        }
    }

    // 删除图片元素
    node.parentNode.remove();
}
function textareaDbClicked(ele) {
    ele.closest('.modal-dialog').classList.add('modal-fullscreen');
    // 添加一个返回原始大小的按钮
    ele.insertAdjacentHTML('afterend', `<button type="button" class="btn btn-primary mt-3" onclick="document.querySelector('#${ele.id}').closest('.modal-dialog').classList.remove('modal-fullscreen');this.remove()">返回</button>`);
}

let inputingInterval = 0;
/**
 * 输入框输入事件, 延迟触发事件处理函数(延迟时间内再次触发, 则会重置延迟时间)
 * @param {any} input
 */
function inputing(input, inputHandler) {
    if (inputingInterval) {
        clearTimeout(inputingInterval);
    }
    inputingInterval = setTimeout(() => {
        if (inputHandler && inputHandler instanceof Function) {
            inputHandler(input);
        }
    }, 500);
}

/**
 * 创建一个数据面板
 */
function newDataPannel(pannelId, pannelClass, innerHtml, hide = true) {
    var existPannel = document.querySelector('#' + pannelId);
    if (existPannel) {
        existPannel.remove();
    }
    // 展示properties
    const propertiesPannel = document.createElement('div');
    propertiesPannel.id = pannelId;

    propertiesPannel.classList.add('data-pannel')

    if (pannelClass instanceof Array) {
        pannelClass.forEach(x => propertiesPannel.classList.add(x));
    } else {
        propertiesPannel.classList.add(pannelClass);
    }

    //propertiesPannel.clientWidth = '100%';
    //propertiesPannel.clientHeight = '100%';
    if (hide) {
        propertiesPannel.style.width = '50px';
        propertiesPannel.style.height = '50px';
    }
    propertiesPannel.innerHTML = innerHtml;
    document.querySelector('body').appendChild(propertiesPannel);
}
/**
 * 移除数据面板
 */
function removeDataPannel(pannelId) {
    if (pannelId) {
        const dataPannel = document.querySelector('#' + pannelId);
        if (dataPannel) {
            dataPannel.remove();
        }
    } else {
        const dataPannels = document.querySelectorAll('.data-pannel');
        dataPannels.forEach(x => x.remove());
    }
}

/**
 * 修剪消息长度, 将消息缩短到指定长度
 * @param {any} msg
 * @param {any} maxLength
 * @returns
 */
function trimMsg(msg, maxLength = 50) {
    const half = maxLength / 2;
    return msg.substring(0, half) + '...' + msg.substring(msg.length - half)
}
/**
 * 格式化文件大小
 * @param {any} size
 * @returns
 */
function formatFileSize(size) {
    if (!size) {
        return ''
    }
    const kb = size / 1024
    if (kb < 1024) {
        return kb.toFixed(2) + 'KB'
    }
    const mb = kb / 1024
    if (mb < 1024) {
        return mb.toFixed(2) + 'MB'
    }
    const gb = mb / 1024
    if (gb < 1024) {
        return gb.toFixed(2) + 'GB'
    }
    return (gb / 1024).toFixed(2) + 'TB'
}

// SSE
const frequency = 200;
/**
 * SSE 流读取器 - 使用 async generator 实现
 * 逐条解析并产出 JSON 数据
 */
async function* readSSEStream(response) {
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    
    try {
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            
            // 解码二进制为文本
            const text = decoder.decode(value, { stream: true });
            
            // 解析 JSON（一次可能返回多条）
            const jsonList = text.match(/\{.+\}\n?/g);
            if (!jsonList) {
                console.warn('无法解析 JSON:', text);
                continue;
            }
            
            for (const json of jsonList) {
                try {
                    yield JSON.parse(json);
                } catch (e) {
                    console.warn('JSON 解析失败:', json);
                }
            }
        }
    } finally {
        reader.releaseLock();
    }
}

/**
 * 发送 SSE 请求（通用版）
 * @param {string} url SSE 后端地址
 * @param {any} requestBody 请求体内容, json 格式
 * @param {string} requestTitle 当前请求的标识, 用于区分不同请求返回的消息
 * @param {HTMLElement} spinnerEle 发送请求的按钮
 * @param {HTMLElement} msgContainer 显示消息的容器
 * @param {Function} onstart 初始化函数
 * @param {Function} msgHandler 消息处理函数 (data, requestTitle, msgContainer) => isLastResult
 * @param {number} timeoutSeconds 超时时间（秒），默认 30 秒
 * @returns
 */
async function sendSseRequestCommon(url, requestBody, requestTitle, spinnerEle, msgContainer, onstart, msgHandler = null, timeoutSeconds = 30) {
    if (onstart) {
        onstart();
    }
    // 支持SSE的fetch请求
    try {
        if (spinnerEle) {
            showSpinner(spinnerEle);
        }
        const accessToken = getAccessToken();
        if (!accessToken) {
            showWarningBox('身份已过期, 请重新登录', () => location.href = `/Home/Login?redirect_path=${location.pathname}`);
            return null;
        }
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'authorization': `Bearer ${accessToken}`
            },
            body: requestBody
        })

        if (!response.ok) {
            if (response.status === 401) {
                showWarningBox('身份已过期, 请重新登录', () => location.href = `/Home/Login?redirect_path=${location.pathname}`);
                return null;
            }
            else if (response.status === 404) {
                showErrorBox('接口不存在, 请确认请求方式和参数');
                return null;
            } else {
                showErrorBox(`请求异常:${response.statusText}`);
                return null;
            }
        }

        // 使用默认或自定义的消息处理函数
        const handler = msgHandler || msgsHandler;
        
        let msgNotFoundCount = 0;
        let pendingRender = false;
        const interval = window.setInterval(() => {
            if (globalMsgContainer[requestTitle]) {
                if (!globalMsgContainer[requestTitle]) return;
                const msgCount = globalMsgContainer[requestTitle].length;
                if (msgCount === 0) {
                    msgNotFoundCount++;
                } else {
                    msgNotFoundCount = 0;
                    // ✅ 使用 requestAnimationFrame 批量渲染
                    if (!pendingRender) {
                        pendingRender = true;
                        requestAnimationFrame(() => {
                            const messages = globalMsgContainer[requestTitle].splice(0);
                            for (const data of messages) {
                                const isLastResult = handler(data, requestTitle, msgContainer);
                                if (isLastResult) {
                                    window.clearInterval(interval);
                                    closeSpinner(spinnerEle);
                                    break;
                                }
                            }
                            pendingRender = false;
                        });
                    }
                }
                // 超时检测
                if (msgNotFoundCount >= (timeoutSeconds * 1000) / frequency) {
                    window.clearInterval(interval);
                }
            }
        }, frequency)

        // ✅ 使用 async generator 读取 SSE 流（已解码）
        for await (const data of readSSEStream(response)) {
            if (!globalMsgContainer[requestTitle]) {
                globalMsgContainer[requestTitle] = [data];
            } else {
                globalMsgContainer[requestTitle].push(data);
            }
        }

        // ✅ SSE 流结束后，立即处理剩余消息
        const remainingMessages = globalMsgContainer[requestTitle]?.splice(0) || [];
        for (const data of remainingMessages) {
            handler(data, requestTitle, msgContainer);
        }

        // 清理
        window.clearInterval(interval);
        closeSpinner(spinnerEle);
    } catch (e) {
        showErrorBox(e.message);
        return null;
    }
}

let lastMsg = '';
let globalMsgContainer = {};
function msgsHandler(data, requestTitle, showMsgEl) {
    let isLastResult = false;
    let msgItem = `<li>{{msgContent}}</li>`
    let msgContent = ''
    if (!data.succeed && data?.commandExecuteNo?.indexOf('-cmd-end') === -1) {
        const errMsg = data.message ? data.message : '操作失败';
        const errMsgLines = errMsg.split('\n');
        msgContent += `<p style="color:red;">${requestTitle}: <p>`;
        for (var i = 0; i < errMsgLines.length; i++) {
            msgContent += `<p style="color:red;">&nbsp;&nbsp;&nbsp;&nbsp;${trimMsg(errMsgLines[i], 50)}</p>`
        }
    } else if (!data.message) {
        if (data.commandExecuteNo.endsWith('-cmd-end')) {
            isLastResult = true;
        } else if (msgContent.length === 0) {
            msgContent += `<p style="color:green;">${requestTitle}: 操作成功</p>`;
        }
    } else {
        const msgs = data.message.split('\n');
        let msgHtml = msgItem.indexOf(requestTitle) > -1 ? requestTitle : '';
        for (var i = 0; i < msgs.length; i++) {
            let msg = msgs[i];
            let currentMsgDiv = `<span style="color:green;">${msg}</span>`;
            if (msg && msg.length > 50) {
                msg = trimMsg(msg, 50);
            }
            const processBarPattern = /\[=*>\s*\]\s*(\d+(\.\d+)*)\s*%/;
            const m = msg.match(processBarPattern);
            if (m && m.length > 2) {
                const last = msgItem.lastChild;
                const lastHtml = last.outerHTML;
                if (last && lastHtml.endsWith('%</div>') && !lastHtml.endsWith('100.00 %</div>')) {
                    if (lastHtml.indexOf('100.00') > -1) {
                        console.warn('remove 100%');
                    }
                    last.remove();
                }
            }
            if (msg.length > 0) {
                msgHtml += currentMsgDiv
                lastMsg = msg;
            }
        }

        msgContent += msgHtml;
    }

    if (msgContent && msgContent.length > 0) {
        msgItem = msgItem.replace('{{msgContent}}', msgContent)

        const msgEl = document.createElement('ul')
        msgEl.innerHTML = msgItem
        if (showMsgEl) {
            showMsgEl.appendChild(msgEl)
        }
    }

    return isLastResult
}

/**
 * =====================================================
 * CustomActions 自定义操作处理
 * =====================================================
 */

/**
 * 处理 customActions 配置，自动绑定按钮事件
 * @param {object} tableOptions - createTable 的配置对象
 */
function processCustomActions(tableOptions) {
    if (!tableOptions.customActions || tableOptions.customActions.length === 0) return;
    
    tableOptions.customActions.forEach(action => {
        document.querySelectorAll(`.${action.className}`).forEach(btn => {
            btn.onclick = () => {
                // 优先从按钮的 data-id 获取
                let rowId = btn.getAttribute('data-id');
                
                // 如果按钮没有 data-id，尝试从所在行获取
                if (!rowId) {
                    const tr = btn.closest('tr');
                    if (tr) {
                        rowId = tr.getAttribute('data-id');
                    }
                }
                
                // 从表格中获取当前行数据
                const rowData = { id: rowId };
                // 尝试从按钮的 data-* 属性中获取更多数据
                for (const attr of btn.attributes) {
                    if (attr.name.startsWith('data-') && attr.name !== 'data-id') {
                        const key = attr.name.replace('data-', '').replace(/-([a-z])/g, (g) => g[1].toUpperCase());
                        rowData[key] = attr.value;
                    }
                }
                showCustomActionModal(action, btn, tableOptions, rowData);
            };
        });
    });
}

/**
 * 显示自定义操作的 Modal 弹窗
 * @param {object} action - customAction 配置
 * @param {HTMLElement} triggerBtn - 触发按钮
 * @param {object} tableOptions - 表格配置
 * @param {object} rowData - 当前行数据
 */
function showCustomActionModal(action, triggerBtn, tableOptions, rowData) {
    // 移除旧的 Modal
    const oldModal = document.querySelector('#custom-action-modal');
    if (oldModal) oldModal.remove();

    // 构建表单字段 HTML
    let fieldsHtml = '';
    if (action.modalFields && action.modalFields.length > 0) {
        action.modalFields.forEach((field, index) => {
            fieldsHtml += `<div class="mb-3">`;
            fieldsHtml += `<label for="ca-field-${field.name}" class="form-label">${field.label || field.name}</label>`;
            
            if (field.type === 'dataSource') {
                // dataSource 类型：下拉选择
                let optionsHtml = '<option value="">请选择</option>';
                
                // 如果配置了 reuseFrom，复用表格中已加载的 dataSource 选项
                if (field.reuseFrom && tables[tableOptions.tableId]) {
                    const dsField = tables[tableOptions.tableId].dataSourceField;
                    console.log('reuseFrom:', field.reuseFrom, 'tableId:', tableOptions.tableId, 'dsField:', dsField);
                    if (dsField && dsField[field.reuseFrom + '_options']) {
                        optionsHtml = dsField[field.reuseFrom + '_options'];
                    } else {
                        console.warn(`dataSourceField.${field.reuseFrom}_options 不存在，可用的 keys:`, dsField ? Object.keys(dsField) : 'dsField 为空');
                    }
                }
                
                fieldsHtml += `<select class="form-control form-select-sm" id="ca-field-${field.name}" name="${field.name}">${optionsHtml}</select>`;
            } else if (field.type === 'textarea') {
                fieldsHtml += `<textarea class="form-control form-control-sm" id="ca-field-${field.name}" name="${field.name}" rows="3" placeholder="${field.placeholder || ''}"></textarea>`;
            } else {
                // 默认 text 类型
                fieldsHtml += `<input type="text" class="form-control form-control-sm" id="ca-field-${field.name}" name="${field.name}" placeholder="${field.placeholder || ''}" />`;
            }
            
            fieldsHtml += `</div>`;
        });
    }

    // 创建 Modal
    const div = document.createElement('div');
    div.className = 'modal fade';
    div.id = 'custom-action-modal';
    div.tabIndex = -1;
    div.innerHTML = `
        <div class="modal-dialog">
            <div class="modal-content" style="border: 2px solid #0d6efd; box-shadow: 0 0 20px rgba(13,110,253,0.4);">
                <div class="modal-header">
                    <h5 class="modal-title">${action.modalTitle || '操作'}</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    ${fieldsHtml || '<p class="text-muted">确定要执行此操作吗？</p>'}
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary btn-sm" data-bs-dismiss="modal">取消</button>
                    <button type="button" class="btn btn-primary btn-sm" id="ca-execute-btn">确定</button>
                </div>
            </div>
        </div>`;

    document.body.appendChild(div);
    const modal = new bootstrap.Modal(div);

    // 绑定执行按钮事件
    document.getElementById('ca-execute-btn').onclick = async () => {
        // 收集表单数据
        const formData = {};
        if (action.modalFields) {
            action.modalFields.forEach(field => {
                const el = document.getElementById(`ca-field-${field.name}`);
                if (el) formData[field.name] = el.value;
            });
        }

        // 执行操作
        await executeCustomAction(action, tableOptions, rowData, formData, triggerBtn);
        modal.hide();
    };

    modal.show();
}

/**
 * 构建 dataContent 并执行自定义操作
 * @param {object} action - customAction 配置
 * @param {object} tableOptions - 表格配置
 * @param {object} rowData - 当前行数据
 * @param {object} formData - 表单数据
 * @param {HTMLElement} triggerBtn - 触发按钮
 */
async function executeCustomAction(action, tableOptions, rowData, formData, triggerBtn) {
    // 构建 dataContent
    const dataContent = buildDataContent(action.dataContent, rowData, formData);

    // 构建执行参数
    const exeParams = {
        'dataTableId': tableOptions.tableId,
        'dataContent': dataContent,
        'dataExecuteUrl': action.executeUrl,
        'dataMethod': action.executeMethod || 'POST',
        'trigger': triggerBtn
    };

    // 执行并处理结果
    await execute(exeParams, (response) => {
        showResultBox(response, tables[tableOptions.tableId]);
    });
}

/**
 * 根据配置构建 dataContent
 * @param {object} dataContentConfig - dataContent 配置
 * @param {object} rowData - 当前行数据
 * @param {object} formData - 表单数据
 * @returns {string} JSON 字符串
 */
function buildDataContent(dataContentConfig, rowData, formData) {
    if (!dataContentConfig) return '';
    
    const result = {};
    for (const [key, value] of Object.entries(dataContentConfig)) {
        if (value === '$form') {
            // 从表单获取同名字段
            result[key] = formData[key] || '';
        } else if (typeof value === 'string' && value.startsWith('$form:')) {
            // 从表单获取指定字段
            const formFieldName = value.replace('$form:', '');
            result[key] = formData[formFieldName] || '';
        } else if (typeof value === 'string' && value.match(/^\{\{.+\}\}$/)) {
            // 从当前行数据获取
            const fieldName = value.slice(2, -2);
            result[key] = rowData[fieldName] || '';
        } else {
            // 固定值
            result[key] = value;
        }
    }
    return JSON.stringify(result);
}
