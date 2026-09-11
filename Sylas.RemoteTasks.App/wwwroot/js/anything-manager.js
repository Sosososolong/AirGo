const apiUrl = '/Hosts/AnythingSettings';
const apiUpdateUrl = '/Hosts/UpdateAnythingSetting';
const tableParentSelector = '#anythingManagerContainer';
const tableId = 'am-datatable';
const cardsStatus = new Map();
const executorMap = new Map();
const executorNamesById = new Map();

const ths = [
    { name: 'properties', title: '环境变量', multiLines: true },
    { name: 'title', title: '标题', searchedByKeywords: true },
    { name: 'executor', title: '执行者', type: 'dataSource|defaultValue=0|dataSourceApi=/Hosts/Executors?pageIndex=1&pageSize=1000|displayField=name' },
    { name: 'commands', title: '命令', multiLines: true },
    { name: 'createTime', title: '创建时间' },
    { name: 'updateTime', title: '更新时间' }
];

let activeExecutorId = null;
let tabsElement = null;
let eventsBound = false;

function escapeHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function getCardStatus(id) {
    return cardsStatus.get(id) ?? cardsStatus.get(Number(id)) ?? cardsStatus.get(String(id));
}

function findCard(id) {
    return Array.from(document.querySelectorAll(`${tableParentSelector} .am-card`))
        .find(card => card.dataset.anythingId === String(id));
}

function findCommandItem(commandId, scope = document) {
    return Array.from(scope.querySelectorAll('.am-command-item'))
        .find(item => item.dataset.commandId === String(commandId));
}

function getExecutorName(executorId) {
    return executorNamesById.get(String(executorId)) ?? `执行器 ${executorId}`;
}

function isFileHelper(executorName) {
    return String(executorName).toLowerCase() === 'filehelper';
}

function setCollapseState(card, shown) {
    const collapseElement = card?.querySelector('.collapse');
    const titleButton = card?.querySelector('.card-title button');
    if (!collapseElement) return;

    const Collapse = window.bootstrap?.Collapse;
    if (Collapse) {
        const instance = typeof Collapse.getOrCreateInstance === 'function'
            ? Collapse.getOrCreateInstance(collapseElement, { toggle: false })
            : new Collapse(collapseElement, { toggle: false });
        shown ? instance.show() : instance.hide();
    } else {
        collapseElement.classList.toggle('show', shown);
    }
    titleButton?.setAttribute('aria-expanded', shown ? 'true' : 'false');
}

function createTabs(executors) {
    const nav = document.createElement('ul');
    nav.className = 'nav nav-tabs am-tabs';
    nav.setAttribute('role', 'tablist');

    const appendTab = (label, executorId, active = false) => {
        const item = document.createElement('li');
        item.className = 'nav-item';
        item.setAttribute('role', 'presentation');

        const button = document.createElement('button');
        button.type = 'button';
        button.className = `nav-link${active ? ' active' : ''}`;
        button.dataset.executorId = executorId == null ? '' : String(executorId);
        button.dataset.executorName = executorId == null ? '' : label;
        button.setAttribute('role', 'tab');
        button.setAttribute('aria-selected', active ? 'true' : 'false');
        button.append(document.createTextNode(label));

        const badge = document.createElement('span');
        badge.className = 'am-tab-badge';
        badge.textContent = '0';
        button.appendChild(badge);
        item.appendChild(button);
        nav.appendChild(item);
    };

    appendTab('全部', null, true);
    executors.forEach(executor => appendTab(executor.name, executor.id));
    return nav;
}

function updateTabBadges(data) {
    if (!tabsElement) return;

    if (activeExecutorId == null) {
        tabsElement.querySelectorAll('.am-tab-badge').forEach(badge => badge.textContent = '0');
        const allBadge = tabsElement.querySelector('.nav-link[data-executor-id=""] .am-tab-badge');
        if (allBadge) allBadge.textContent = String(data.length);

        const counts = new Map();
        data.forEach(record => {
            const key = String(record.executor);
            counts.set(key, (counts.get(key) ?? 0) + 1);
        });
        counts.forEach((count, executorId) => {
            const tab = Array.from(tabsElement.querySelectorAll('.nav-link'))
                .find(item => item.dataset.executorId === executorId);
            const badge = tab?.querySelector('.am-tab-badge');
            if (badge) badge.textContent = String(count);
        });
        return;
    }

    const activeTab = Array.from(tabsElement.querySelectorAll('.nav-link'))
        .find(item => item.dataset.executorId === String(activeExecutorId));
    const badge = activeTab?.querySelector('.am-tab-badge');
    if (badge) badge.textContent = String(data.length);
}

