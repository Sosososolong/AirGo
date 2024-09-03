﻿// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const tables = {};
const dataSources = {};

/**
 * 
 * @param {string} apiUrl 查询api地址
 * @param {number} pageIndex 当前页
 * @param {number} pageSize 每页多少条数据
 * @param {string} tableId 数据表table标签的id属性值
 * @param {string} tableContainerSelector 数据表table标签的父元素选择器
 * @param {Array} ths 数据表对应的数据列的配置, 如果dataViewBuilder不为空, 则ths的作用为配置关键字搜索的字段
 * @param {string} idFieldName 主键字段名
 * @param {Array} filterItems 过滤项集合
 * @param {Array} data 初始化数据表的数据
 * @param {function} onDataLoaded 当数据加载完毕执行的回调函数
 * @param {string} wrapper table标签的外部html
 * @param {Object} modalSettings 添加数据弹窗的配置
 * @param {Boolean} primaryKeyIsInt 主键是否是整型
 * @param {string} addButtonSelector 添加按钮的选择器, 不传将创建添加按钮
 * @param {function} dataViewBuilder 不使用table标签展示数据时使用, 该函数根据传入的数据集返回展示数据的元素对象
 */
async function createTable(apiUrl, pageIndex, pageSize, tableId, tableContainerSelector, ths, idFieldName, filterItems = null, data = null, onDataLoaded = undefined, wrapper = '', modalSettings = {}, primaryKeyIsInt = true, addButtonSelector = '', dataViewBuilder = null) {
    if (!tables[tableId]) {
        tables[tableId] = {
            tableId: tableId,
            primaryKeyIsInt: primaryKeyIsInt,
            pageIndex: pageIndex,
            pageSize: pageSize,
            totalPages: 0,
            orderField: '',
            isAsc: true,
            dataFilter: {
                filterItems: [
                    {
                        fieldName: '',
                        compareType: '',
                        value: null
                    }
                ],
                keywords: {
                    fields: [],
                    value: ''
                }
            },
            onDataLoaded: onDataLoaded,
            wrapper: wrapper,
            formItemIds: [], // `${this.tableId}FormInput_${th.name}`
            formItemIdsForAddPannel: [], // 比formItems少一个Id字段的表单项dom的id
            formItemIdsMapper: {},
            modalSettings: modalSettings, // 添加数据面板
            ths: ths,
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
            }
        }
    }
    
    var targetTable = tables[tableId];

    if (addButtonSelector) {
        targetTable.addOptions.button = document.querySelector(addButtonSelector);
    }

    if (filterItems) {
        targetTable.dataFilter.filterItems = filterItems;
    }
    targetTable.dataFilter.keywords.fields = ths.filter(th => th.searchedByKeywords).map(th => th.name);

    targetTable.renderBody = async function (data) {
        if (dataViewBuilder && typeof(dataViewBuilder) === 'function') {
            let dataPannel = dataViewBuilder(data);
            dataPannel.id = this.tableId;
            const t = document.querySelector(`#${this.tableId}`);
            t.parentNode.replaceChild(dataPannel, t);
            return;
        }
        var tbody = $(`#${this.tableId} tbody`);
        tbody.empty();
        for (var j = 0; j < data.length; j++) {
            var row = data[j];
            var tr = $('<tr>');

            for (var i = 0; i < ths.length; i++) {
                var th = ths[i]
                if (th.type === 'button') {
                    var buttonHtml = th.tmpl.replace(/{{id}}/g, row[idFieldName])
                    tr.append(`<td>${buttonHtml}</td>`);
                }
                if (th.name) {
                    // 字段值
                    var tdValue = row[th.name];
                    if (th.title.indexOf('时间') > -1) {
                        tdValue = tdValue.replace('T', ' ');
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

                    // 单元格只显示部分值
                    if (th.showPart && tdValue && tdValue.length > 12) {
                        tdValue = tdValue.substring(0, th.showPart) + '...'
                    }

                    if (j === 0) {
                        var tdWidthProp = '';
                        if (th.width) {
                            tdWidthProp = ` style="width:${th.width}px"`
                        }
                        tr.append(`<td${tdWidthProp}>` + tdValue + '</td>');
                    } else {
                        tr.append('<td>' + tdValue + '</td>');
                    }
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
    }

    // this指向的是window对象, 因为它的父作用域就是createTable()方法, createTable中的this就是window对象
    targetTable.test = () => console.log(this);

    targetTable.getFetchUrl = function () {
        // 如果换成() => 函数形式this就成了window对象
        return `${apiUrl}?pageIndex=${this.pageIndex}&pageSize=${this.pageSize}&orderField=${this.orderField}&isAsc=${this.isAsc}`;
    }

    targetTable.loadData = async function () {
        // 分页条
        var pagination = $(`#page-${this.tableId}`);

        if (data) {
            await this.renderBody(data);
            pagination.hide();
            data = null;
            return;
        }

        // 发送 AJAX 请求获取数据
        const url = this.getFetchUrl();
        const method = 'POST';

        async function onSuccess(response) {
            if (!response) {
                showErrorBox('请求失败');
                return;
            }
            if (response && !response.data) {
                showErrorBox(response.errMsg ?? '请求失败');
                return;
            }
            var data = response.data.data;
            var totalCount = response.data.count;
            targetTable.totalPages = response.data.totalPages;
            // 将数据添加到表格中
            await targetTable.renderBody(data);

            // 更新分页导航
            pagination.empty();
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == 1 ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex - 1) + '">Previous</a></li>');
            for (var i = 1; i <= targetTable.totalPages; i++) {
                pagination.append('<li class="page-item ' + (i == targetTable.pageIndex ? 'active' : '') + '"><a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>');
            }
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == targetTable.totalPages ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex + 1) + '">Next</a></li>');

            // 记录Id主键字段是否是整型
            if (data.length > 0 && isNaN(data[0].id) && targetTable.primaryKeyIsInt) {
                targetTable.primaryKeyIsInt = false;
            }
        }
        
        var response = await fetchData(url, method, targetTable.dataFilter, this.tableId);
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
        for (var i = 0; i < ths.length; i++) {
            var th = ths[i]
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
                        const formItemComponent = th.multiLines
                            ? `<textarea class="form-control form-control-sm" placeholder="${th.title}" name="${th.name}" id="${formItemId}" ondblclick="textareaDbClicked(this)"></textarea>`
                            : `<input class="form-control form-control-sm" type="text" placeholder="${th.title}" name="${th.name}" id="${formItemId}">`
                        // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.2表单项 除Id字段外的其他表单项 - 普通字段
                        this.buildFormItemHtml(th, formItemId, formItemComponent);;
                    }
                } else if (th.type.indexOf('dataSource') === 0) {
                    let dataSourceOptions = await this.resolveDataSourceField(th);
                    const formItemComponent = `<select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>`;
                    this.buildFormItemHtml(th, formItemId, formItemComponent);
                } else if (th.type == 'image') {
                    // BOOKMARK: 前端/frontend封装site.js - 创建模态框 3.2表单项 除Id字段外的其他表单项 - 图片字段
                    const formItemComponent = `<input type="hidden" name="${th.name}" id="${formItemId}" /><input class="form-control form-control-sm" type="file" multiple name="${th.name}_files" onchange="showImages(event, '.img-preview-container')" id="${formItemId}_files"><div class="img-preview-container" style="display:flex;align-items:flex-end;flex-wrap:wrap;"></div>`;
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
        this.tableForm.formHtmlFieldPk = `<input name="id" type="hidden" id="${pkInputId}" />`;
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
            let response = await fetchData(url, 'POST', bodyDataFilter, null)
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
        let searchFormHtmlBase = `<form id="search-form">
    <div class="row g-3">
        <div class="col-sm-3">
            <input type="text" placeholder="关键字" class="form-control form-control-sm" id="search-input">
        </div>
        {{othersFormItems}}
        <div class="col-sm">
            <button type="submit" class="btn btn-dark btn-sm">搜索</button>
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
                this.addOptions.button = `<button type="button" class="btn btn-primary btn-sm" onclick="showAddPannel(tables['${tableId}'])">添加</button>`;
                searchFormHtmlBase = searchFormHtmlBase.replace('{{others}}', this.addOptions.button);
            } else {
                this.addOptions.button.onclick = function(event) {
                    showAddPannel(targetTable);
                };
                searchFormHtmlBase = searchFormHtmlBase.replace('{{others}}', '');
            }
        }
        const searchForm = document.querySelector(tableContainerSelector).querySelector("#search-form");
        if (!searchForm) {
            $(tableContainerSelector).prepend(searchFormHtmlBase);
        }

        // 搜索表单提交事件
        $('#search-form').on('submit', function (event) {
            event.preventDefault();
            targetTable.dataFilter.keywords.value = $('#search-input').val().trim();
            
            const filterItems = [];
            document.querySelector('#search-form').querySelectorAll('input,select').forEach(item => {
                if (item.id !== 'search-input') {
                    // itemValue如果直接使用表单项的值(item.value)得到的都是字符串; 实际上id的值有可能是数字, 所以需要从dataSourceField中找到原始的数据项
                    const itemValue = targetTable.dataSourceField[item.name].find(x => x.id == item.value).id; // item.value;

                    filterItems.push({
                        fieldName: item.name,
                        compareType: '=',
                        value: itemValue
                    });
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
    targetTable.initDataViewStructs = async function() {
        if ($(`#${this.tableId}`).length) {
            return;
        }

        if (this.modalSettings) {
            await this.createModal();
        }

        var tableHtml = `<table class="table table-sm table-hover table-bordered mt-3" id="${tableId}">
        <thead>
            <tr>
            </tr>
        </thead>
        <tbody>
        </tbody>
    </table>
    <nav aria-label="Page navigation">
        <ul class="pagination" id="page-${this.tableId}">
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
            tableHtml = $(tableContainerSelector).append(this.wrapper.replace('{{tableHtml}}', tableHtml));
        }

        // 初始化数据表格结构
        $(tableContainerSelector).append(tableHtml);
        // 获取对应的modal
        if (this.modalId) {
            // BOOKMARK: 前端/frontend封装site.js - table对象的 bootstrap.Modal对象
            this.modal = new bootstrap.Modal(`#${this.modalId}`);
        }

        ths.forEach(th => {
            $(`#${this.tableId} thead tr`).append(`<th>${th.title}</th>`);
        });

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


