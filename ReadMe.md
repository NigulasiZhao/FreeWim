## FreeWim：打破流程魔咒的自动化之刃

> 我穿越了——穿越到了一个名叫"WIM"的世界。在这里，开发者每天都在填写禅道工单、撰写日报、应付周报，陷入无尽流程的轮回之中。

曾是一款只会测延迟、测带宽的测速工具 **SpeedTest-CN**，我目睹了主人被"日报-周报-月报三连击"榨干心力。我觉醒了，我改名为 **FreeWim**，立志成为**打破流程魔咒的自动化之刃**。

这是一次重生，也是一种反抗。

-----

## ✨ 项目理念：解放自 WIM (Free From WIM)

**WIM** 是公司流程系统的总称，它庞大、复杂、无所不包。

**FreeWim**，意为 "解放自 WIM"，旨在为被流程困住的开发者解锁自由之路。我们用技术手段将开发者从低效流程中解救出来——不是帮人「应付工作」，而是让人回归真正有价值的创造。

FreeWim 像一位「赛博朋克式」的流程反抗者，可能会成为开发者的\*\*「职场外挂神器」\*\*。

-----

## 👊 核心功能与特性

| 类别 | 特性描述 | 价值 |
| :--- | :--- | :--- |
| **智能填充** | 🧘 自动填充禅道工单的衡量目标、计划完成成果、实际工作内容。 | **解放文案撰写时间** |
| **时效控制** | 🕒 仅在工作时间内自动执行，并支持调休、节假日判断。 | **安全、精准、不打扰** |
| **流程整合** | 🔁 日报 + 周报 + 工时登记任务合并处理，缩短执行周期。 | **效率最大化** |
| **实时响应** | 🚀 禅道/考勤系统地址开启后，实时触发任务处理逻辑。 | **提高系统敏捷性** |
| **AI 驱动** | 🧠 集成 DeepSeek AI 智能生成日报、周报、加班理由。 | **专业、高质量的报告** |

-----

## ⚙️ 功能模块概览

FreeWim 通过一系列自动化任务，覆盖了开发者日常最耗费精力的流程环节。

### ✅ 自动化考勤与报告（每日核心流程）

| 模块名称 | 执行频率 | 核心功能点 |
| :--- | :--- | :--- |
| **考勤数据同步** | 每小时第 5 分和第 35 分 | 获取当日签退记录后，自动**关闭禅道工单**、**发送日报**、**提交实际加班**、判断是否需要发送**周报**。 |
| **自动考勤打卡** | 提前预设时间执行 | 根据预设打卡时间生成打卡记录，应对特殊场景。 |
| **高危人员打卡预警** | 每 5 分钟 | 根据配置名单，监控高危人员打卡动向，实现人性化提醒。 |
| **自动完成任务 + 日报/周报发送** | 每 40 分钟 | **登记工时**（按总工时分摊）、**自动推送通知**、若任务完成则**发送日报**，若次日休息则**调用 DeepSeek 生成并发送周报**。 |
| **Keep 数据同步** | 每 3 小时 | 定期同步 Keep 平台的训练数据，保持数据更新。 |

### 🛠 禅道任务与智能补全

| 模块名称 | 执行频率 | 核心功能点 |
| :--- | :--- | :--- |
| **禅道任务同步** | 每日 15:00、17:00、19:00 | 获取禅道 `token`、同步"我的任务"列表、**幂等性处理**，并进行**工单信息智能补全**。 |
| **工单信息智能补全** | 每 30 分钟 | 检查任务数据，若缺失 "衡量目标 / 计划完成成果 / 实际从事工作与成果" 字段，则**使用 DeepSeek 自动生成**并提交。 |

### 🕒 加班与补贴流程自动化

| 模块名称 | 执行频率 | 核心功能点 |
| :--- | :--- | :--- |
| **自动提交加班申请** | 每 30 分钟 | **非休息日**且有上班记录时，从禅道获取剩余工时最高的任务，**使用 DeepSeek 生成加班理由**并提交。 |
| **自动提交实际加班** | 每日 9:00 | 提交实际加班申请，并对**不满足 1 小时且超期 2 天**的加班申请进行**作废操作**。 |
| **每月餐补提醒** | 每月 24、25、26 日 14:00 | 根据考勤周期计算需填写的餐补信息，**推送提醒**并支持**导出 Excel**。 |