function buildDataView(data) {
    cardsStatus.clear();
    updateTabBadges(data);

    const container = document.createElement('div');
    container.className = 'row';
    container.innerHTML = '<div class="col-sm-12 cards-container"></div>';
    const cardsContainer = container.querySelector('.cards-container');

    data.forEach(record => {
        const id = record.id;
        const collapseBtnId = `am-collapse-btn-${id}`;
        const collapseId = `am-collapse-${id}`;
        const executorName = getExecutorName(record.executor);
        cardsStatus.set(id, {
            id,
            isShown: false,
            collapseBtnId,
            commandArray: [],
            originProperties: '',
            resolvedProperties: {},
            executorName
        });

        const card = document.createElement('div');
        card.className = 'card am-card';
        card.dataset.anythingId = String(id);
        card.dataset.executorName = executorName;
        card.innerHTML = `
            <div class="card-header am-card-header" id="am-heading-${escapeHtml(id)}">
                <div class="d-flex align-items-center gap-2">
                    <span class="am-executor-badge" data-type="${escapeHtml(executorName)}">${escapeHtml(executorName)}</span>
                    <h5 class="card-title mb-0" record-id="${escapeHtml(id)}" record-title="${escapeHtml(record.title)}">
                        <button id="${escapeHtml(collapseBtnId)}" class="btn btn-link btn-sm" type="button"
                            data-bs-target="#${escapeHtml(collapseId)}" aria-expanded="false" aria-controls="${escapeHtml(collapseId)}">
                            ${escapeHtml(record.title)}
                        </button>
                    </h5>
                </div>
                <div class="d-flex flex-wrap gap-1">
                    <button type="button" class="btn btn-outline-secondary btn-sm env-vars-btn" data-id="${escapeHtml(id)}" title="环境变量">⚙ 环境变量</button>
                    <button type="button" class="btn btn-primary btn-sm update-btn" data-table-id="${tableId}"
                        data-id="${escapeHtml(id)}" data-fetch-url="${apiUrl}" data-method="POST">更新</button>
                    <button type="button" class="btn btn-primary btn-sm add-command-btn" data-id="${escapeHtml(id)}">添加命令</button>
                    ${isFileHelper(executorName) ? `<button type="button" class="am-quick-edit-btn" data-id="${escapeHtml(id)}">📝 快速编辑</button>` : ''}
                    <button type="button" class="btn btn-primary btn-sm run-commands-btn" data-id="${escapeHtml(id)}">运行</button>
                </div>
            </div>
            <div id="${escapeHtml(collapseId)}" class="collapse" aria-labelledby="am-heading-${escapeHtml(id)}">
                <div class="p-3 pb-0">
                    <div class="am-env-panel" id="am-env-panel-${escapeHtml(id)}" hidden>
                        <div class="d-flex justify-content-between align-items-center mb-2">
                            <h6 class="mb-0">⚙ 环境变量</h6>
                            <button type="button" class="btn-close btn-sm close-env-panel" aria-label="关闭"></button>
                        </div>
                        <div class="row">
                            <div class="col-md-6">
                                <textarea id="am-properties-${escapeHtml(id)}" placeholder="设置变量 (JSON)" name="properties" rows="4" class="form-control form-control-sm mb-2"></textarea>
                                <input type="hidden" id="am-setting-id-${escapeHtml(id)}" name="id" value="${escapeHtml(id)}">
                                <button type="button" class="btn btn-primary btn-sm update-env-btn" data-id="${escapeHtml(id)}"
                                    data-content="formItemIds:am-properties-${escapeHtml(id)};am-setting-id-${escapeHtml(id)}"
                                    data-execute-url="${apiUpdateUrl}" data-method="POST">更新变量</button>
                            </div>
                            <div class="col-md-6">
                                <div class="am-env-resolved"><em>展开卡片后显示解析后的变量</em></div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="card-body"></div>
            </div>`;
        cardsContainer.appendChild(card);
    });

    return container;
}

