using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using FreeWim.Models.PmisAndZentao;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FreeWim.Common;

public class ZentaoHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger, PushMessageHelper pushMessageHelper, IChatClient chatClient)
{
    /// <summary>
    /// 获取禅道token
    /// C:\Windows\System32\Microsoft\Microsoft Storage Spaces TMP\MicrosoftSoftwareShadowCopy
    /// </summary>
    /// <returns></returns>
    public string GetZentaoToken()
    {
        var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>()!;
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(zentaoInfo.Url + "/api.php/v1/tokens", new
        {
            account = zentaoInfo.Account,
            password = zentaoInfo.Password
        }).Result;
        var json = postResponse.Content.ReadAsStringAsync().Result;
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString();
        return token ?? string.Empty;
    }

    /// <summary>
    /// 获取禅道任务
    /// </summary>
    /// <returns></returns>
    public JObject GetZentaoTask()
    {
        var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>()!;
        var zentaoToken = GetZentaoToken();
        var httpHelper = new HttpRequestHelper();
        var getTaskResponse = httpHelper.GetAsync(zentaoInfo.Url + "/my-work-task.json", new Dictionary<string, string> { { "Token", zentaoToken } }).Result;
        var outer = JsonSerializer.Deserialize<ZentaoResponse>(getTaskResponse.Content.ReadAsStringAsync().Result);
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), // 确保不重新编码
            WriteIndented = true
        };
        if (outer == null) return new JObject();
        var jsonDoc = JsonDocument.Parse(outer.data ?? string.Empty); // 内层是个 JSON 字符串
        var prettyJson = JsonSerializer.Serialize(jsonDoc.RootElement, options);
        var json = JObject.Parse(prettyJson);
        return json;

        //var zentaoTaskResult = JsonSerializer.Deserialize<ZentaoTaskResponse>(prettyJson);
        //return zentaoTaskResult.tasks;
    }

    /// <summary>
    /// 同步禅道任务
    /// </summary>
    /// <returns></returns>
    public bool SynchronizationZentaoTask()
    {
        try
        {
            var taskObject = GetZentaoTask();
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            dbConnection.Open();

            using var transaction = dbConnection.BeginTransaction();
            var updateRows = 0;

            if (!taskObject.TryGetValue("tasks", out var tasksToken) || tasksToken?.Type != JTokenType.Array)
            {
                logger.LogWarning("Invalid or missing 'tasks' field");
                return false;
            }

            var dataArray = tasksToken as JArray;
            if (dataArray == null || !dataArray.Any())
            {
                logger.LogWarning("'tasks' array is empty or invalid");
                return false;
            }

            foreach (var zentaoTaskItem in dataArray)
            {
                var projectCode = GetProjectCodeForProjectId(zentaoTaskItem["project"]?.ToString() ?? string.Empty);
                var sql = @"
                    INSERT INTO public.zentaotask (
                        id, project, execution, taskname, estimate, timeleft, consumed, registerhours,
                        taskstatus, eststarted, deadline, taskdesc, openedby, openeddate, qiwangriqi,
                        executionname, projectname, projectcode
                    ) VALUES (
                        @id, @project, @execution, @taskname, @estimate, @timeleft, @consumed, @registerhours,
                        @taskstatus, @eststarted, @deadline, @taskdesc, @openedby, @openeddate, @qiwangriqi,
                        @executionname, @projectname, @projectcode
                    ) ON CONFLICT (id) DO NOTHING;";

                var parameters = new
                {
                    id = zentaoTaskItem["id"]?.ToObject<int>() ?? 0,
                    project = zentaoTaskItem["project"]?.ToObject<int?>(),
                    execution = zentaoTaskItem["execution"]?.ToObject<int?>(),
                    taskname = zentaoTaskItem["name"]?.ToString(),
                    // float8字段空值转0
                    estimate = zentaoTaskItem["estimate"]?.ToObject<double?>() ?? 0,
                    timeleft = zentaoTaskItem["left"]?.ToObject<double?>() ?? 0,
                    consumed = zentaoTaskItem["consumed"]?.ToObject<double?>() ?? 0,
                    registerhours = 0.0, // 固定值
                    taskstatus = zentaoTaskItem["status"]?.ToString(),
                    eststarted = zentaoTaskItem["estStarted"]?.ToObject<DateTime?>(),
                    deadline = zentaoTaskItem["deadline"]?.ToObject<DateTime?>(),
                    taskdesc = zentaoTaskItem["desc"]?.ToString(),
                    openedby = zentaoTaskItem["openedBy"]?.ToString(),
                    openeddate = zentaoTaskItem["openedDate"]?.ToObject<DateTime?>(),
                    qiwangriqi = zentaoTaskItem["qiwangriqi"]?.ToObject<DateTime?>(),
                    executionname = zentaoTaskItem["executionName"]?.ToString(),
                    projectname = zentaoTaskItem["projectName"]?.ToString(),
                    projectcode = projectCode
                };

                updateRows += dbConnection.Execute(sql, parameters, transaction);
            }

            transaction.Commit();
            if (updateRows > 0)
            {
                pushMessageHelper.Push("禅道", $"任务数据同步成功\n本次同步 {updateRows} 条任务", PushMessageHelper.PushIcon.Zentao);
                TaskDescriptionComplete();
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError("同步禅道任务异常:" + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 完成禅道任务
    /// </summary>
    public void FinishZentaoTask(DateTime finishedDate, double totalHours)
    {
        var pushMessage = string.Empty;
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>()!;
            var httpHelper = new HttpRequestHelper();
            var tasklist = AllocateWork(finishedDate, totalHours);
            if (tasklist.Count > 0)
            {
                var zentaoToken = GetZentaoToken();
                foreach (var task in tasklist)
                {
                    var getResponse = httpHelper.PostAsync(zentaoInfo.Url + $"/api.php/v1/tasks/{task.Id}/finish", new
                    {
                        currentConsumed = task.TimeConsuming,
                        assignedTo = "",
                        realStarted = task.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        finishedDate = task.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        comment = "任务完成"
                    }, new Dictionary<string, string> { { "Token", zentaoToken } }).Result;
                    var outer = JsonSerializer.Deserialize<FinishZentaoTaskResponse>(getResponse.Content.ReadAsStringAsync().Result);
                    if (outer != null)
                        dbConnection.Execute(
                            $@"UPDATE public.zentaotask SET consumed =consumed+ {outer.Consumed},timeleft = {outer.Left},registerhours = registerhours + {task.TimeConsuming},taskstatus = '{outer.Status}' WHERE ID = {outer.id}");
                }

                pushMessage = "已处理 " + tasklist.Count + " 条任务\n共登记 " + tasklist.Sum(e => e.TimeConsuming) + " 工时";
                pushMessageHelper.Push("禅道", pushMessage, PushMessageHelper.PushIcon.Zentao);
            }
        }
        catch (Exception e)
        {
            pushMessage = "禅道完成任务异常:" + e.Message;
            pushMessageHelper.Push("禅道", pushMessage, PushMessageHelper.PushIcon.Alert);
            logger.LogError("禅道完成任务异常:" + e.Message);
        }
    }

    /// <summary>
    /// 获取项目编码
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public string GetProjectCodeForProjectId(string projectId)
    {
        try
        {
            var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>()!;
            var zentaoToken = GetZentaoToken();
            var httpHelper = new HttpRequestHelper();
            var getResponse = httpHelper.GetAsync(zentaoInfo.Url + "/api.php/v1/projects/" + projectId, new Dictionary<string, string> { { "Token", zentaoToken } }).Result;
            var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
            return json["code"]?.ToString() ?? string.Empty;
        }
        catch (Exception e)
        {
            logger.LogError("获取项目编码异常:" + e.Message);
            return "";
        }
    }

    /// <summary>
    /// 计算任务耗时
    /// </summary>
    /// <param name="startDate">任务日期</param>
    /// <param name="totalHours">当日工时</param>
    /// <returns></returns>
    public List<TaskItem> AllocateWork(DateTime startDate, double totalHours)
    {
        var result = new List<TaskItem>();
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var registerhours = dbConnection.Query<float?>($@"select sum(registerhours) from public.zentaotask where to_char(eststarted,'yyyy-MM-dd') = '{startDate:yyyy-MM-dd}'").FirstOrDefault();
        if (registerhours == null) return result;
        totalHours -= registerhours.Value;
        var tasks = dbConnection
            .Query<TaskItem>($@"select id,timeleft from public.zentaotask where (taskstatus ='wait' or taskstatus = 'doing') and to_char(eststarted,'yyyy-MM-dd') = '{startDate:yyyy-MM-dd}'").ToList();
        var current = new DateTime(startDate.Year, startDate.Month, startDate.Day, 8, 30, 0);

        foreach (var task in tasks)
        {
            var taskCopy = new TaskItem
            {
                Id = task.Id,
                Timeleft = task.Timeleft,
                StartTime = current
            };

            double timeAllocated = 0;

            while (timeAllocated < task.Timeleft && totalHours >= 0.5)
            {
                // 跳过中午12:00~13:00
                if (current.Hour == 12 && current.Minute == 0)
                {
                    current = current.AddHours(1);
                    continue;
                }

                timeAllocated += 0.5;
                totalHours -= 0.5;
                current = current.AddMinutes(30);

                if (totalHours < 0.01) break;
            }

            taskCopy.TimeConsuming = timeAllocated;
            taskCopy.EndTime = current;
            result.Add(taskCopy);

            if (totalHours < 0.01) break;
        }

        return result;
    }

    /// <summary>
    /// 禅道衡量目标、计划完成成果、实际从事工作与成果信息补全
    /// </summary>
    public void TaskDescriptionComplete()
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var taskList = dbConnection
                .Query<(int id, string taskname, string taskdesc)>(
                    "select id,taskname,taskdesc from public.zentaotask where to_char(eststarted,'yyyy-MM-dd') = to_char(now(),'yyyy-MM-dd') and (target is null or planfinishact is  null or realjob is  null)")
                .ToList();
            var chatOptions = new ChatOptions { Tools = [] };
            foreach (var task in taskList)
            {
                var chatHistory = new List<ChatMessage>
                {
                    new(ChatRole.System, pmisInfo.DailyWorkPrompt),
                    new(ChatRole.User, "任务内容：" + task.taskname + ":" + task.taskdesc)
                };
                var res = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
                if (string.IsNullOrWhiteSpace(res?.Text)) return;
                var taskContent = JsonConvert.DeserializeObject<dynamic>(res.Text.Replace("```json", "").Replace("```", "").Trim());
                dbConnection.Execute(
                    $"UPDATE public.zentaotask SET target= '{taskContent?.target}',planfinishact= '{taskContent?.planFinishAct}',realjob= '{taskContent?.realJob}' where id = {task.id}");

                string target = taskContent?.target.ToString() ?? "";
                string planFinishAct = taskContent?.planFinishAct.ToString() ?? "";
                string realJob = taskContent?.realJob.ToString() ?? "";
                var shortTarget = target.Length > 10 ? target.Substring(0, 10) + "..." : target;
                var shortPlan = planFinishAct.Length > 10 ? planFinishAct.Substring(0, 10) + "..." : planFinishAct;
                var shortReal = realJob.Length > 10 ? realJob.Substring(0, 10) + "..." : realJob;
                pushMessageHelper.Push("禅道", $"任务信息已完善\n衡量目标:{shortTarget}\n计划完成成果:{shortPlan}\n实际从事工作与成果:{shortReal}", PushMessageHelper.PushIcon.Zentao);
            }
        }
        catch (Exception e)
        {
            pushMessageHelper.Push("禅道任务异常", e.Message, PushMessageHelper.PushIcon.Alert);
        }
    }
}