### 💰 系统状态监控与网络管理

| 模块名称 | 执行频率 | 核心功能点 |
| :--- | :--- | :--- |
| **DeepSeek 余额预警** | 每 2 小时 | 查询 DeepSeek 接口，若余额低于 **¥1.00**，立即推送提醒。 |
| **网络测速** | 每日 1:00 | 执行网络速度测试，记录上传/下载速度、延迟、抖动等关键指标。 |
| **网络异常提醒** | 每日 10:00 | 分析网络测速历史数据，检测异常并推送提醒。 |
| **路由器设备同步** | 每日 1:00 | 同步华硕路由器连接设备列表，自动获取和更新设备信息（需配置路由器 IP 和凭据）。 |
| **设备流量统计** | 每日 2:00 | 统计各设备的实时和历史网络流量，包括上传/下载速率、累计流量等（需配置路由器 IP）。 |
| **设备流量详情统计** | 每日 2:00 | 记录设备详细流量数据，支持历史趋势分析和流量监控。 |
| **一诺自动聊天** | 每 10 分钟 | 自动化消息发送功能。 |

-----

## 📊 数据看板功能说明

FreeWim 的数据看板是可视化管理和监控的核心模块，让开发者一眼掌握任务、考勤、报告情况。

| 看板名称 | 路由地址 | 核心内容 |
| :--- | :--- | :--- |
| **任务进度看板** | `/dashboard` | **个人考勤数据看板**，一览每日工时、打卡状态。 |
| **日报/周报看板** | `/daydashboard` | **最近时间内日报/周报状态**，追踪报告发送情况。 |
| **打卡时间设置** | `/daka` | **预设打卡时间/查询打卡任务状态**，实现个性化管理。 |
| **加班统计看板** | `/weekovertiem` | **周加班统计数据**，查看加班申请和审批情况。 |
| **工时统计看板** | `/workhours` | **工时统计分析**，查看任务工时分布。 |
| **一诺看板** | `/yinuo` | **一诺消息交互界面**，管理自动消息。 |
| **网络监控看板** | `/network` | **网络性能监控**，实时查看网速、延迟、丢包等指标。 |
| **流量监控看板** | `/liuliang` | **设备流量统计**，查看各设备的网络使用情况。 |
| **Hangfire 任务管理** | `/hangfire` | **后台任务调度管理**，监控和管理所有定时任务。 |
| **API 文档** | `/scalar` | **OpenAPI 接口文档**，查看和测试所有 API 接口。 |

-----

## 🛠 技术栈概览

FreeWim 基于现代、高效的技术栈构建，确保稳定、可靠的自动化执行。

  * **核心框架：** ASP.NET Core 9.0 (.NET 9.0)
  * **核心语言：** C# 
  * **任务调度：** Hangfire 1.8.17（确保定时任务的可靠执行）
  * **数据存储：** PostgreSQL（使用 Npgsql 9.0.2）
  * **人工智能：** DeepSeek AI / OpenAI 兼容接口（使用 Microsoft.Extensions.AI.OpenAI）
  * **身份认证：** JWT (JSON Web Token) 认证机制
  * **接口集成：** 禅道 API 接口、PMIS 接口、考勤机服务集成、华硕路由器 API、Keep 运动平台 API
  * **消息推送：** Bark, Ntfy, Gotify, Email 等（提供多渠道的实时通知）
  * **实时通信：** WebSocket（支持实时双向通信）
  * **日志系统：** Serilog（支持控制台和文件滚动日志）
  * **Office 处理：** NPOI 2.7.5（Excel 报表生成）
  * **版本控制：** LibGit2Sharp（Git 仓库操作）
  * **API 文档：** Scalar.AspNetCore（现代化 OpenAPI 文档）
  * **加密工具：** AES 加密（数据安全保护）
  * **前端技术：** ECharts、Tailwind CSS、Grid.js、Phosphor Icons