function renderResolvedProperties(card, properties) {
    const resolved = card.querySelector('.am-env-resolved');
    if (!resolved) return;
    resolved.replaceChildren();

    const entries = Object.entries(properties ?? {});
    if (entries.length === 0) {
        const empty = document.createElement('em');
        empty.textContent = '无环境变量';
        resolved.appendChild(empty);
        return;
    }

    entries.forEach(([key, value]) => {
        const row = document.createElement('div');
        const label = document.createElement('strong');
        label.textContent = `${key}: `;
        row.append(label, document.createTextNode(String(value ?? '')));
        resolved.appendChild(row);
    });
}

function renderCommands(card, anythingSetting, anythingInfo, id) {
    const status = getCardStatus(id);
    const commands = [...(anythingInfo.commands ?? [])]
        .sort((first, second) => Number(first.orderNo) - Number(second.orderNo));
    status.commandArray = commands;

    const originalCommands = anythingSetting.commands ?? [];
    const fileHelper = isFileHelper(anythingInfo.commandExecutor);
    const commandsHtml = commands.map((commandInfo, index) => {
        const command = originalCommands.find(item => String(item.id) === String(commandInfo.id)) ?? commandInfo;
        let commandOrigin = String(command.commandTxt ?? '');
        const commandValue = String(commandInfo.commandTxt ?? '');
        const rows = Math.min(10, Math.max(1, commandOrigin.split(/\r?\n/).length));
        const states = String(commandInfo.executedState ?? '').split(/\r?\n/).filter(Boolean);
        const stateHtml = states.map(state => `<div class="card-text">${escapeHtml(state)}</div>`).join('');
        const disabled = commandInfo.executedState ? ' disabled' : '';

        return `<div class="am-command-item${commands.length === 1 ? ' single' : ''}" data-command-id="${escapeHtml(commandInfo.id)}">
            <div class="input-group mb-3">
                <div class="input-group-text">
                    <input class="form-check-input mt-0 command-checkbox" type="checkbox"
                        aria-label="选择命令 ${escapeHtml(commandInfo.name)}" data-command-id="${escapeHtml(commandInfo.id)}"
                        data-command-name="${escapeHtml(commandInfo.name)}">
                </div>
                <textarea class="form-control form-control-sm command-input-${escapeHtml(commandInfo.id)}" rows="${rows}"
                    aria-label="命令设置" anything-id="${escapeHtml(id)}" data-command-id="${escapeHtml(commandInfo.id)}">${escapeHtml(commandOrigin)}</textarea>
            </div>
            <div class="d-flex justify-content-between gap-2">
                <div><button class="btn btn-sm btn-danger mb-2 run-command-btn${disabled}" type="button"
                    command-id="${escapeHtml(commandInfo.id)}" command-name="${escapeHtml(commandInfo.name)}"${disabled ? ' disabled' : ''}>${escapeHtml(commandInfo.name)}</button></div>
                <div>
                    ${fileHelper ? `<button class="btn btn-sm btn-outline-warning mb-2 visual-edit-btn" type="button" command-id="${escapeHtml(commandInfo.id)}" anything-id="${escapeHtml(id)}" title="可视化编辑">📝</button>` : ''}
                    <button class="btn btn-sm btn-primary mb-2 resolve-command-btn" type="button" command-id="${escapeHtml(commandInfo.id)}">解析模板</button>
                    <button class="btn btn-sm btn-primary mb-2 update-command-btn" type="button" command-id="${escapeHtml(commandInfo.id)}">更新命令</button>
                </div>
            </div>
            <div class="d-flex justify-content-between align-items-center mb-1">
                <small class="text-muted">解析结果</small>
                <button type="button" class="am-clear-output-btn">清空</button>
            </div>
            <div class="am-command-resolved am-scrollable-nobar">${escapeHtml(commandValue).replace(/\r?\n/g, '<br>')}</div>
            <div class="am-command-state">${stateHtml}</div>
            <div class="am-order-arrow up change-order-arrow arrow-up" command-id="${escapeHtml(commandInfo.id)}"
                anything-id="${escapeHtml(id)}" command-index="${index}" commands-length="${commands.length}" order-no="${escapeHtml(commandInfo.orderNo)}"></div>
            <div class="am-order-arrow down change-order-arrow arrow-down" command-id="${escapeHtml(commandInfo.id)}"
                anything-id="${escapeHtml(id)}" command-index="${index}" commands-length="${commands.length}" order-no="${escapeHtml(commandInfo.orderNo)}"></div>
            <button type="button" class="am-remove-btn border-0 bg-transparent" data-id="${escapeHtml(id)}"
                command-id="${escapeHtml(commandInfo.id)}" command-index="${index}" command-name="${escapeHtml(commandInfo.name)}" aria-label="删除命令">❌</button>
        </div>`;
    }).join('');

    card.querySelector('.card-body').innerHTML = commandsHtml || '<p class="text-muted mb-0">暂无命令</p>';
}

