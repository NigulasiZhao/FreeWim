using Dapper;
using LibGit2Sharp;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using FreeWim.Models.Attendance;
using FreeWim.Models.Gogs;
using System.Data;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreeWim.Common;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class GogsController(PushMessageHelper pushMessageHelper, IConfiguration configuration) : Controller
{
    [Tags("代码记录")]
    [EndpointSummary("代码记录组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var commitDates = dbConnection.Query<int>(@"select
                                                          	count(0)
                                                          from
                                                          	(
                                                          	select
                                                          		to_char(commitsdate,
                                                          		'yyyy-mm-dd')
                                                          	from
                                                          		public.gogsrecord
                                                          	group by
                                                          		to_char(commitsdate,
                                                          		'yyyy-mm-dd') )").First();
        var uniqueCommits = dbConnection.Query<int>(@"select
                                                            	count(0)
                                                            from
                                                            	public.gogsrecord").First();
        return Json(new
        {
            commitDates,
            uniqueCommits,
            DayAvg = Math.Round(uniqueCommits / (double)commitDates, 2)
        });
    }

    [Tags("代码记录")]
    [EndpointSummary("日历事件列表查询接口")]
    [HttpGet]
    public ActionResult calendar(string start = "", string end = "")
    {
        IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        string sqlwhere = " where 1=1 ", sqlwhere1 = " where 1=1 ";
        if (!string.IsNullOrEmpty(start))
        {
            sqlwhere += $" and a.commitsdate >= '{DateTime.Parse(start)}'";
            sqlwhere1 += $" and a.clockintime >= '{DateTime.Parse(start)}'";
        }

        if (!string.IsNullOrEmpty(end))
        {
            sqlwhere += $" and a.commitsdate <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
            sqlwhere1 += $" and a.clockintime <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
        }

        var WorkList = _DbConnection.Query<GogsCalendar>(@"select * from (select
                                                                                                	a.id as rownum,
                                                                                                	case
                                                                                                		when message like '%Merge branch%' then '合并 '
                                                                                                		else '变更 '
                                                                                                	end || case
                                                                                                		when repositoryname is null then ''
                                                                                                		else '仓库:' || repositoryname
                                                                                                	end || case
                                                                                                		when branchname is null then ''
                                                                                                		else ';分支:' || SPLIT_PART(branchname,
                                                                                                		'/',
                                                                                                		LENGTH(branchname) - LENGTH(replace(branchname,
                                                                                                		'/',
                                                                                                		'')) + 1)
                                                                                                	end as title,
                                                                                                	to_char(timezone('UTC',
                                                                                                	a.commitsdate at TIME zone 'Asia/Shanghai'),
                                                                                                	'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
                                                                                                	true as hasFile,
                                                                                                	coalesce(message,
                                                                                                	'') as message,
                                                                                                	'sky' as color
                                                                                                from
                                                                                                	public.gogsrecord a " + sqlwhere + @"
                                                                                                union all
                                                                                                select
                                                                                                	cast(a.id  as VARCHAR) as rownum,
                                                                                                	case
                                                                                                		a.clockintype when '0' then '上班打卡'
                                                                                                		else '下班打卡'
                                                                                                	end as title,
                                                                                                	to_char(timezone('UTC',
                                                                                                	a.clockintime at TIME zone 'Asia/Shanghai'),
                                                                                                	'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
                                                                                                	true as hasFile,
                                                                                                	case
                                                                                                		when b.workhours = 0 then '当日工时: ' || RTRIM(RTRIM(cast(ROUND(extract(EPOCH
                                                                                                	from
                                                                                                		(now() at TIME zone 'Asia/Shanghai' - a.clockintime))/ 3600,
                                                                                                		1) as VARCHAR),
                                                                                                		'0'),
                                                                                                		'.')|| ' 小时'
                                                                                                		else '当日工时: ' || RTRIM(RTRIM(cast(b.workhours as VARCHAR),
                                                                                                		'0'),
                                                                                                		'.') || ' 小时'
                                                                                                	end as message,
                                                                                                	'emerald' as color
                                                                                                from
                                                                                                	public.attendancerecorddaydetail a
                                                                                                left join attendancerecordday b on
                                                                                                	to_char(a.attendancedate,
                                                                                                	'yyyy-mm-dd') = to_char(b.attendancedate,
                                                                                                	'yyyy-mm-dd') " + sqlwhere1 +
                                                         @"union all
                                                                                                    	select
                                                                                                    		id,
                                                                                                    		title,
                                                                                                    		to_char(timezone('UTC',
                                                                                                    		a.clockintime at TIME zone 'Asia/Shanghai'),
                                                                                                    		'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
                                                                                                    		true as hasFile,
                                                                                                    		message,
                                                                                                    		color
                                                                                                    	from
                                                                                                    		eventinfo a	" + sqlwhere1 +
                                                         @") order by airDateUtc desc").ToList();
        _DbConnection.Dispose();
        return Json(WorkList);
    }

    [Tags("代码记录")]
    [EndpointSummary("总部WebHook触发接口")]
    [HttpPost]
    public ActionResult GogsPush([FromBody] WebhookPayload input)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var branchName = input.Ref?.Split("/").Last();
        try
        {
            if (input.Commits != null)
                if (input.Commits.Count > 0)
                {
                    var webhookCommitList = input.Commits.Where(e => e.Committer?.Email == configuration["GogsEmail"]).ToList();
                    foreach (var item in webhookCommitList)
                    {
                        var commitExists = dbConnection.Query<int>("select count(0) from public.gogsrecord where id = :id", new { id = item.Id }).First();
                        if (commitExists == 0)
                            dbConnection.Execute(
                                $@"INSERT INTO public.gogsrecord(id,repositoryname,branchname,commitsdate,message) VALUES(:id,:repositoryname,:branchname,to_timestamp('{item.Timestamp.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'),:message);"
                                , new { id = item.Id, repositoryname = input.Repository?.Name, branchname = input.Ref, message = item.Message });
                    }

                    dbConnection.Dispose();
                }
        }
        catch (IOException e)
        {
            dbConnection.Dispose();
            return Json(e.Message);
        }

        return Json("成功");
    }

    [Tags("代码记录")]
    [EndpointSummary("GitLab WebHook触发接口")]
    [HttpPost]
    public ActionResult GitLabPush([FromBody] JsonDocument json)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var root = json.RootElement;
        var branchName = root.GetProperty("ref").GetString()?.Split("/").Last();
        try
        {
            if (root.TryGetProperty("commits", out var commitsElement) && commitsElement.ValueKind == JsonValueKind.Array)
            {
                var commits = commitsElement.EnumerateArray().ToList();
                if (commits.Count > 0)
                {
                    var webhookCommitList = commits.Where(e =>
                        e.TryGetProperty("author", out var committer) &&
                        committer.TryGetProperty("name", out var nameElement) &&
                        nameElement.GetString() == configuration["GogsName"]
                    ).ToList();
                    foreach (var item in webhookCommitList)
                        if (item.TryGetProperty("id", out var idElement))
                        {
                            var commitId = idElement.GetString();
                            var commitExists = dbConnection.Query<int>("select count(0) from public.gogsrecord where id = :id", new { id = commitId }).First();

                            if (commitExists == 0)
                            {
                                // 获取其他必要的属性
                                var timestamp = item.TryGetProperty("timestamp", out var timestampElement) ? timestampElement.GetString() : "";
                                var message = item.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "";
                                var repositoryName = root.TryGetProperty("repository", out var repoElement) &&
                                                     repoElement.TryGetProperty("name", out var nameElement)
                                    ? nameElement.GetString()
                                    : "";

                                if (timestamp != null)
                                    dbConnection.Execute(
                                        $@"INSERT INTO public.gogsrecord(id,repositoryname,branchname,commitsdate,message) VALUES(:id,:repositoryname,:branchname,to_timestamp('{DateTime.Parse(timestamp):yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss'),:message);",
                                        new { id = commitId, repositoryname = repositoryName, branchname = branchName, message = message });
                            }
                        }

                    dbConnection.Dispose();
                }
            }
        }
        catch (IOException e)
        {
            dbConnection.Dispose();
            return Json(e.Message);
        }

        return Json("成功");
    }

    [Tags("代码记录")]
    [EndpointSummary("GitHubWebHook触发接口")]
    [HttpPost]
    public ActionResult GitHubPush([FromBody] GitHubWebhookPayload input)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var branchName = input.@ref?.Split("/").Last();
        var dataSql = "";
        try
        {
            if (input.commits is { Count: > 0 })
            {
                var webhookCommitList = input.commits.Where(e => e.committer?.Email == configuration["GogsEmail"]).ToList();
                foreach (var item in webhookCommitList)
                {
                    var commitExists = dbConnection.Query<int>("select count(0) from public.gogsrecord where id = :id", new { id = item.id }).First();
                    if (commitExists == 0)
                        dbConnection.Execute(
                            @$"INSERT INTO public.gogsrecord(id,repositoryname,branchname,commitsdate,message) VALUES(:id,:repositoryname,:branchname,to_timestamp('{item.timestamp.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'),:message);"
                            , new { id = item.id, repositoryname = input.repository?.name, branchname = input.@ref, message = item.message });
                }

                if (!string.IsNullOrEmpty(dataSql)) dbConnection.Execute(dataSql);
                dbConnection.Dispose();
            }
        }
        catch (IOException e)
        {
            dbConnection.Dispose();
            return Json(e.Message);
        }

        return Json("成功");
    }

    [Tags("代码记录")]
    [EndpointSummary("GitLabWebHook触发Jenkins")]
    [HttpPost]
    public ActionResult GitPushTriggerJenkins([FromBody] JsonDocument json)
    {
        var root = json.RootElement;
        var branchName = root.GetProperty("ref").GetString()?.Split("/").Last();
        try
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{configuration.GetSection("JenkinsUserName").Value}:{configuration.GetSection("JenkinsUserToken").Value}")));

            var gitList = configuration.GetSection("GitList").Get<List<GitInfoModel>>()!;
            gitList = gitList.Where(e => e.BranchName == branchName).ToList();
            foreach (var model in gitList)
                if (!string.IsNullOrEmpty(model.JenkinsBuildUrl))
                {
                    if (model.BuildParameters?.Count > 0)
                    {
                        for (var i = 0; i < model.BuildParameters.Count; i++)
                        {
                            var content = new FormUrlEncodedContent(model.BuildParameters[i]);
                            var result = client.PostAsync(model.JenkinsBuildUrl, content).GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        var buffer = Encoding.UTF8.GetBytes("");
                        var byteContent = new ByteArrayContent(buffer);
                        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var result = client.PostAsync(model.JenkinsBuildUrl, byteContent).GetAwaiter().GetResult();
                    }
                }

            #region 提醒推送

            pushMessageHelper.Push("测试站点更新", "分支：" + branchName + "测试站点已更新", PushMessageHelper.PushIcon.Jenkins);

            #endregion
        }
        catch (IOException e)
        {
            return Json(e.Message);
        }

        return Json(branchName + "更新完成");
    }
}