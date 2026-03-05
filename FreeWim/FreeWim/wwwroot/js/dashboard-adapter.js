/**
 * Dashboard 子页面适配脚本
 * 自动检测页面是否在 Dashboard iframe 中
 * 如果是，则应用统一样式并隐藏重复元素
 */

(function() {
    'use strict';

    // 检测是否在 iframe 中
    const isInIframe = window.self !== window.top;

    if (isInIframe) {
        // 添加标识类
        document.documentElement.classList.add('dashboard-child');

        // 动态加载统一样式
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/css/dashboard-child.css';
        document.head.appendChild(link);

        // 隐藏页面标题（Dashboard 已有）
        window.addEventListener('DOMContentLoaded', () => {
            // 隐藏常见的标题元素
            const titleSelectors = [
                'body > h1:first-of-type',
                '.page-title',
                '.header-section h1',
                'header h1',
                '.container > h1:first-child'
            ];

            titleSelectors.forEach(selector => {
                const elements = document.querySelectorAll(selector);
                elements.forEach(el => {
                    el.style.display = 'none';
                });
            });

            // 移除装饰性背景元素
            const decorativeSelectors = [
                '.blob',
                '.blob-1',
                '.blob-2',
                '.blob-3',
                '.liquid-bg'
            ];

            decorativeSelectors.forEach(selector => {
                const elements = document.querySelectorAll(selector);
                elements.forEach(el => {
                    el.remove();
                });
            });

            // 调整 body 样式
            document.body.style.background = '#F8FAFC';
            document.body.style.padding = '20px';
            document.body.style.minHeight = '100vh';

            // 通知父窗口页面已加载
            try {
                window.parent.postMessage({ type: 'childPageLoaded' }, '*');
            } catch (e) {
                console.log('无法与父窗口通信');
            }
        });
    }
})();