async function resolveCmdSettingAsync(input) {
    const id = input.getAttribute('anything-id');
    const commandId = input.dataset.commandId;
    const status = getCardStatus(id);
    const command = status?.commandArray.find(item => String(item.id) === String(commandId));
    if (command) command.commandTxt = input.value;

    const data = await httpRequestDataAsync(
        '/Hosts/ResolveCommandSettting',
        input,
        'POST',
        JSON.stringify({ id, cmdTxt: input.value }),
        'application/json',
        errorHandlerType.returnErrorMessage
    );
    const output = input.closest('.am-command-item')?.querySelector('.am-command-resolved');
    if (output && data != null) output.textContent = String(data);
}

async function loadCommandsAsync(element, id, refresh = false) {
    const card = element.closest('.am-card') ?? findCard(id);
    const status = getCardStatus(id);
    if (!card || !status) return;

    if (!refresh && status.isShown) {
        status.isShown = false;
        card.classList.remove('expanded');
        setCollapseState(card, false);
        return;
    }

    if (status.loading) return status.loading;
    status.loading = (async () => {
        const data = await httpRequestDataAsync(`/Hosts/AnythingSettingAndInfo?id=${encodeURIComponent(id)}`, element, 'GET');
        if (!data) return;

        const anythingSetting = data.anythingSetting;
        const anythingInfo = data.anythingInfo;
        status.originProperties = anythingSetting.properties ?? '';
        status.resolvedProperties = anythingInfo.properties ?? {};
        status.executorName = anythingInfo.commandExecutor ?? status.executorName;

        renderCommands(card, anythingSetting, anythingInfo, id);
        const propertiesTextarea = card.querySelector(`#am-properties-${id}`);
        if (propertiesTextarea) propertiesTextarea.value = status.originProperties;
        renderResolvedProperties(card, status.resolvedProperties);

        status.isShown = true;
        card.classList.add('expanded');
        setCollapseState(card, true);
    })();

    try {
        await status.loading;
    } finally {
        status.loading = null;
    }
}

async function changeOrderAsync(trigger, id, currentCommandIndex, length, step) {
    const commandCount = Number(length);
    if (!step || commandCount < 2) return;

    let nextIndex = Number(currentCommandIndex) + Number(step);
    if (nextIndex < 0) nextIndex += commandCount;
    if (nextIndex >= commandCount) nextIndex -= commandCount;
    if (nextIndex === Number(currentCommandIndex)) return;

    const params = {
        id: trigger.getAttribute('command-id'),
        orderNo: String(Number(trigger.getAttribute('order-no')) + Number(step))
    };
    const executeData = {
        dataContent: JSON.stringify(params),
        dataExecuteUrl: '/Hosts/UpdateCommand',
        dataMethod: 'POST',
        trigger
    };
    execute(executeData, () => {
        const card = findCard(id);
        if (card) loadCommandsAsync(card.querySelector('.card-title'), id, true);
    });
}

async function removeCommandItemAsync(trigger, id, commandId, removedIndex) {
    const status = getCardStatus(id);
    const index = Number(removedIndex);
    if (status && Number.isInteger(index) && index >= 0) status.commandArray.splice(index, 1);
    trigger.closest('.am-command-item')?.remove();
    await removeCommandAsync(id, commandId);
}