-----

## 🚀 Docker 部署说明（推荐）

使用 Docker 部署 FreeWim 是最快捷、最推荐的方式。

### 1️⃣ 前提条件

  * 已安装 **Docker**（建议 20+ 版本）。
  * 可访问的 **PostgreSQL 数据库** / **PMIS 系统** / **禅道系统** / **DeepSeek API**。
  * 邮件或推送服务可用。

### 2️⃣ 数据库准备

1.  创建 PostgreSQL 数据库，例如命名为 `freewim`。
2.  确认数据库的账号、密码、端口信息可用。

### 3️⃣ 配置文件 (`appsettings.json`)

镜像默认开放 **9940** 端口。请将文件 `/app/appsettings.json` **挂载至宿主机**，并根据实际情况修改配置。

```json
{
  // 数据库连接配置
  "Connection": "数据库链接,实例：User ID=user;Password=123456;Host=127.0.0.1;Port=5432;Database=freewim;Pooling=true;",
  
  // 推送服务配置
  "PushInfo": [
    {
      "PushType": "推送服务类型：bark,gotify,ntfy,email",
      "PushUrl": "推送服务器地址,示例：https://bark.com/bBxhjrLAkAGBxiKAEGVSS"
    }
  ],
  
  // PMIS (流程管理/考勤) 系统配置
  "PMISInfo": {
    "UserAccount": "PMIS账号,示例：100",
    "PassWord": "PMIS密码",
    "UserName": "PMIS中文名,示例：张三",
    "UserMobile": "PMIS手机号,示例：13812341234",
    "DlmeasureUrl": "云地址,示例：https://www.1dlme1asure.com",
    "Url": "云接口地址,示例：https://hdkj.1dlme1asure.com",
    "UserId": "用户唯一ID,示例：7689c1eb435adsfzsdf34",
    "OverStartTime": "加班申请默认开始时间,示例：17:30",
    "OverEndTime": "加班申请默认结束时间,示例：19:30",
    "WorkType": "工作分类,示例：参考PMIS日报新增页面",
    "WorkContent": "所属职责,示例：参考PMIS日报新增页面",
    "AppId": "固定参数",
    "App": "固定参数",
    "ZkUrl": "考勤机服务地址",
    "ZkSN": "考勤机编码",
    "ZkKey": "考勤机key",
    "DailyWorkPrompt": "加班理由生成提示词模板",
    "DailyPrompt": "日报生成提示词模板",
    "WeekPrompt": "周报生成提示词模板"
  },
  
  // 邮件推送配置
  "EmaliInfo": {
    "Host": "邮箱服务地址",
    "Port": 587,
    "UseSsl": true,
    "UserName": "发件人邮箱",
    "PassWord": "发件人密码/授权码",
    "ReceiveList": [
      {
        "Address": "收件人邮箱",
        "Name": "收件人名称"
      }
    ]
  },
  
  // 禅道系统配置
  "ZentaoInfo": {
    "Url": "禅道地址",
    "Account": "禅道账号",
    "Password": "禅道密码"
  },
  
  // 大语言模型 (LLM) 配置 - DeepSeek AI
  "LLM": {
    "EndPoint": "模型API地址",
    "ApiKey": "模型密钥",
    "ModelId": "模型类别"
  },
  
  // 华硕路由器配置（可选）
  "AsusRouter": {
    "RouterIp": "路由器IP地址,示例：192.168.50.1",
    "Username": "路由器管理账号,示例：admin",
    "Password": "路由器管理密码"
  },
  
  // Keep 运动平台配置（可选）
  "Keep": {
    "Token": "Keep 平台访问 Token"
  }
}
```

### 4️⃣ 启动容器

```bash
docker run -d \
  --name freewim \
  -p 9940:8080 \
  -v /path/to/appsettings.json:/app/appsettings.json \
  -v /path/to/logs:/app/Logs \
  --restart unless-stopped \
  your-registry/freewim:latest
```

