// 保存原生方法
const nativeElementQuerySelector = Element.prototype.querySelector;
const nativeDocumentQuerySelector = Document.prototype.querySelector;
function ytCustomQuerySelector(selector) {
    // 第二步：尝试用选择器获取DOM元素
    // 执行原生选择器查询
    const foundElement = this === document ?
        nativeDocumentQuerySelector.call(this, selector) :
        nativeElementQuerySelector.call(this, selector);

    if (foundElement) {
        // 设置属性
        if (!foundElement.hasAttribute('data-selectorname')) {
            foundElement.setAttribute('data-selectorname', selector);
        }
        // 第三步：直接返回找到的元素
        return foundElement;
    }

    // 如果通过选择器没找到，尝试通过data-selectorName属性查找
    const allElements = document.querySelectorAll('[data-selectorname]');
    for (let i = 0; i < allElements.length; i++) {
        if (allElements[i].getAttribute('data-selectorname') === selector) {
            return allElements[i];
        }
    }

    // 如果都没找到，返回null
    return null;
}

// 如果需要也重写querySelectorAll，可以类似实现
// 重写原生的querySelector
Document.prototype.querySelector = ytCustomQuerySelector
Element.prototype.querySelector = ytCustomQuerySelector

const nativeElementInsertBefore = Element.prototype.insertBefore;

function ytCustomInsertBefore(newNode, referenceNode) {
    // 当前元素作为默认父元素
    const defaultParentNode = this;

    // 检查参考节点是否存在
    if (!referenceNode) {
        // 如果没有提供参考节点，直接添加到末尾
        return nativeElementInsertBefore.call(defaultParentNode, newNode, null);
    }

    // 检查参考节点是否仍然是父节点的直接子节点
    if (referenceNode.parentNode === defaultParentNode) {
        // 正常情况：参考节点仍在父节点下，直接插入
        return nativeElementInsertBefore.call(defaultParentNode, newNode, referenceNode);
    }

    // 检查参考节点是否有 data-ytparentvalue 属性（被移动出去的节点）
    const referenceParentValue = referenceNode.getAttribute('data-ytparentvalue');

    if (referenceParentValue) {
        // 查找具有匹配 data-ytextravalue 的父元素
        const actualParentNode = document.querySelector('[data-ytextravalue="' + referenceParentValue + '"]');

        if (actualParentNode) {
            // 获取参考节点原来的索引位置
            const originalIndex = referenceNode.getAttribute('data-ytoriginindex');

            if (originalIndex !== null && !isNaN(originalIndex)) {
                // 获取实际父节点当前的所有子节点
                const children = Array.from(actualParentNode.children);

                // 查找应该插入的位置
                for (let i = 0; i < children.length; i++) {
                    const child = children[i];
                    const childOriginalIndex = child.getAttribute('data-ytoriginindex');

                    // 如果子节点有原始索引，并且比参考节点的原始索引大
                    if (childOriginalIndex !== null && !isNaN(childOriginalIndex)) {
                        if (parseInt(childOriginalIndex) > parseInt(originalIndex)) {
                            // 找到第一个索引更大的节点，插入到它前面
                            return nativeElementInsertBefore.call(actualParentNode, newNode, child);
                        }
                    }
                }

                // 如果没有找到更大的索引，插入到最后
                return nativeElementInsertBefore.call(actualParentNode, newNode, null);
            }

            // 没有原始索引信息，插入到实际父元素的最后
            return nativeElementInsertBefore.call(actualParentNode, newNode, null);
        }
    }

    // 默认情况：插入到当前父元素的最后
    return nativeElementInsertBefore.call(defaultParentNode, newNode, null);
}

// 重写原生 insertBefore 方法
Element.prototype.insertBefore = ytCustomInsertBefore;

// 需要给新添加的a标签跳转链接加入一些必要的样式 保证加入后不影响原来的布局
function addUniqueStyle(cssText, id = 'custom-style') {
    const targetDom = document.getElementById(id)
    if (targetDom && targetDom.tagName === 'STYLE') return; // 已存在则跳过

    const style = document.createElement('style');
    style.id = id;
    style.innerHTML = cssText;
    document.head.appendChild(style);
}
addUniqueStyle('.yt-a-defalut-link[custom-a="true"] > * { margin:0;flex:1; }')

// 定义要劫持的属性
const ytCustomProperties = ['textContent', 'innerText'];

ytCustomProperties.forEach(prop => {
    let descriptor = Object.getOwnPropertyDescriptor(Element.prototype, prop) ||
        Object.getOwnPropertyDescriptor(Node.prototype, prop);
    if (descriptor && descriptor.set && descriptor.get) {
        const originalGet = descriptor.get; // 保存原生 getter
        const originalSet = descriptor.set;
        Object.defineProperty(Element.prototype, prop, {
            get: function () {
                return originalGet.call(this); // 保持原生 getter 逻辑
            },
            set: function (value) {
                // 优先取 data-yteditvalue，否则用传入的 value
                const finalValue = this.dataset.yteditvalue ?? value;
                originalSet.call(this, finalValue);
            },
            configurable: true,
        });
    }
});