async function removeCommandAsync(id, commandId) {
    const trigger = {
        dataContent: JSON.stringify({ commandId }),
        dataExecuteUrl: `/Hosts/DeleteAnythingCommandById?id=${encodeURIComponent(commandId)}`,
        dataMethod: 'POST'
    };
    await execute(trigger, () => {
        const card = findCard(id);
        if (card) loadCommandsAsync(card.querySelector('.card-title'), id, true);
    });
}

async function addCommand(eventTrigger, id) {
    const card = eventTrigger.closest('.am-card') ?? findCard(id);
    const status = getCardStatus(id);
    if (!card || !status) return;

    if (!status.isShown) await loadCommandsAsync(card.querySelector('.card-title'), id);
    const cardBody = card.querySelector('.card-body');
    const existing = cardBody.querySelector('.am-new-command');
    if (existing) {
        existing.querySelector('input')?.focus();
        return;
    }

    cardBody.insertAdjacentHTML('afterbegin', `<div class="am-command-item am-new-command row g-2 align-items-center">
        <div class="col">
            <label for="am-command-name-${escapeHtml(id)}" class="visually-hidden">命令名称</label>
            <input type="text" class="form-control form-control-sm" id="am-command-name-${escapeHtml(id)}" placeholder="命令名称">
        </div>
        <div class="col-auto">
            <button type="button" class="btn btn-primary btn-sm add-command-confirm-btn" data-id="${escapeHtml(id)}">确定</button>
        </div>
    </div>`);
    cardBody.querySelector('.am-new-command input')?.focus();
}

async function addCommandPost(eventTrigger, id) {
    const input = eventTrigger.closest('.am-command-item')?.querySelector('input');
    const name = input?.value.trim();
    if (!name) {
        input?.focus();
        return;
    }

    const command = { Name: name, CommandTxt: '', ExecutedState: '' };
    getCardStatus(id)?.commandArray.push(command);
    await addAnythingCommandAsync(id, command);
}

async function addAnythingCommandAsync(id, command) {
    command.anythingId = String(id);
    const trigger = {
        dataContent: JSON.stringify(command),
        dataExecuteUrl: '/Hosts/AddCommand',
        dataMethod: 'POST'
    };
    await execute(trigger, () => {
        const card = findCard(id);
        if (card) loadCommandsAsync(card.querySelector('.card-title'), id, true);
    });
}

async function updateCommandAsync(id) {
    const commandItem = findCommandItem(id, document.querySelector(tableParentSelector));
    const input = commandItem?.querySelector('textarea');
    const card = commandItem?.closest('.am-card');
    if (!input || !card) return;

    const trigger = {
        dataContent: JSON.stringify({ id: String(id), commandTxt: input.value }),
        dataExecuteUrl: '/Hosts/UpdateCommand',
        dataMethod: 'POST',
        trigger: commandItem.querySelector('.update-command-btn')
    };
    await execute(trigger, () => loadCommandsAsync(card.querySelector('.card-title'), card.dataset.anythingId, true), true, true);
}

async function executeCommand(commandId, commandName, executeBtn) {
    const root = executeBtn?.closest('.am-card') ?? document.querySelector(tableParentSelector);
    const commandItem = findCommandItem(commandId, root) ?? findCommandItem(commandId);
    const output = commandItem?.querySelector('.am-command-resolved');
    if (output) {
        output.replaceChildren();
        delete output.dataset.sseWritten;
    }

    commandItem?.classList.remove('am-command-queued', 'am-command-done');
    commandItem?.classList.add('am-command-executing');
    const requestKey = `${commandName}-${commandId}-${Date.now()}`;
    try {
        await sendSseRequestCommon(
            '/Hosts/ExecuteCommand',
            JSON.stringify({ commandId }),
            requestKey,
            executeBtn,
            output,
            null,
            (data, _requestTitle, container) => commandResultHandler(data, commandName, container),
            60 * 3
        );
    } finally {
        commandItem?.classList.remove('am-command-executing', 'am-command-queued');
        commandItem?.classList.add('am-command-done');
        commandItem?.removeAttribute('data-order');
    }
}

