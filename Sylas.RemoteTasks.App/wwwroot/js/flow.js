const tpl = 
`<style>
    header {
        font-size: 18px;
    }
    ul {
        margin: 0;
        padding-left: 20px;
    }
    .form-item {
        display: flex;
        justify-content: space-between;
        margin-top: 5px;
    }
    input, textarea, select {
        border: 0;
    }
  </style>

<div style="margin-left: 10%; margin-top:20px; width: 80%; max-height: 80vh; overflow-y: auto; border: 1px solid #fff; border-radius: 10px; box-shadow: 20px; z-index: 99999; background: rgba(245, 245, 245, 0.5); backdrop-filter: blur(20rpx);padding-right:20px;">
    <ul style="list-style:none; color: #555;">
        <li style="padding:10px;border-bottom: 1px solid #ccc;font-size:14px;line-height:1.5;">
            <div class="news-header" style="display: flex; justify-content: space-between; align-items: center; cursor: pointer;">
                <span>{{title}}</span>
                <span>
                    <span>{{url}}</span>
                    <span class="icon" style="transition: transform .3s ease;display:inline-block;">▶</span>
                </span>
            </div>
            <div style="max-height:0;overflow:hidden;transition: max-height .2s ease;">
                <div style="color: #555; padding-left: 20px;">
                    <div class="form-item">
                        <span class="input-group-text">url: </span>
                        <input type="text" placeholder="url" name="url">
                    </div>
                    <div class="form-item">
                        <span class="input-group-text">method: </span>
                        <select name="method" style="width: 166px;">
                            <option value="get">get</option>
                            <option value="post">post</option>
                        </select>
                    </div>
                    <div class="form-item">
                        <span class="input-group-text">content-type: </span>
                        <select name="contentType" style="width: 166px;">
                            <option value="application/json">application/json</option>
                            <option value="application/x-www-form-urlencoded">application/x-www-form-urlencoded</option>
                            <option value="multipart/form-data;boundary=aljflajflj2324LJLJsfj">multipart/form-data;boundary=aljflajflj2324LJLJsfj</option>
                        </select>
                    </div>
                    <div class="form-item">
                        <span class="input-group-text">authorization: </span>
                        <input type="text" placeholder="Bearer xxx" name="authorization">
                    </div>
                    <div class="form-item">
                        <span class="input-group-text">body: </span>
                        <slot name="body"><textarea name="body" style="padding:0;width: 166px; height: 92px;"></textarea></slot>
                    </div>
                    {{content}}
                </div>
            </div>
        </li>
    </ul>
</div>
`

class SelfDiv extends HTMLElement {
    constructor() {
        super()
        
        let shadow = this.attachShadow({ mode: 'open' })
        this._data = {
            title: this.attributes.title && this.attributes.title.value !== "undefined" ? this.attributes.title.value : '',
            content: this.attributes.content && this.attributes.content.value !== "undefined" ? this.attributes.content.value : '',
            url: this.attributes.url ? this.attributes.url.value : '',
            method: this.attributes.method ? this.attributes.method.value : '',
            contentType: this.attributes['content-type'] ? this.attributes['content-type'].value : '',
            authorization: this.attributes.authorization && this.attributes.authorization.value !== "undefined" ? this.attributes.authorization.value : '',
            body: this.attributes.body && this.attributes.body.value !== "undefined" ? this.attributes.body.value.replaceAll('\'', '"') : '',
        }
        this.render(shadow);
    }

    render(shadow) {
        let html = tpl
        html = html.replace('{{title}}', `<header>${this._data.title}</header>`)
        html = html.replace('{{content}}', `<section>${this._data.content}</section>`)
        html = html.replace('{{url}}', this._data.url)
        shadow.innerHTML = html
        const methodSelect = shadow.querySelector('select[name="method"]')
        if (methodSelect && this._data.method) {
            methodSelect.value = this._data.method.toLowerCase()
        }
        const contentTypeSelect = shadow.querySelector('select[name="contentType"]')
        if (contentTypeSelect && this._data.contentType) {
            contentTypeSelect.value = this._data.contentType.toLowerCase()
        }

        shadow.querySelector('input[name="url"]').value = this._data.url
        shadow.querySelector('textarea[name="body"]').value = this._data.body
        shadow.querySelector('input[name="authorization"]').value = this._data.authorization

        // 处理事件
        const liList = shadow.querySelectorAll('li');
        if (liList.length > 0) {
            liList[liList.length - 1].style.border = '0';
        }

        var newsHeaders = shadow.querySelectorAll('.news-header');
        for (var i = 0; i < newsHeaders.length; i++) {
            var header = newsHeaders[i];
            header.addEventListener('click', function () {
                const content = header.nextElementSibling;
                const icon = header.querySelector('.icon');
                const isOpen = content.style.maxHeight && content.style.maxHeight !== '0px';

                if (isOpen) {
                    icon.style.transform = '';
                    content.style.maxHeight = '0px';
                } else {
                    icon.style.transform = 'rotate(90deg)';
                    content.style.maxHeight = content.scrollHeight + 'px';
                }
            });
        }
    }
}
customElements.define('flow-node', SelfDiv)