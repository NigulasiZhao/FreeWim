/**
 * Dashboard 统一工具库
 * 提供通用功能: 表格排序、分页、加载状态、Toast提示等
 */

// ==================== 表格排序 ====================
class TableSorter {
    constructor(tableId) {
        this.table = document.getElementById(tableId);
        this.tbody = this.table?.querySelector('tbody');
        this.currentSort = { column: null, direction: 'asc' };
        this.init();
    }

    init() {
        if (!this.table) return;

        const headers = this.table.querySelectorAll('th.sortable');
        headers.forEach((header, index) => {
            header.addEventListener('click', () => this.sort(index, header));
        });
    }

    sort(columnIndex, header) {
        const rows = Array.from(this.tbody.querySelectorAll('tr'));

        // 切换排序方向
        if (this.currentSort.column === columnIndex) {
            this.currentSort.direction = this.currentSort.direction === 'asc' ? 'desc' : 'asc';
        } else {
            this.currentSort.direction = 'asc';
        }
        this.currentSort.column = columnIndex;

        // 更新表头样式
        this.table.querySelectorAll('th.sortable').forEach(th => {
            th.classList.remove('asc', 'desc');
        });
        header.classList.add(this.currentSort.direction);

        // 排序行
        rows.sort((a, b) => {
            const aValue = a.cells[columnIndex]?.textContent.trim() || '';
            const bValue = b.cells[columnIndex]?.textContent.trim() || '';

            // 尝试数字比较
            const aNum = parseFloat(aValue.replace(/[^0-9.-]/g, ''));
            const bNum = parseFloat(bValue.replace(/[^0-9.-]/g, ''));

            if (!isNaN(aNum) && !isNaN(bNum)) {
                return this.currentSort.direction === 'asc' ? aNum - bNum : bNum - aNum;
            }

            // 字符串比较
            return this.currentSort.direction === 'asc'
                ? aValue.localeCompare(bValue, 'zh-CN')
                : bValue.localeCompare(aValue, 'zh-CN');
        });

        // 重新插入排序后的行
        rows.forEach(row => this.tbody.appendChild(row));
    }
}

// ==================== Toast 提示 ====================
class Toast {
    static show(message, type = 'success', duration = 3000) {
        const container = this.getContainer();

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.textContent = message;

        container.appendChild(toast);

        // 动画进入
        setTimeout(() => toast.classList.add('show'), 10);

        // 自动移除
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    }

    static getContainer() {
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                display: flex;
                flex-direction: column;
                gap: 10px;
            `;
            document.body.appendChild(container);
        }
        return container;
    }

    static success(message) {
        this.show(message, 'success');
    }

    static error(message) {
        this.show(message, 'error');
    }

    static warning(message) {
        this.show(message, 'warning');
    }

    static info(message) {
        this.show(message, 'info');
    }
}

// Toast 样式
const toastStyles = document.createElement('style');
toastStyles.textContent = `
    .toast {
        padding: 12px 20px;
        border-radius: 8px;
        font-size: 14px;
        font-weight: 500;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        opacity: 0;
        transform: translateX(100px);
        transition: opacity 0.3s, transform 0.3s;
        min-width: 200px;
        font-family: 'Fira Sans', sans-serif;
    }
    .toast.show {
        opacity: 1;
        transform: translateX(0);
    }
    .toast-success {
        background: #10B981;
        color: white;
    }
    .toast-error {
        background: #EF4444;
        color: white;
    }
    .toast-warning {
        background: #F59E0B;
        color: white;
    }
    .toast-info {
        background: #3B82F6;
        color: white;
    }
`;
document.head.appendChild(toastStyles);

// ==================== 加载状态 ====================
class LoadingOverlay {
    static show(container = document.body) {
        // 先移除已存在的overlay,避免重复
        this.hide();

        const overlay = document.createElement('div');
        overlay.className = 'loading-overlay';
        overlay.innerHTML = '<div class="spinner"></div>';
        overlay.id = 'loading-overlay';
        container.style.position = 'relative';
        container.appendChild(overlay);
    }

    static hide() {
        const overlay = document.getElementById('loading-overlay');
        if (overlay) {
            overlay.remove();
        }
    }
}

// ==================== API 请求封装 ====================
class API {
    static async get(url, params = {}) {
        try {
            const queryString = new URLSearchParams(params).toString();
            const fullUrl = queryString ? `${url}?${queryString}` : url;

            const response = await fetch(fullUrl);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            return await response.json();
        } catch (error) {
            console.error('API GET Error:', error);
            Toast.error('数据加载失败');
            throw error;
        }
    }

    static async post(url, data = {}) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });

            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            return await response.json();
        } catch (error) {
            console.error('API POST Error:', error);
            Toast.error('操作失败');
            throw error;
        }
    }
}

// ==================== 日期格式化 ====================
class DateFormatter {
    static format(date, format = 'YYYY-MM-DD') {
        if (!date) return '';
        const d = new Date(date);
        if (isNaN(d.getTime())) return '';

        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        const hours = String(d.getHours()).padStart(2, '0');
        const minutes = String(d.getMinutes()).padStart(2, '0');
        const seconds = String(d.getSeconds()).padStart(2, '0');

        return format
            .replace('YYYY', year)
            .replace('MM', month)
            .replace('DD', day)
            .replace('HH', hours)
            .replace('mm', minutes)
            .replace('ss', seconds);
    }

    static today() {
        return this.format(new Date(), 'YYYY-MM-DD');
    }

    static monthStart() {
        const d = new Date();
        d.setDate(1);
        return this.format(d, 'YYYY-MM-DD');
    }

    static monthEnd() {
        const d = new Date();
        d.setMonth(d.getMonth() + 1, 0);
        return this.format(d, 'YYYY-MM-DD');
    }
}

// ==================== 数字格式化 ====================
class NumberFormatter {
    static format(num, decimals = 2) {
        if (num === null || num === undefined || isNaN(num)) return '0';
        return Number(num).toFixed(decimals);
    }

    static formatHours(hours) {
        if (!hours) return '0小时';
        const h = Math.floor(hours);
        const m = Math.round((hours - h) * 60);
        return m > 0 ? `${h}小时${m}分钟` : `${h}小时`;
    }

    static percentage(value, total) {
        if (!total || total === 0) return '0%';
        return ((value / total) * 100).toFixed(1) + '%';
    }
}

// ==================== 表格数据导出 ====================
class TableExporter {
    static toCSV(tableId, filename = 'export.csv') {
        const table = document.getElementById(tableId);
        if (!table) return;

        let csv = [];
        const rows = table.querySelectorAll('tr');

        rows.forEach(row => {
            const cols = row.querySelectorAll('td, th');
            const rowData = Array.from(cols).map(col => {
                let text = col.textContent.trim();
                // 转义逗号和引号
                if (text.includes(',') || text.includes('"')) {
                    text = '"' + text.replace(/"/g, '""') + '"';
                }
                return text;
            });
            csv.push(rowData.join(','));
        });

        const csvContent = '\uFEFF' + csv.join('\n'); // BOM for Excel UTF-8
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.click();

        Toast.success('导出成功');
    }
}

// ==================== 全局暴露 ====================
window.TableSorter = TableSorter;
window.Toast = Toast;
window.LoadingOverlay = LoadingOverlay;
window.API = API;
window.DateFormatter = DateFormatter;
window.NumberFormatter = NumberFormatter;
window.TableExporter = TableExporter;