async function fetchData(url, method, dataFilter, renderElementId, finallyAction) {
    //showSpinner();
    let overlay = null;
    if (renderElementId) {
        overlay = addOverlay(renderElementId);
    }
    try {
        let response = await $.ajax({
            url: url,
            method: method,
            data: JSON.stringify(dataFilter),
            //contentType: 'application/x-www-form-urlencoded',
            contentType: 'application/json',
            dataType: 'json',
            //success: function (response) {
            //    onSuccess(response)
            //},
            //error: function (jqXHR, textStatus, errorThrown) {
            //    alert(errorThrown)
            //}
        });
        return response;
    } catch (e) {
        if (e.status === 500) {
            alert('数据异常, 请联系系统管理员')
        } else {
            alert(e.textStatus)
        }
    } finally {
        //closeSpinner();
        if (overlay) {
            overlay.remove();
        }

        if (finallyAction) {
            finallyAction();
        }
    }
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
        if (!formData.get(key)) { // 如果字段值为空
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
    if (currentForm.querySelector('input[type="file"]')) {
        var formData = new FormData(currentForm);
        checkFormData(formData);
        fetch(url, {
            // 你的服务器端接收上传的URL
            method: method,
            body: formData
        })
        .then(resp => {
            if (resp.ok) {
                return resp.json();
            } else {
                showMessage = () => showErrorBox('操作失败:' + resp.statusText);
                return;
            }
        })
        .then(data => {
            if (data) {
                showMessage = () => showResultBox(data);
            }
        })
        .catch(error => {
            if (e.status === 500) {
                showMessage = () => showErrorBox('接口异常, 请联系系统管理员')
            } else if (e.status === 404) {
                showMessage = () => showErrorBox(`接口不存在: ${url}`)
            } else {
                showMessage = () => showErrorBox(e.textStatus);
            }
            console.log(e);
        })
        .finally(() => {
            closeSpinner();
            showMessage();
        });
    } else {
        // 需要提交的数据对应的所有表单项(添加时不需要Id字段, 如果带上了值为""的Id字段, 会因为转为int类型失败从而导致参数自动绑定失败)
        let formItemIds = handleType === "add" ? table.formItemIdsForAddPannel : table.formItemIds;

        let data = getFormData(formItemIds);
        let dataJsonString = JSON.stringify(data);
        try {
            response = await $.ajax({
                url: url,
                method: method,
                data: dataJsonString,
                //contentType: 'application/x-www-form-urlencoded',
                contentType: 'application/json;charset=utf-8',
                dataType: 'json'
            });
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
        if (response && (response.code === 1 || response.isSuccess)) {
            table.modal.hide();
            showMsgBox('操作成功', () => table.loadData());
        } else {
            if (response) {
                showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
            }
        }
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
 */
async function showUpdatePannel(eventTrigger) {
    let tableId = eventTrigger.getAttribute('data-table-id');
    let table = tables[tableId];
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
    var fetchedData = await fetchData(fetchUrl, method, findByIdFilter);
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
                            showImage(imgContainer, url);
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

    if (response && response.isSuccess) {
        window.table = table;
        showMsgBox('操作成功', () => table.loadData());
    } else {
        showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}

async function execute(eventTrigger, callback = null, useSpinner = true) {
    const isEle = eventTrigger instanceof HTMLElement;
    let tableId = isEle ? eventTrigger.getAttribute('data-table-id') : eventTrigger['dataTableId'];
    let table = tableId ? tables[tableId] : tables[0];
    let dataContent = isEle ? eventTrigger.getAttribute('data-content') : eventTrigger['dataContent'];

    let url = isEle ? eventTrigger.getAttribute('data-execute-url') : eventTrigger['dataExecuteUrl'];
    let method = isEle ? eventTrigger.getAttribute('data-method') : eventTrigger['dataMethod'];
    let response = null;
    if (useSpinner) {
        showSpinner();
    }
    try {
        response = await $.ajax({
            url: url,
            method: method,
            data: dataContent.toString(),
            contentType: 'application/json',
            dataType: 'json',
        });
    } catch (e) {
        showErrorBox('操作失败');
        console.log(e);
    } finally {
        if (useSpinner) {
            closeSpinner();
        }
    }

    if (callback) {
        callback(response);
        return;
    }

    if (response && (response.isSuccess || response.data)) {
        showMsgBox('操作成功', () => table.loadData());
    } else {
        showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}

let cachedFiles = {};
function showImages(event, imgContainer) {
    var files = event.target.files;
    var filesName = event.target.name;
    //const allImg = document.querySelectorAll('.img-preview');
    //if (allImg) {
    //    allImg.forEach(x => x.remove());
    //}
    // 遍历预览所有图片
    for (var i = 0; i < files.length; i++) {
        const file = files[i];
        if (file.type.indexOf('image') !== 0) {
            continue;
        }
        // 缓存所有图片
        const urlsField = filesName.replace('_files', '');
        const urls = document.querySelector(`input[name="${urlsField}"]`).value;
        if (cachedFiles[filesName].findIndex(x => x.name === file.name && x.lastModified === file.lastModified && x.size === file.size) === -1 && urls.indexOf(file.name) === -1) {
            cachedFiles[filesName].push(file);
            showImage(imgContainer, file);
        }
    }
}

function showImage(imgContainer, image) {
    let div = document.createElement('div');
    div.style.display = 'inline-block';
    div.style.position = 'relative';
    div.style.margin = '5px 5px 0 0';

    let img = document.createElement('img');
    // 只设置宽度, 高度自适应
    img.width = 100;
    // 添加class
    img.classList.add('img-preview');
    if (typeof (image) === 'object') {
        img.src = URL.createObjectURL(image);
        img.setAttribute('data-filename', image.name);
        img.onload = function () {
            URL.revokeObjectURL(img.src); // 释放内存
        };
    } else {
        img.src = image;
    }
    
    //event.target.insertAdjacentElement('afterend', img);
    div.appendChild(img);

    if (typeof (imgContainer) === 'object') {
        imgContainer.appendChild(div);
    } else {
        document.querySelector(imgContainer).appendChild(div);
    }

    // 删除按钮
    div.innerHTML += `<div style="width:100%;border-radius:20px;position:absolute;bottom:0;right:0;cursor:pointer;padding:3px;font-size:18px;line-height:15px;text-align:center;background:rgba(255,0,0,0.3);color:#fff;backdrop-filter: blur(5px);" onclick="deleteImg(this)">-</div>`;
}
function deleteImg(node) {
    const imgUrl = node.previousElementSibling.getAttribute('src');
    // 遍历 cachedFiles 对象的每个属性, 更新字段所存的Url信息
    for (var key in cachedFiles) {
        if (cachedFiles.hasOwnProperty(key)) {
            if (imgUrl.indexOf('blob:') === -1) {
                const imgField = key.replace('_files', '');
                const imgInput = document.querySelector(`input[name="${imgField}"]`);
                imgInput.value = imgInput.value.replace(imgUrl + ';', '').replace(imgUrl, '');
            } else {
                // 删除缓存的文件
                const files = cachedFiles[key];
                const imgName = node.previousElementSibling.getAttribute('data-filename');
                const index = files.findIndex(x => x.name === imgName);
                if (index > -1) {
                    files.splice(index, 1);
                }
            }
        }
    }

    // 删除图片元素
    node.parentNode.remove();
}
//function textareaDbClicked(ele) {
//    fetch("/Home/CodeEditor")
//        .then(response => {
//            // 确保请求成功
//            if (!response.ok) {
//                throw new Error('Network response was not ok');
//            }
//            return response.text(); // 将响应转换为文本
//        })
//        .then(html => {
//            // 将获取到的HTML内容插入到页面的指定元素中
//            document.getElementById('container').innerHTML = html;
//        })
//        .catch(error => {
//            // 处理请求过程中可能出现的错误
//            console.error('There was a problem with the fetch operation:', error);
//        });
//}