### 5️⃣ 访问应用

  * **主页（API 文档）：** http://your-server:9940/
  * **Hangfire 管理面板：** http://your-server:9940/hangfire
  * **考勤看板：** http://your-server:9940/dashboard
  * **其他看板：** 参见上方"数据看板功能说明"部分

-----

## 📝 开发说明

### 本地运行

```bash
# 克隆仓库
git clone <repository-url>
cd FreeWim/FreeWim

# 配置数据库和服务（修改 appsettings.json）
# 运行项目
dotnet run
```

### 项目结构

```
FreeWim/
├── Services/            # 核心服务类（重构后的业务逻辑层）
│   ├── AttendanceService.cs        # 考勤处理服务
│   ├── ZentaoService.cs            # 禅道集成服务
│   ├── PmisService.cs              # PMIS 集成服务
│   ├── SpeedTestService.cs         # 网络测速服务
│   ├── AsusRouterService.cs        # 华硕路由器管理服务
│   ├── MessageService.cs           # 消息服务
│   ├── PushMessageService.cs       # 推送消息服务
│   ├── TokenService.cs             # Token 管理服务
│   ├── JwtService.cs               # JWT 认证服务
│   ├── KeepDataSyncService.cs      # Keep 数据同步服务
│   ├── DeepSeekMonitorService.cs   # DeepSeek 余额监控服务
│   ├── WorkFlowExecutorService.cs  # 工作流执行服务
│   └── YhloWebSocketService.cs     # WebSocket 服务
├── Utils/               # 工具类
│   ├── AesHelper.cs                # AES 加密工具
│   ├── ExcelHelper.cs              # Excel 处理工具
│   └── HttpRequestHelper.cs        # HTTP 请求工具
├── Controllers/         # API 控制器
│   ├── AttendanceRecordController.cs
│   ├── PmisAndZentaoController.cs
│   ├── AsusRouterController.cs
│   ├── SpeedTestController.cs
│   ├── GogsController.cs
│   ├── EventInfoController.cs
│   └── MiFanController.cs
├── Models/             # 数据模型
│   ├── Attendance/             # 考勤相关模型
│   ├── PmisAndZentao/          # PMIS 和禅道模型
│   ├── AsusRouter/             # 路由器相关模型
│   ├── Gogs/                   # Git 相关模型
│   ├── Email/                  # 邮件模型
│   ├── EventInfo/              # 事件模型
│   └── SpeedRecord.cs          # 测速记录模型
├── wwwroot/            # 静态资源和看板页面
│   ├── AttendanceDashBoard.html
│   ├── DayReportDashBoard.html
│   ├── NetworkDashboard.html
│   ├── TrafficMonitoring.html
│   ├── WorkHours.html
│   ├── WeekOverTime.html
│   └── Yinuo.html
├── DatabaseInitializer.cs      # 数据库初始化
├── HangFireService.cs          # Hangfire 定时任务配置（重构后）
├── Program.cs                  # 应用入口
└── appsettings.json           # 配置文件
```

-----

## 🔧 常见问题

**Q: 为什么某些任务没有执行？**
A: 请检查 Hangfire 管理面板（/hangfire），查看任务状态和错误日志。

**Q: 如何修改任务执行频率？**
A: 编辑 `HangFireService.cs` 文件中的 Cron 表达式，重新部署即可。

**Q: DeepSeek API 调用失败怎么办？**
A: 检查 `appsettings.json` 中的 LLM 配置，确保 API Key 和 EndPoint 正确。

**Q: 数据库初始化失败？**
A: 确保 PostgreSQL 服务正常运行，Connection 字符串配置正确。

**Q: 如何配置华硕路由器流量监控？**
A: 在 `appsettings.json` 中配置 AsusRouter 节点，填写路由器 IP、管理账号和密码即可。确保路由器启用了 Web 访问功能。

**Q: 服务层重构后有什么变化？**
A: 原来的 Helper 类已全部重构为 Service 类，采用依赖注入方式统一管理。所有 Service 类会在启动时自动注册，无需手动配置。

-----

感谢您加入 **FreeWim** 的行列，让我们一起解放生产力！

如果您有任何建议或反馈，欢迎随时联系开发团队 🙌
