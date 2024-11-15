﻿async function executeCommand(settingId, commandName, executeBtn) {
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
const apiUpdateUrl = "/Hosts/UpdateAnythingSetting";
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

// 用于存储卡片状态
const cardsStatus = [];
function buildDataView(data) {
    let container = document.createElement('div');

    container.classList.add('row');
    container.innerHTML = `<div class="col-sm-6 cards-container"></div><div class="col-sm-6 data-right-pannel"></div>`;
    let cardsHtml = '';
    data.forEach((record, index) => {
        const collapseBtnId = `collapseBtn${record.id}`;
        cardsStatus.push({
            id: record.id,
            isShown: false,
            collapseBtnId,
            commandArray: []
        });
        cardsHtml += (`
                <div class="card mb-2">
                    <!--Header-->
                    <div class="card-header d-flex justify-content-between" id="heading-${record.title}">
                        <h5 class="card-title mb-0" onclick="loadCommandsAsync(this, ${record.id})">
                            <button id="${collapseBtnId}" class="btn btn-link btn-sm" type="button" data-bs-toggle="collapse" data-bs-target="#collapse${record.id}" aria-expanded="false" aria-controls="collapse${record.id}">
                                ${record.title}
                            </button>
                        </h5>
                        <div>
                            <button type="button" class="btn btn-primary btn-sm" data-table-id="${tableId}" data-id="${record.id}" data-fetch-url="${apiUrl}" data-method="POST" onclick="showUpdatePannel(this)">更新</button>
                            <button type="button" class="btn btn-primary btn-sm" onclick="addCommand(this, ${record.id}, '${record.title}')">添加命令</button>
                            <button type="button" class="btn btn-primary btn-sm d-none" data-table-id="${tableId}" data-content="&quot;${record.id}&quot;" data-execute-url="/Study/DeleteQuestion" data-method="POST" onclick="showConfirmBox('确定删除? 此操作无法撤销!', () => execute(this))">删除</button>
                        </div>
                    </div>

                    <!--Body-->
                    <div id="collapse${record.id}" class="collapse" aria-labelledby="heading-${record.title}" data-bs-parent="#accordion">
                        <div class="card-body">
                        </div>
                    </div>
                </div>`);
    });
    container.querySelector('.cards-container').innerHTML = cardsHtml;
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
    { url: 'Hosts/AddAnythingSetting', method: 'POST', updateUrl: apiUpdateUrl, updateMethod: 'POST' },   // 添加修改数据表单弹窗; url为添加数据的url(修改的url放到修改按钮上); method也是请求添加数据接口的请求方法
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
async function loadCommandsAsync(ele, id) {
    const card = ele.closest('.card');

    const cardStatus = currentCard = cardsStatus.find(x => x.id == id)
    if (!cardStatus.isShown) {
        cardsStatus.forEach(x => {
            if (x.isShown) {
                document.querySelector(`#${x.collapseBtnId}`).click();
            }
        })
        var data = await httpRequestDataAsync(`/Hosts/AnythingSettingAndInfo?id=${id}`, ele);
        if (data) {
            let anythingSetting = data.anythingSetting;
            let anythingInfo = data.anythingInfo;

            const commandsArr = JSON.parse(anythingSetting.commands);
            // cardStatus对象保存命令数组
            cardStatus.commandArray = commandsArr;
            let commandsHtml = '';
            const commandItemStyle = commandsArr.length > 1 ? 'background-color:#eee;padding:10px 10px 0 10px;margin-bottom:10px;' : '';
            for (let i = 0; i < commandsArr.length; i++) {
                const command = commandsArr[i];
                const commandInfo = anythingInfo.commands[i];

                let commandState = '';
                const stateArr = !commandInfo || !commandInfo.executedState ? [] : commandInfo.executedState.split('\n');
                for (var j = 0; j < stateArr.length; j++) {
                    commandState += `<div class="card-text">${stateArr[j]}</div>`
                }
                // 命令内容, 可能有多条脚本, 一个脚本一行显示到textarea中
                let commandTxt = command.CommandTxt;
                commandTxt = commandTxt.split(';');
                const commandLength = commandTxt.length;
                for (var j = 0; j < commandLength; j++) {
                    commandTxt[j] = commandTxt[j].trim();
                }
                commandTxt = commandTxt.join('\n');
                commandsHtml += `<div style="${commandItemStyle}" class="command-item" draggable="true">
            <div class="input-group mb-3">
                <textarea class="form-control form-control-sm" rows="${commandLength}" aria-label="command setting" aria-describedby="button-cmd-${id}" anything-id="${id}" oninput="inputing(this, resolveCmdSettingAsync)">${commandTxt}</textarea>
            </div>
        
            <div style="font-size:12px;color:gray;margin-bottom:10px">
                ${!commandInfo || !commandInfo.commandTxt ? '' : commandInfo.commandTxt.split(';').join('<br/>')}
            </div>
            <button class="btn btn-sm btn-primary mb-4 ${(commandInfo && commandInfo.executedState ? " disabled" : "")}" type="button" onclick="executeCommand(${id}, '${command.Name}', this)" id="button-cmd-${id}">${command.Name}</button>
            <div style="font-size:12px; margin-left:30px; color:green;">${commandState}</div>
        </div>`;
            }

            card.querySelector('.card-body').innerHTML = commandsHtml;
            cardStatus.isShown = true;

            // 创建变量properties展示修改面板
            let resolvedProperties = '';
            for (var key in anythingInfo.properties) {
                if (anythingInfo.properties.hasOwnProperty(key)) {
                    resolvedProperties += `<div>${key}: ${anythingInfo.properties[key]}</div>`;
                }
            }
            const dataPannelHtml = `<h5 class="text-white">环境变量</h5>
            <textarea id='properties-${id}' name="properties" rows="6" class="form-control mb-2">${anythingSetting.properties}</textarea><input type="hidden" type="number" id="setting-id-${id}" name="id" value="${id}">
            <div class="d-flex justify-content-end">
                <button type="button" class="btn btn-primary btn-sm" data-content="formItemIds:properties-${id};setting-id-${id}" data-execute-url="${apiUpdateUrl}" data-method="POST" onclick="showConfirmBox('确定更新变量吗?', () => execute(this))">更新变量</button>
            </div>
            <div style="color:white;">${resolvedProperties}</div>`;
            newDataPannel(`data-pannel-${id}`, 'right-data-pannel', dataPannelHtml);
            // 更新选中状态
            ele.parentElement.style.backgroundColor = 'rgba(250, 129, 90, 0.7)';
            ele.parentElement.style.backdropFilter = 'blur(20rpx)';
            ele.closest('.card').style.border = '1px solid rgba(250, 129, 90, 0.7)';
        }
    } else {
        cardStatus.isShown = false;
        // 移除变量properties展示面板
        removeDataPannel(`data-pannel-${id}`);
        // 更新选中状态
        ele.parentElement.style.backgroundColor = '';
        ele.parentElement.style.backdropFilter = '';
        ele.closest('.card').style.border = '';
    }
}

/**
 * 解析命令设置
 * @param {any} input
 */
async function resolveCmdSettingAsync(input) {
    const id = input.getAttribute('anything-id');
    let cmdTxt = input.value;
    cmdTxt = cmdTxt.split('\n').join(';');

    // 先更新cardStatus中的commandArray
    const cardStatus = cardsStatus.find(x => x.id == id);
    const commandName = input.closest('.command-item').querySelector('button').textContent;
    const command = cardStatus.commandArray.find(x => x.Name == commandName);
    command.CommandTxt = cmdTxt;

    // 在解析命令内容
    var data = await httpRequestDataAsync(`/Hosts/ResolveCommandSettting?id=${id}&command=${cmdTxt}`, input, 'POST', '', '', errorHandlerType.returnErrorMessage);
    if (data) {
        input.parentElement.nextElementSibling.innerHTML = data.split(';').join('<br/>');
    }
}

/**
 * 添加一个命令
 * @param {any} eventTrigger
 * @param {any} id
 */
async function addCommand(eventTrigger, id) {
    const cardStatus = cardsStatus.find(x => x.id == id);
    if (!cardsStatus.isShown) {
        const bsCollapse = new bootstrap.Collapse(`#collapse${id}`, {
            toggle: false
        });
        bsCollapse.show();
        cardsStatus.isShown = true;

        const cardTitle = eventTrigger.closest('.card').querySelector('.card-title');
        await loadCommandsAsync(cardTitle, id);
    }
    const cardBody = eventTrigger.closest('.card').querySelector('.card-body');
    const commandHtml = `<div style="background-color:#eee;padding:10px 10px 0 10px;margin-bottom:10px;" class="command-item row g-3">
        <div class="col-auto">
            <label for="commandName" class="visually-hidden">命令名称</label>
            <input type="text" class="form-control form-control-sm" id="commandName" placeholder="命令名称">
            </div>
            <div class="col-auto">
            <button type="button" class="btn btn-primary btn-sm mb-3" onclick="addCommandPost(this, '${id}')">确定</button>
            </div>
        </div>`;
    cardBody.insertAdjacentHTML('afterbegin', commandHtml);
}
/**
 * 给当前Anything添加命令
 * @param {any} eventTrigger
 * @returns
 */
async function addCommandPost(eventTrigger, id) {
    const input = eventTrigger.closest('.command-item').querySelector('input');
    if (!input.value) {
        return;
    }

    // anything的每一条命令更新时(编辑textarea时), 都会更新cardStatus中的commandArray; 所以commandArray中的命令是最新的, 可以作为提交更新的数据
    const cardStatus = cardsStatus.find(x => x.id == id);
    const commandArray = cardStatus.commandArray;
    commandArray.push({ Name: input.value, CommandTxt: '', ExecutedState: '' });
    const anythingCommands = JSON.stringify(commandArray);
    await updateAnythingCommandsAsync(id, anythingCommands);
}

async function updateAnythingCommandsAsync(id, commands) {
    const params = { id, commands };
    //params["id"] = id;
    //params["commands"] = commands;
    const paramsJson = JSON.stringify(params);
    const trigger = {
        // dataTableId: '', // 用于更新后重载数据表格
        dataContent: paramsJson,
        dataExecuteUrl: apiUpdateUrl,
        dataMethod: 'POST'
    };
    execute(trigger, () => loadCommandsAsync(document.querySelector(`#button-cmd-${id}`).closest('.card').querySelector('.card-title'), id));
}