async function executeCommands(trigger) {
    const card = trigger.closest('.am-card');
    const checkboxes = Array.from(card?.querySelectorAll('.command-checkbox:checked') ?? []);
    if (checkboxes.length === 0) {
        showErrorBox('请选择需要执行的命令');
        return;
    }

    checkboxes.forEach((checkbox, index) => {
        const item = checkbox.closest('.am-command-item');
        item?.classList.remove('am-command-done', 'am-command-executing');
        item?.classList.add('am-command-queued');
        if (item) item.dataset.order = String(index + 1);
    });

    for (const checkbox of checkboxes) {
        const item = checkbox.closest('.am-command-item');
        item?.classList.remove('am-command-queued');
        await executeCommand(checkbox.dataset.commandId, checkbox.dataset.commandName, trigger);
    }
}

function openTemplateEditor(input, anythingId) {
    const status = getCardStatus(anythingId);
    if (!input || !status) return;
    if (!window.TemplateEditor) {
        showErrorBox('模板编辑器未加载，请刷新页面重试');
        return;
    }
    const inputClass = Array.from(input.classList).find(name => name.startsWith('command-input-'));
    window.TemplateEditor.showEditor(inputClass, status.originProperties || '{}', status.resolvedProperties || {}, anythingId);
}

async function quickEdit(trigger) {
    const card = trigger.closest('.am-card');
    const id = trigger.dataset.id;
    const status = getCardStatus(id);
    if (!card || !status) return;
    if (!status.isShown) await loadCommandsAsync(card.querySelector('.card-title'), id);

    const firstInput = card.querySelector('.am-command-item textarea[data-command-id]');
    if (!firstInput) {
        showErrorBox('当前配置没有可编辑的命令');
        return;
    }
    openTemplateEditor(firstInput, id);
}

async function toggleEnvironmentPanel(button) {
    const card = button.closest('.am-card');
    const id = button.dataset.id;
    const status = getCardStatus(id);
    if (!card || !status) return;
    if (!status.isShown) await loadCommandsAsync(card.querySelector('.card-title'), id);

    const panel = card.querySelector('.am-env-panel');
    if (!panel) return;
    panel.hidden = !panel.hidden;
    button.classList.toggle('btn-outline-secondary', panel.hidden);
    button.classList.toggle('btn-secondary', !panel.hidden);
}

function closeEnvironmentPanel(button) {
    const card = button.closest('.am-card');
    const panel = button.closest('.am-env-panel');
    if (panel) panel.hidden = true;
    const envButton = card?.querySelector('.env-vars-btn');
    envButton?.classList.add('btn-outline-secondary');
    envButton?.classList.remove('btn-secondary');
}

