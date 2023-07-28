// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var tables = {};
const defaultDataFilter = {
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
};

function createTable(apiUrl, tableId, tableParentSelector, ths, idFieldName, filterItems = null, data = null, onDataLoaded = undefined, wrapper = '', addModalSettings, updateModalSettings) {
    if (!tables[tableId]) {
        tables[tableId] = {
            pageIndex: 1,
            totalPages: 0,
            pageSize: 1,
            orderField: '',
            isAsc: true,
            dataFilter: defaultDataFilter,
            onDataLoaded: onDataLoaded,
            wrapper: wrapper,
            formItemIds: [],
            addModalSettings: addModalSettings,
            updateModalSettings: updateModalSettings,
            addOptions: {
                tableForm: '',
                button: ''
            }
        }
    }

    var targetTable = tables[tableId];

    if (!filterItems) {
        targetTable.dataFilter.filterItems = filterItems;
    }

    targetTable.renderBody = function renderBody(data) {
        var tbody = $(`#${tableId} tbody`);
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

    targetTable.loadData = async function loadData() {
        // 分页条
        var pagination = $(`#page-${tableId}`);

        if (data) {
            this.renderBody(data);
            pagination.hide();
            return;
        }

        // 发送 AJAX 请求获取数据
        const url = `${apiUrl}?pageIndex=${targetTable.pageIndex}&pageSize=${targetTable.pageSize}&orderField=${targetTable.orderField}&isAsc=${targetTable.isAsc}`;
        const method = 'POST';

        function onSuccess(response) {
            var data = response.data;
            var totalCount = response.count;
            targetTable.totalPages = response.totalPages;
            // 将数据添加到表格中
            targetTable.renderBody(data);

            // 更新分页导航
            pagination.empty();
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == 1 ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex - 1) + '">Previous</a></li>');
            for (var i = 1; i <= targetTable.totalPages; i++) {
                pagination.append('<li class="page-item ' + (i == targetTable.pageIndex ? 'active' : '') + '"><a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>');
            }
            pagination.append('<li class="page-item ' + (targetTable.pageIndex == targetTable.totalPages ? 'disabled' : '') + '"><a class="page-link" href="#" data-page="' + (targetTable.pageIndex + 1) + '">Next</a></li>');
        }
        //function onError(jqXHR, textStatus, errorThrown) {
        //    console.log('Error: ' + errorThrown);
        //}
        //fetchData(url, method, targetTable.dataFilter, onSuccess, onError)
        var response = await fetchData(url, method, targetTable.dataFilter)
        if (response) {
            onSuccess(response);
        }
    };

    targetTable.initTableStruct = async function initTableStruct() {
        if ($(`#${tableId}`).length) {
            return;
        }
        const tableDataModalId = `modelForAdd${tableId}`;

        if (this.addModalSettings) {
            this.addOptions.button = `<button type="button" class="btn btn-primary btn-sm mt-3" data-bs-toggle="modal" data-bs-target="#${tableDataModalId}">添加</button>`;
            // 构建modal表单 - tableForm
            for (var i = 0; i < ths.length; i++) {
                var th = ths[i]
                if (th.name) {
                    let formItemId = `${tableId}FormInput_${th.name}`;
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
                        }
                    } else if (th.type.indexOf('dataSource') === 0) {
                        let dataSourceApi = /dataSourceApi=([^=|]+)/.exec(th.type)[1];

                        let displayField = 'id'
                        let displayFieldPattern = /displayField=([^=|]+)/.exec(th.type);
                        if (displayFieldPattern && displayFieldPattern.length > 1) {
                            displayField = displayFieldPattern[1]
                        }

                        let url = `${dataSourceApi}?pageIndex=1&pageSize=1000`;
                        let dataSourceOptions = '';
                        let response = await fetchData(url, 'POST', null)
                        if (response) {
                            response.data.forEach(row => {
                                dataSourceOptions += `<option value="${row['id']}">${row[displayField]}</option>`
                            })
                        }
                        this.addOptions.tableForm += `<div class="mb-3">
    <label for="${formItemId}" class="col-form-label">${th.title}:</label>
    <select class="form-control form-select-sm" aria-label="Default select" name="${th.name}" id="${formItemId}">${dataSourceOptions}</select>
  </div>`;
                        // 记录表单项Id
                        if (this.formItemIds.indexOf(formItemId) === -1) {
                            this.formItemIds.push(formItemId);
                        }
                    }
                }
            }
            let idInputId = `${tableId}FormInput_id`;
            this.addOptions.tableForm += `<input name="id" type="hidden" id="${idInputId}" />`;
            if (this.formItemIds.indexOf(idInputId) === -1) {
                this.formItemIds.push(idInputId);
            }
            let formItemIdsStr = this.formItemIds.join(',');
            this.addOptions.modalHtml = `<div class="modal fade" tabindex="-1" id="${tableDataModalId}">
      <div class="modal-dialog">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">${tableId}添加数据</h5>
            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
          </div>
          <div class="modal-body">
            ${this.addOptions.tableForm}
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">关闭</button>
            <button type="button" class="btn btn-primary" onclick="addData('${this.addModalSettings.url}', '${this.addModalSettings.method}', getFormData('${formItemIdsStr}'))">提交</button>
          </div>
        </div>
      </div>
    </div>`;
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
        <ul class="pagination" id="page-${tableId}">
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

        // modal提交事件
        $(`#${tableDataModalId}`).on('click', 'a[data-page]', function (event) {
            event.preventDefault();
            
        });

        ths.forEach(th => {
            $(`#${tableId} thead tr`).append(`<th>${th.title}</th>`);
        });

        // 分页导航点击事件
        $(`#page-${tableId}`).on('click', 'a[data-page]', function (event) {
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

    targetTable.render();
}


async function fetchData(url, method, dataFilter) {
    try {
        return await $.ajax({
            url: url,
            method: method,
            data: dataFilter,
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
    } catch (e) {
        if (e.status === 500) {
            alert('数据异常, 请联系系统管理员')
        } else {
            alert(e.textStatus)
        }
        console.log(e);
    }
}

async function addData(url, method, data) {
    try {
        let dataJsonString = JSON.stringify(data);
        let response = await $.ajax({
            url: url,
            method: method,
            data: dataJsonString,
            //contentType: 'application/x-www-form-urlencoded',
            contentType: 'application/json;charset=utf-8',
            dataType: 'json'
        });
        console.log('resp', response);
        if (response.isSuccess) {
            alert("操作成功");
        } else {
            alert(response.errMsg)
        }
    } catch (e) {
        if (e.status === 500) {
            alert('接口异常, 请联系系统管理员')
            console.log(url, e);
        } else {
            alert(e.textStatus)
        }
        console.log(e);
    }
}

function getFormData(formItemIdsString) {
    let formItemIds = formItemIdsString.split(',');
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
