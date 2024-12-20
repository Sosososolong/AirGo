﻿async function executeCommand(settingId, commandName, executeBtn, overrideMsg = true) {
    const requestBody = JSON.stringify({ settingId, commandName });
    const data = await httpRequestDataAsync('/Hosts/ExecuteCommand', executeBtn, 'POST', requestBody);
    if (data) {
        const rigthPannel = document.querySelector('.data-right-pannel');
        if (data.succeed) {
            if (data.message) {
                const msgs = data.message.split('\n').filter(x => x);
                let msgHtml = `<div style="color:green;">${commandName}:</div>`;
                msgs.forEach(msg => {
                    if (msg && msg.length > 50) {
                        msg = trimMsg(msg, 50);
                    }
                    msgHtml += `<div style="color:gray; margin-left:20px;">${msg}</div>`
                })
                if (overrideMsg) {
                    rigthPannel.innerHTML = msgHtml;
                }
                else {
                    rigthPannel.innerHTML += msgHtml;
                }
                //showMsgBox("操作成功" + ": " + data.message);
            } else {
                rigthPannel.innerHTML += `<p style="color:green;">${commandName}: 操作成功</p>`;
            }
        } else {
            const errMsg = data.message ? data.message : '操作失败';
            if (overrideMsg) {
                rigthPannel.innerHTML = `<p style="color:red;">${commandName}: ${trimMsg(errMsg, 50)}</p>`
            } else {
                rigthPannel.innerHTML += `<p style="color:red;">${commandName}: ${trimMsg(errMsg, 50)}</p>`
            }
            //showErrorBox(errMsg);
        }
    }
}

