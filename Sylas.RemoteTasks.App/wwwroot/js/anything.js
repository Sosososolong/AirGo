async function executeCommand(settingId, commandName, executeBtn) {
    const requestBody = JSON.stringify({ settingId, commandName });
    const data = await httpRequestDataAsync('/Hosts/ExecuteCommand', executeBtn, 'POST', requestBody);
    if (data) {
        if (data.succeed) {
            if (data.message) {
                showMsgBox("操作成功" + ": " + data.message);
            } else {
                showMsgBox("操作成功");
            }
        } else {
            showErrorBox(data.message ? data.message : '操作失败');
        }
    }
}

function searchItems(searchDropdown) {
    // Get reference to dropdown items
    var dropdownItems = searchDropdown.parentNode.parentNode.querySelectorAll(".dropdown-item");

    var value = searchDropdown.value.toLowerCase();
    dropdownItems.forEach(function (item) {
        if (item.textContent.toLowerCase().indexOf(value) > -1) {
            item.style.display = "block";
        } else {
            item.style.display = "none";
        }
    });
}


// 数据查询接口
const apiUrl = "/Hosts/AnythingSettings";
// 用于添加数据搜索表单,数据列表,分页栏等数据相关元素
const tableParentSelector = "#anythingContainer";
const tableId = 'datatable';
// 自定义布局时, 也需要定义字段(生成表单用); 自定义布局时无需定义操作栏
const ths = [
    { name: 'properties', title: '环境变量', multiLines: true },
    { name: 'name', title: '名称', searchedByKeywords: true },
    { name: 'title', title: '标题', searchedByKeywords: true },
    { name: 'executor', title: '执行者' },
    { name: 'commands', title: '命令', multiLines: true },

    { name: 'createTime', title: '创建时间' },
    { name: 'updateTime', title: '更新时间' },

    // { name: 'typeId', title: '题目类型', type: 'dataSource|dataSourceApi=/Study/GetQuestionTypes?pageIndex=1&pageSize=1000|displayField=name' },

    // 操作栏 
    // {
    //     name: '', title: '操作', type: 'button', tmpl: `
    //                     <button type="button" class="btn btn-primary btn-sm" data-table-id="${tableId}" data-id="{{id}}" data-fetch-url="${apiUrl}" data-method="POST" onclick="showUpdatePannel(this)">修改</button>
    //                     <button type="button" class="btn btn-primary btn-sm d-none" data-table-id="${tableId}" data-content="&quot;{{id}}&quot;" data-execute-url="/Study/DeleteQuestion" data-method="POST" onclick="showConfirmBox('确定删除? 此操作无法撤销!', () => execute(this))">删除</button>`
    // }
]
const idFieldName = "id";

function buildDataView(data) {
    let container = document.createElement('div');

    container.classList.add('row');
    container.innerHTML = `<div class="col-sm-6 cards-container"></div><div class="col-sm-6 data-right-pannel"></div>`;
    let cardsHtlm = '';
    data.forEach((record, index) => {
        cardsHtlm += (`
                <div class="card mb-2">
                    <!--Header-->
                    <div class="card-header d-flex justify-content-between" id="heading-${record.title}">
                        <h5 class="card-title mb-0" onclick="showDetails(this, ${record.id})">
                            <button class="btn btn-link btn-sm" type="button" data-bs-toggle="collapse" data-bs-target="#collapse${record.title}" aria-expanded="false" aria-controls="collapse${record.title}">
                                ${record.title}
                            </button>
                        </h5>
                        <div>
                            <button type="button" class="btn btn-primary btn-sm" data-table-id="${tableId}" data-id="${record.id}" data-fetch-url="${apiUrl}" data-method="POST" onclick="showUpdatePannel(this)">修改</button>
                            <button type="button" class="btn btn-primary btn-sm d-none" data-table-id="${tableId}" data-content="&quot;${record.id}&quot;" data-execute-url="/Study/DeleteQuestion" data-method="POST" onclick="showConfirmBox('确定删除? 此操作无法撤销!', () => execute(this))">删除</button>
                        </div>
                    </div>

                    <!--Body-->
                    <div id="collapse${record.title}" class="collapse" aria-labelledby="heading-${record.title}" data-bs-parent="#accordion">
                        <div class="card-body">
                        </div>
                    </div>
                </div>`);
    });
    container.querySelector('.cards-container').innerHTML = cardsHtlm;
    return container;
}

/**
    * 初始化数据表格
    */
createTable(
    apiUrl,                     // 接口地址
    1,                          // pageIndex
    100,                        // pageSize
    tableId,                    // 给数据表一个标识符, 方便一个页面操作多个数据表
    tableParentSelector,        // 数据表父元素
    ths,                        // 数据表列配置
    idFieldName,                // 数据的Id字段名
    null,                       // 数据过滤条件集合 FilterItems
    null,                       // 初始化数据(有的话就渲染此数据, 不会请求接口)
    onDataLoaded,               // 订阅数据渲染完成事件
    '',                         // 对数据表添加父元素, 使用{{tableHtml}}变量代表整个数据表html
    { url: 'Hosts/AddAnythingSetting', method: 'POST', updateUrl: '/Hosts/UpdateAnythingSetting', updateMethod: 'POST' },   // 添加修改数据表单弹窗; url为添加数据的url(修改的url放到修改按钮上); method也是请求添加数据接口的请求方法
    true,
    'h1',
    buildDataView,
    [{ orderField: 'name', isAsc: false }]
)

/**
    * 回调函数
    */
function onDataLoaded(row) {
    return;
}
async function showDetails(ele, id) {
    const card = ele.closest('.card');
    const collapsePannel = card.querySelector('.card-body').parentNode;
    if (!bootstrap.Collapse.getInstance(collapsePannel)._isShown()) {
        var data = await httpRequestDataAsync(`/Hosts/AnythingSettingAndInfo?id=${id}`, ele);
        let anythingSetting = data.anythingSetting;
        let anythingInfo = data.anythingInfo;

        const commandsArr = JSON.parse(anythingSetting.commands);
        let commandsHtml = '';
        for (let i = 0; i < commandsArr.length; i++) {
            const command = commandsArr[i];
            const commandInfo = anythingInfo.commands[i];

            let commandState = '';
            const stateArr = commandInfo.executedState.split('\n');
            for (var j = 0; j < stateArr.length; j++) {
                commandState += `<p class="card-text">${stateArr[j]}</p>`
            }

            let commandTxt = command.CommandTxt;
            commandTxt = commandTxt.split(';');
            commandTxt = commandTxt.join('<br />');
            commandsHtml += `
        <p class="card-text">
            <button type="button"
                    class="btn btn-primary" ${(commandInfo.executedState ? " disabled" : "")}
                    onclick="executeCommand(${id}, '${command.Name}', this)">
                ${command.Name}
            </button>
        </p>
        <p class="card-text">
            ${commandTxt}
        </p>
        <div style="font-size:12px; margin-left:30px; color:green;">${commandState}</div>
        `;
        }

        card.querySelector('.card-body').innerHTML = commandsHtml;
    }
}