function bindEvents() {
    if (eventsBound) return;
    const container = document.querySelector(tableParentSelector);
    if (!container) return;
    eventsBound = true;

    container.addEventListener('click', async event => {
        const target = event.target.closest('button, .change-order-arrow, .card-title');
        if (!target || !container.contains(target)) return;

        const cardTitle = target.closest('.card-title');
        if (cardTitle) {
            event.preventDefault();
            event.stopPropagation();
            await loadCommandsAsync(cardTitle, cardTitle.getAttribute('record-id'));
            return;
        }

        if (target.matches('.env-vars-btn')) {
            event.preventDefault();
            await toggleEnvironmentPanel(target);
        } else if (target.matches('.close-env-panel')) {
            event.preventDefault();
            closeEnvironmentPanel(target);
        } else if (target.matches('.update-env-btn')) {
            event.preventDefault();
            showConfirmBox('确定更新变量吗?', () => execute(target, null, null, true));
        } else if (target.matches('.update-btn')) {
            event.preventDefault();
            const id = target.dataset.id;
            showUpdatePannel(
                target,
                () => {
                    const status = getCardStatus(id);
                    if (status) status.isShown = false;
                },
                () => {
                    const card = findCard(id);
                    if (card) loadCommandsAsync(card.querySelector('.card-title'), id, true);
                }
            );
        } else if (target.matches('.add-command-btn')) {
            event.preventDefault();
            await addCommand(target, target.dataset.id);
        } else if (target.matches('.add-command-confirm-btn')) {
            event.preventDefault();
            await addCommandPost(target, target.dataset.id);
        } else if (target.matches('.run-commands-btn')) {
            event.preventDefault();
            await executeCommands(target);
        } else if (target.matches('.run-command-btn')) {
            event.preventDefault();
            await executeCommand(target.getAttribute('command-id'), target.getAttribute('command-name'), target);
        } else if (target.matches('.resolve-command-btn')) {
            event.preventDefault();
            const input = target.closest('.am-command-item')?.querySelector('textarea');
            if (input) await resolveCmdSettingAsync(input);
        } else if (target.matches('.update-command-btn')) {
            event.preventDefault();
            await updateCommandAsync(target.getAttribute('command-id'));
        } else if (target.matches('.visual-edit-btn')) {
            event.preventDefault();
            const input = target.closest('.am-command-item')?.querySelector('textarea');
            openTemplateEditor(input, target.getAttribute('anything-id'));
        } else if (target.matches('.change-order-arrow')) {
            event.preventDefault();
            const step = target.classList.contains('arrow-up') ? -1 : 1;
            await changeOrderAsync(
                target,
                target.getAttribute('anything-id'),
                Number(target.getAttribute('command-index')),
                Number(target.getAttribute('commands-length')),
                step
            );
        } else if (target.matches('.am-remove-btn')) {
            event.preventDefault();
            const name = target.getAttribute('command-name');
            showConfirmBox(`确定移除命令[${name}]吗?`, () => removeCommandItemAsync(
                target,
                target.dataset.id,
                target.getAttribute('command-id'),
                target.getAttribute('command-index')
            ));
        } else if (target.matches('.am-clear-output-btn')) {
            event.preventDefault();
            const output = target.closest('.am-command-item')?.querySelector('.am-command-resolved');
            if (output) {
                output.replaceChildren();
                delete output.dataset.sseWritten;
            }
        } else if (target.matches('.am-quick-edit-btn')) {
            event.preventDefault();
            await quickEdit(target);
        }
    });

    container.addEventListener('submit', event => {
        if (event.target.id !== 'search-form') return;
        event.preventDefault();
        event.stopImmediatePropagation();

        const table = tables[tableId];
        table.dataFilter.keywords.value = event.target.querySelector('#search-input')?.value.trim() ?? '';
        table.dataFilter.filterItems = activeExecutorId == null
            ? []
            : [{ fieldName: 'executor', compareType: '=', value: activeExecutorId }];
        table.pageIndex = 1;
        table.loadData();
    }, true);

    tabsElement?.addEventListener('click', async event => {
        const tab = event.target.closest('.nav-link');
        if (!tab || tab.classList.contains('active')) return;
        tabsElement.querySelectorAll('.nav-link').forEach(item => {
            const active = item === tab;
            item.classList.toggle('active', active);
            item.setAttribute('aria-selected', active ? 'true' : 'false');
        });

        activeExecutorId = tab.dataset.executorId === ''
            ? null
            : (executorMap.get(tab.dataset.executorName) ?? Number(tab.dataset.executorId));
        cardsStatus.clear();
        const table = tables[tableId];
        table.dataFilter.filterItems = activeExecutorId == null
            ? []
            : [{ fieldName: 'executor', compareType: '=', value: activeExecutorId }];
        table.pageIndex = 1;
        await table.loadData();
    });
}

async function initializeAnythingManager() {
    const container = document.querySelector(tableParentSelector);
    if (!container) return;

    const executorPage = await httpRequestDataAsync('/Hosts/Executors?pageIndex=1&pageSize=1000', container, 'GET');
    const executors = Array.isArray(executorPage) ? executorPage : (executorPage?.data ?? []);
    executors.forEach(executor => {
        executorMap.set(executor.name, executor.id);
        executorNamesById.set(String(executor.id), executor.name);
    });

    tabsElement = createTabs(executors);
    container.prepend(tabsElement);

    await createTable({
        apiUrl,
        pageIndex: 1,
        pageSize: 100,
        tableId,
        tableContainerSelector: tableParentSelector,
        ths,
        idFieldName: 'id',
        dataViewBuilder: buildDataView,
        modalSettings: {
            url: '/Hosts/AddAnythingSetting',
            method: 'POST',
            updateUrl: apiUpdateUrl,
            updateMethod: 'POST'
        },
        primaryKeyIsInt: true,
        addButtonSelector: 'h1',
        orderRules: [{ fieldName: 'title', isAsc: false }],
        filterItems: []
    });

    container.prepend(tabsElement);
    bindEvents();
}

initializeAnythingManager().catch(error => {
    console.error(error);
    showErrorBox(error.message || 'Anything Manager 初始化失败');
});