async function executeCommands(trigger) {
    const commandCheckboxes = document.querySelectorAll('.command-checkbox:checked');
    if (commandCheckboxes && commandCheckboxes.length > 0) {
        document.querySelector('.data-right-pannel').innerHTML = '';
    } else {
        showErrorBox("请选择需要执行的命令");
    }
    for (var i = 0; i < commandCheckboxes.length; i++) {
        if (commandCheckboxes[i].checked) {
            const anythingId = commandCheckboxes[i].getAttribute("data-id");
            const commandName = commandCheckboxes[i].getAttribute("data-command-name");
            await executeCommand(anythingId, commandName, trigger, false);
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
    { name: 'executor', title: '执行者', type: 'dataSource|dataSourceApi=/Hosts/Executors?pageIndex=1&pageSize=1000|displayField=name' },
    { name: 'commands', title: '命令', multiLines: true },

    { name: 'createTime', title: '创建时间' },
    { name: 'updateTime', title: '更新时间' },
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
                    <div class="card-header d-flex justify-content-between" style="flex-wrap: wrap;" id="heading-${record.title}">
                        <h5 class="card-title mb-0" onclick="loadCommandsAsync(this, ${record.id})">
                            <button id="${collapseBtnId}" class="btn btn-link btn-sm" type="button" data-bs-toggle="collapse" data-bs-target="#collapse${record.id}" aria-expanded="false" aria-controls="collapse${record.id}">
                                ${record.title}
                            </button>
                        </h5>
                        <div>
                            <button type="button" class="btn btn-primary btn-sm" data-table-id="${tableId}" data-id="${record.id}" data-fetch-url="${apiUrl}" data-method="POST" onclick="showUpdatePannel(this, () => cardsStatus.find(x => x.id == ${record.id}).isShown = false, () => { document.querySelector('#${collapseBtnId}').click(); })">更新</button>
                            <button type="button" class="btn btn-primary btn-sm" onclick="updateAnythingCommandsFromCardStatus(${record.id})">应用</button>
                            <button type="button" class="btn btn-primary btn-sm" onclick="addCommand(this, ${record.id}, '${record.title}')">添加命令</button>
                            <button type="button" class="btn btn-primary btn-sm" onclick="executeCommands(this)">运行</button>
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
    { url: '/Hosts/AddAnythingSetting', method: 'POST', updateUrl: apiUpdateUrl, updateMethod: 'POST' },   // 添加修改数据表单弹窗; url为添加数据的url(修改的url放到修改按钮上); method也是请求添加数据接口的请求方法
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
const multiCommandItemsStyle = 'background-color:#eee;padding:10px 30px 0 10px;margin-bottom:10px;position:relative;border-radius:5px;';
const sigleCommandItemStyle = 'position:relative;';
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
            const commandItemStyle = commandsArr.length > 1 ? multiCommandItemsStyle : sigleCommandItemStyle;
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
                let commandLength = 1;
                if (commandTxt.indexOf('\n') > -1) {
                    commandLength = commandTxt.split('\n').length;
                } else {
                    commandTxtArr = commandTxt.split(';');
                    commandLength = commandTxtArr.length;
                    for (var j = 0; j < commandLength; j++) {
                        commandTxtArr[j] = commandTxtArr[j].trim();
                    }
                    commandTxt = commandTxtArr.join('\n');
                }

                commandsHtml += `<div style="${commandItemStyle}" class="command-item" style="padding-right:10px;" draggable="true">
            <div class="input-group mb-3">
                <div class="input-group-text">
                  <input class="form-check-input mt-0 command-checkbox" type="checkbox" value="" aria-label="Checkbox for following text input" data-id="${id}" data-command-name="${command.Name}">
                </div>
                <textarea class="form-control form-control-sm" rows="${commandLength}" aria-label="command setting" aria-describedby="button-cmd-${id}" anything-id="${id}" oninput="inputing(this, resolveCmdSettingAsync)">${commandTxt}</textarea>
            </div>

            <button class="btn btn-sm btn-primary mb-2 ${(commandInfo && commandInfo.executedState ? " disabled" : "")}" type="button" onclick="executeCommand(${id}, '${command.Name}', this)" id="button-cmd-${id}">${command.Name}</button>

            <div style="font-size:12px;color:gray;padding-bottom:10px" class="command-resolved">
                ${ !commandInfo || !commandInfo.commandTxt ? '' : commandInfo.commandTxt.split(';').join('<br/>').replace(/</g, '&lt;').replace(/>/g, '&gt;') }
            </div>

            <div style="font-size:12px; margin-left:30px; color:green;">${commandState}</div>
            <div class="arrow-up" style="position:absolute;right:5px;bottom:20px;cursor:pointer;" onclick="changeOrderAsync(this, ${id}, ${i}, ${commandsArr.length}, -1)"></div>
            <div class="arrow-down" style="position:absolute;right:5px;bottom:-5px;cursor:pointer;" onclick="changeOrderAsync(this, ${id}, ${i}, ${commandsArr.length}, 1)"></div>
            <div style="position:absolute;right:7px;top:10px;cursor:pointer;font-size:12px;" onclick="showConfirmBox('确定移除命令[${command.Name}]吗?', () => removeCommandItemAsync(this, ${id}, ${i}))">❌</div>
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
            const dataPannelHtml = `<div class="d-flex justify-content-between">
                <h4 class="text-white mb-4 data-pannel-title">环境变量</h4>
                <div style="color:white;font-size:24px;cursor:pointer;padding: 0 10px;line-height:12px;" onclick="resizeDataPannel(event, ${id})">-</div>
            </div>
            <div class="data-pannel-body">
                <textarea id='properties-${id}' placeholder="设置变量" style="background-color:rgba(255,255,255,0);color:white;" name="properties" rows="6" class="form-control mb-2">${anythingSetting.properties}</textarea><input type="hidden" type="number" id="setting-id-${id}" name="id" value="${id}">
                <div class="d-flex justify-content-end">
                    <button type="button" class="btn btn-primary btn-sm" data-content="formItemIds:properties-${id};setting-id-${id}" data-execute-url="${apiUpdateUrl}" data-method="POST" onclick="showConfirmBox('确定更新变量吗?', () => execute(this))">更新变量</button>
                </div>
                <div style="color:gray;">${resolvedProperties}</div>
            </div>`;
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

const resizeDataPannel = (e, id) => {
    const trigger = e.target;
    const triggerSymbol = trigger.textContent;
    const dataPannel = document.querySelector(`#data-pannel-${id}`);

    dataPannel.style.transition = 'all 0.3s';
    if (triggerSymbol === '-') {
        dataPannel.querySelector('.data-pannel-title').style.display = 'none';
        dataPannel.querySelector('.data-pannel-body').style.display = 'none';
        trigger.innerText = '+';
        dataPannel.style.width = '50px';
        dataPannel.style.height = '50px';

        trigger.style.lineHeight = '24px';
    } else {
        dataPannel.querySelector('.data-pannel-title').style.display = 'block';
        dataPannel.querySelector('.data-pannel-body').style.display = 'block';
        trigger.innerText = '-';
        dataPannel.style.width = '';
        dataPannel.style.height = '';

        trigger.style.lineHeight = '12px';
    }
}

/**
 * 解析命令设置
 * @param {any} input
 */
async function resolveCmdSettingAsync(input) {
    const id = input.getAttribute('anything-id');
    let cmdTxt = input.value;

    // 先更新cardStatus中的commandArray
    const cardStatus = cardsStatus.find(x => x.id == id);
    const commandName = input.closest('.command-item').querySelector('button').textContent;
    const command = cardStatus.commandArray.find(x => x.Name == commandName);
    command.CommandTxt = cmdTxt;

    // 在解析命令内容
    const bodyObj = { id, cmdTxt };
    const body = JSON.stringify(bodyObj);
    var data = await httpRequestDataAsync(`/Hosts/ResolveCommandSettting`, input, 'POST', body, '', errorHandlerType.returnErrorMessage);
    if (data) {
        input.closest('.command-item').querySelector('.command-resolved').innerText = data.split(';').join('<br/>');
    }
}

/**
 * 调整命令的顺序
 * @param {any} trigger
 * @param {any} id
 * @param {any} currentCommandIndex
 * @param {any} length
 * @param {any} step 当前命令的向前移动(索引+)多少
 * @returns
 */
async function changeOrderAsync(trigger, id, currentCommandIndex, length, step) {
    if (step === 0) {
        return;
    }

    let nextIndex = currentCommandIndex + step;
    if (nextIndex < 0) {
        nextIndex = length + nextIndex;
    }
    else if (nextIndex >= length) {
        nextIndex = nextIndex - length;
    }

    // 先更新cardStatus中的commandArray的顺序
    const cardStatus = cardsStatus.find(x => x.id == id);
    const commands = cardStatus.commandArray;

    const currentCommandCopy = commands[currentCommandIndex];
    
    commands[currentCommandIndex] = commands[nextIndex];
    commands[nextIndex] = currentCommandCopy;

    // 更新div的顺序(下一个放到当前元素的前面)
    const commandBlocks = trigger.closest('.card').querySelectorAll('.command-item');
    const currentCommandBlock = commandBlocks[currentCommandIndex];
    const nextCommandBlock = commandBlocks[nextIndex];
    nextCommandBlock.insertAdjacentElement('afterend', currentCommandBlock);

    // 更新anything的命令集
    await updateAnythingCommandsFromCardStatus(id, commands);
}

/**
 * 移除anything的指定命令
 * @param {any} trigger
 * @param {any} id
 * @param {any} removedIndex
 */
async function removeCommandItemAsync(trigger, id, removedIndex) {
    // 先移除cardStatus中的对应的命令
    const cardStatus = cardsStatus.find(x => x.id == id);
    const commands = cardStatus.commandArray;
    commands.splice(removedIndex);

    // 移除对应的dom
    trigger.closest('.command-item').remove();
    document.querySelector('.command-item').remove();

    // 更新anything的命令集
    await updateAnythingCommandsFromCardStatus(id, commands);
}

/**
 * 添加一个命令
 * @param {any} eventTrigger
 * @param {any} id
 */
async function addCommand(eventTrigger, id) {
    const cardStatus = cardsStatus.find(x => x.id == id);
    if (!cardStatus.isShown) {
        const bsCollapse = new bootstrap.Collapse(`#collapse${id}`, {
            toggle: false
        });
        bsCollapse.show();
        cardStatus.isShown = true;

        const cardTitle = eventTrigger.closest('.card').querySelector('.card-title');
        await loadCommandsAsync(cardTitle, id);
    }
    const cardBody = eventTrigger.closest('.card').querySelector('.card-body');
    const commandHtml = `<div style="${multiCommandItemsStyle}" class="command-item row g-3">
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

    await updateAnythingCommandsFromCardStatus(id, commandArray);
}

async function updateAnythingCommandsFromCardStatus(id, cardStatusCommands) {
    if (!cardStatusCommands) {
        const cardStatus = cardsStatus.find(x => x.id == id);
        cardStatusCommands = cardStatus.commandArray;
    }
    const anythingCommands = JSON.stringify(cardStatusCommands);
    await updateAnythingCommandsAsync(id, anythingCommands);
}

async function updateAnythingCommandsAsync(id, commands) {
    const params = { id: id.toString(), commands };
    //params["id"] = id;
    //params["commands"] = commands;
    const paramsJson = JSON.stringify(params);
    const trigger = {
        // dataTableId: '', // 用于更新后重载数据表格
        dataContent: paramsJson,
        dataExecuteUrl: apiUpdateUrl,
        dataMethod: 'POST'
    };

    // 将显示状态设置为false(true不会重载数据), 然后重载数据
    const cardStatus = cardsStatus.find(x => x.id == id);
    cardStatus.isShown = false;
    execute(trigger, () => loadCommandsAsync(document.querySelector(`#button-cmd-${id}`).closest('.card').querySelector('.card-title'), id));
}
