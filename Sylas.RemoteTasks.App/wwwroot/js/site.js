﻿// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const tables = {};

function createTable(apiUrl, pageIndex, pageSize, tableId, tableParentSelector, ths, idFieldName, filterItems = null, data = null, onDataLoaded = undefined, wrapper = '', addModalSettings) {
    if (!tables[tableId]) {
        tables[tableId] = {
            tableId: tableId,
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
                        value: ''
                    }
                ],
                keywords: {
                    fields: [],
                    value: ''
                }
            },
            onDataLoaded: onDataLoaded,
            wrapper: wrapper,
            formItemIds: [],
            formItemIdsMapper: {},
            addModalSettings: addModalSettings,
            ths: ths,
            dataSourceField: {
                xxFieldName: [
                    { id: 1, value: '同步应用和流程数据 - zcmu' }
                ]
            },
            modalId: '',
            modal: '',
            addOptions: {
                tableForm: '',
                button: ''
            }
        }
    }

    var targetTable = tables[tableId];

    if (filterItems) {
        targetTable.dataFilter.filterItems = filterItems;
    }

    targetTable.renderBody = async function (data) {
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
                    // 单元格只显示部分值
                    var tdValue = row[th.name];
                    if (th.type && th.type.indexOf('dataSource') === 0) {
                        if (!this.dataSourceField || !this.dataSourceField[th.name]) {
                            await this.resolveDataSourceField(th);
                        }
                        for (let dataSourceIndex = 0; dataSourceIndex < this.dataSourceField[th.name].length; dataSourceIndex++) {
                            let dataSource = this.dataSourceField[th.name][dataSourceIndex];
                            if (tdValue === dataSource.id) {
                                tdValue = dataSource.value;
                                break;
                            }
                        }
                    }

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

            if (this.onDataLoaded) {
                this.onDataLoaded(row);
            }
        }
    }

    // this指向的是window对象, 因为它的父作用域就是createTable()方法, createTable中的this就是window对象
    targetTable.test = () => console.log(this);

    targetTable.getFetchUrl = function () {
        // 如果换成() => 函数形式this就成了window对象
        return `${apiUrl}?pageIndex=${this.pageIndex}&pageSize=${this.pageSize}&orderField=${this.orderField}&isAsc=${this.isAsc}`;
    }

    targetTable.loadData = async function() {
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
            var data = response.data;
            var totalCount = response.count;
            targetTable.totalPages = response.totalPages;
            // 将数据添加到表格中
            await targetTable.renderBody(data);

            // 更新分页导航
            pagination.empty();
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == 1 ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex - 1) + '">Previous</a></li>');
            for (var i = 1; i <= targetTable.totalPages; i++) {
                pagination.append('<li class="page-item ' + (i == targetTable.pageIndex ? 'active' : '') + '"><a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>');
            }
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == targetTable.totalPages ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex + 1) + '">Next</a></li>');
        }
        
        var response = await fetchData(url, method, targetTable.dataFilter, this.tableId);
        if (response) {
            await onSuccess(response);
        }
    };

    targetTable.createModal = async function () {
        if (this.modalId && this.modal) {
            // 已经创建过了
            return;
        }

        const tableDataModalId = `modelForAdd${this.tableId}`;
        this.modalId = tableDataModalId;

        //data-bs-toggle="modal" data-bs-target="#${tableDataModalId}"
        this.addOptions.button = `<button type="button" class="btn btn-primary btn-sm mt-3" onclick="showAddPannel(tables['${tableId}'])">添加</button>`;
        // 构建modal表单 - tableForm
        for (var i = 0; i < ths.length; i++) {
            var th = ths[i]
            if (th.name) {
                let formItemId = `${this.tableId}FormInput_${th.name}`;
                if (!th.type) {
                    if (th.enumValus) {
                        // 字段值有限, 下拉框选取
                        let dataSourceOptions = '';
                        th.enumValus.forEach(val => {
                            dataSourceOptions += `<option value="${val}">${val}</option>`
                        })
                        this.addOptions.tableForm += `<div class="mb-3">
<label for="${formItemId}" class="col-form-label">${th.title}:</label>
<select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>
</div>`;
                    } else {
                        this.addOptions.tableForm += `<div class="mb-3">
<label for="${formItemId}" class="col-form-label">${th.title}:</label>
<input class="form-control form-control-sm" type="text" placeholder="${th.title}" name="${th.name}" id="${formItemId}" aria-label=".form-control-sm example">
</div>`;
                    }

                    // 记录表单项Id
                    if (this.formItemIds.indexOf(formItemId) === -1) {
                        this.formItemIds.push(formItemId);
                        this.formItemIdsMapper[formItemId] = th.name;
                    }
                } else if (th.type.indexOf('dataSource') === 0) {
                    let dataSourceOptions = await this.resolveDataSourceField(th);

                    this.addOptions.tableForm += `<div class="mb-3">
<label for="${formItemId}" class="col-form-label">${th.title}:</label>
<select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>
</div>`;
                    // 记录表单项Id
                    if (this.formItemIds.indexOf(formItemId) === -1) {
                        this.formItemIds.push(formItemId);
                        this.formItemIdsMapper[formItemId] = th.name;
                    }
                }
            }
        }
        let idInputId = `${this.tableId}FormInput_id`;
        this.addOptions.tableForm += `<input name="id" type="hidden" id="${idInputId}" />`;
        if (this.formItemIds.indexOf(idInputId) === -1) {
            this.formItemIds.push(idInputId);
            this.formItemIdsMapper[idInputId] = 'id';
        }

        this.addOptions.modalHtml = `<div class="modal fade" tabindex="-1" id="${tableDataModalId}">
    <div class="modal-dialog">
    <div class="modal-content">
        <div class="modal-header">
        <h5 class="modal-title">${this.tableId}添加数据</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
        </div>
        <div class="modal-body">
        ${this.addOptions.tableForm}
        </div>
        <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">关闭</button>
        <button type="button" class="btn btn-primary" data-btn-type="submit" data-form-type="add" onclick="handleData(tables['${this.tableId}'], this)">提交</button>
        </div>
    </div>
    </div>
</div>`;
    }

    targetTable.resolveDataSourceField = async function (thDataSource) {
        let dataSourceApi = /dataSourceApi=([^=|]+)/.exec(thDataSource.type)[1];

        let displayField = 'id'
        let displayFieldPattern = /displayField=([^=|]+)/.exec(thDataSource.type);
        if (displayFieldPattern && displayFieldPattern.length > 1) {
            displayField = displayFieldPattern[1]
        }

        let url = `${dataSourceApi}?pageIndex=1&pageSize=1000`;
        let dataSourceOptions = '';

        let response = await fetchData(url, 'POST', null, null)
        if (response) {
            response.data.forEach(row => {
                dataSourceOptions += `<option value="${row['id']}">${row[displayField]}</option>`

                if (!this.dataSourceField) {
                    this.dataSourceField = {};
                }
                if (!this.dataSourceField[thDataSource.name]) {
                    this.dataSourceField[thDataSource.name] = []
                }
                // HttpRequestProcessor的id和title(id: 1, value: 同步应用和流程数据 - zcmu)
                this.dataSourceField[thDataSource.name].push({ id: row['id'], value: row[displayField] })
            })
        }
        return dataSourceOptions;
    }

    targetTable.initTableStruct = async function initTableStruct() {
        if ($(`#${this.tableId}`).length) {
            return;
        }

        if (this.addModalSettings) {
            await this.createModal();
        }

        var tableHtml = `${this.addOptions.button}
    <table class="table table-sm table-hover table-bordered mt-3" id="${tableId}">
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
            tableHtml = $(tableParentSelector).append(this.wrapper.replace('{{tableHtml}}', tableHtml))
        }

        // 初始化数据表格结构
        $(tableParentSelector).append(tableHtml)
        // 获取对应的modal
        if (this.modalId) {
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
        await this.initTableStruct();
        // 加载第一页数据
        await this.loadData();
    }

    // 搜索表单提交事件
    $('#search-form').submit(function (event) {
        event.preventDefault();
        searchQuery = $('#search-input').val().trim();
        targetTable.pageIndex = 1;
        targetTable.loadData();
    });

    targetTable.searchKeywords = function() {
        // 获取表单元素
        var form = document.querySelector('#search-form');
        // 触发搜索表单提交事件重新查询数据
        form.submit();
    }

    targetTable.render();
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
    showModal(table);
}

function showModal(table) {
    table.modal.show();
}

async function handleData(table, eventTrigger) {
    let handleType = eventTrigger.getAttribute("data-form-type");
    let url = handleType === "add" ? table.addModalSettings.url : eventTrigger.getAttribute("data-update-url");
    let method = handleType === "add" ? table.addModalSettings.method : eventTrigger.getAttribute("data-update-method");
    let data = getFormData(table.formItemIds);
    let dataJsonString = JSON.stringify(data);
    showSpinner();
    let response = null;
    try {
        response = await $.ajax({
            url: url,
            method: method,
            data: dataJsonString,
            //contentType: 'application/x-www-form-urlencoded',
            contentType: 'application/json;charset=utf-8',
            dataType: 'json'
        });

        closeSpinner();
    } catch (e) {
        if (e.status === 500) {
            showErrorBox('接口异常, 请联系系统管理员')
            console.log(url, e);
        } else if (e.status === 404) {
            showErrorBox(`接口不存在: ${url}`)
        } else {
            alert(e.textStatus)
        }
        console.log(e);
    } finally {
        closeSpinner();
    }

    if (response && response.isSuccess) {
        table.modal.hide();
        showMsgBox('操作成功', () => table.loadData()); //table.searchKeywords();
    } else {
        showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}

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
    let dataId = eventTrigger.getAttribute('data-id');
    let fetchUrl = eventTrigger.getAttribute('data-fetch-url');
    let updateUrl = eventTrigger.getAttribute('data-update-url');
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
        let record = fetchedData.data[0];
        table.formItemIds.forEach(formItemId => {
            let formItem = document.querySelector(`#${formItemId}`);

            let field = table.formItemIdsMapper[formItemId];
            let fieldValue;
            try {
                fieldValue = record[field].toString();
            } catch (e) {
                console.log(e);
            }

            formItem.value = fieldValue;
        })

        let submitButton = document.querySelector(`#${table.modalId} button[data-btn-type="submit"]`);
        submitButton.setAttribute("data-form-type", "update");
        submitButton.setAttribute("data-update-url", updateUrl);
        submitButton.setAttribute("data-update-method", method);
        showModal(table);
    }
}

async function deleteData(eventTrigger) {
    let tableId = eventTrigger.getAttribute('data-table-id');
    let table = tables[tableId];
    let dataId = eventTrigger.getAttribute('data-id');

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
        showMsgBox('操作成功', () => table.loadData());
    } else {
        showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}

async function execute(eventTrigger) {
    let tableId = eventTrigger.getAttribute('data-table-id');
    let table = tables[tableId];
    let dataContent = eventTrigger.getAttribute('data-content');

    let url = eventTrigger.getAttribute('data-execute-url');
    let method = eventTrigger.getAttribute('data-method');
    let response = null;
    showSpinner();
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
        closeSpinner();
    }

    if (response && response.isSuccess) {
        showMsgBox('操作成功', () => table.loadData());
    } else {
        showErrorBox(response.errMsg, '错误提示', [{ class: 'error', content: '关闭' }]);
    }
}
