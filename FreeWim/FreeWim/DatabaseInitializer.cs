using System.Data;
using Dapper;
using FreeWim.Services;
using FreeWim.Models.Attendance;
using FreeWim.Models.PmisAndZentao;
using LibGit2Sharp;
using Newtonsoft.Json;
using Npgsql;

namespace FreeWim;

public class DatabaseInitializer
{
    private readonly IConfiguration _Configuration;
    private readonly TokenService _tokenService;

    public DatabaseInitializer(IConfiguration configuration, TokenService tokenService)
    {
        _Configuration = configuration;
        _tokenService = tokenService;
    }

    public void Initialize()
    {
        var pmisInfo = _Configuration.GetSection("PMISInfo").Get<PMISInfo>();
        IDbConnection _dbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        
        // 一次性查询所有表，避免重复查询
        var existingTables = GetExistingTables(_dbConnection);
        
        if (!existingTables.Contains("speedrecord"))
        {
            var createTableSql = @"
                                    CREATE TABLE public.speedrecord (
                                    	id varchar(36) NOT NULL,
                                    	ping varchar(200) NULL,
                                    	download numeric(11, 2) NULL,
                                    	upload numeric(11, 2) NULL,
                                    	server_id float8 NULL,
                                    	server_host varchar(500) NULL,
                                    	server_name varchar(500) NULL,
                                    	url varchar(500) NULL,
                                    	scheduled float8 DEFAULT 0 NULL,
                                    	failed float8 DEFAULT 0 NULL,
                                    	created_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	updated_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	CONSTRAINT speedrecord_pk PRIMARY KEY (id)
                                    );";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("attendancerecord"))
        {
            var createTableSql = @"
                                    CREATE TABLE public.attendancerecord (
                                                	attendancemonth varchar(100) NULL,
                                                	workdays float8 NULL,
                                                	latedays float8 NULL,
                                                	earlydays float8 NULL
                                                );
                                    COMMENT ON COLUMN public.attendancerecord.attendancemonth IS '考勤年月';
                                    COMMENT ON COLUMN public.attendancerecord.workdays IS '工作天数';
                                    COMMENT ON COLUMN public.attendancerecord.latedays IS '迟到天数';
                                    COMMENT ON COLUMN public.attendancerecord.earlydays IS '早退天数';";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("attendancerecordday"))
        {
            var createTableSql = @"
                                    CREATE TABLE public.attendancerecordday (
                                                	untilthisday boolean NULL,
                                                	""day"" float8 NULL,
                                                	checkinrule varchar(100) NULL,
                                                	isnormal varchar(100) NULL,
                                                	isabnormal varchar(100) NULL,
                                                	isapply varchar(100) NULL,
                                                	clockinnumber float8 NULL,
                                                	workhours numeric(11, 2) NULL,
                                                	attendancedate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("attendancerecorddaydetail"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.attendancerecorddaydetail (
                                                	id float8 NULL,
                                                	recordid float8 NULL,
                                                	clockintype varchar(100) NULL,
                                                	clockintime timestamp NULL,
                                                	attendancedate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("eventinfo"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.eventinfo (
													id varchar(100) NULL,
													title varchar(200) NULL,
													message varchar(2000) NULL,
													clockintime timestamp NULL,
													color varchar(200) NULL,
													""source"" varchar(200) NULL,
													distinguishingmark varchar(200) NULL
												);
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("zentaotask"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.zentaotask (
																	id integer NOT NULL,
																	project integer NULL,
																	execution integer NULL,
																	taskname varchar(1000) NULL,
																	estimate float8 NULL,
																	timeleft float8 NULL,
																	consumed float8 NULL,
																	registerhours float8 NULL,
																	taskstatus varchar(200) NULL,
																	eststarted timestamp NULL,
																	deadline timestamp NULL,
																	taskdesc varchar(1000) NULL,
																	openedby varchar(200) NULL,
																	openeddate timestamp NULL,
																	qiwangriqi timestamp NULL,
																	executionname varchar(500) NULL,
																	projectname varchar(500) NULL,
																	CONSTRAINT zentaotask_pk PRIMARY KEY (id)
																);
																
																-- Column comments
																
																COMMENT ON COLUMN public.zentaotask.id IS '任务id';
																COMMENT ON COLUMN public.zentaotask.project IS '项目id';
																COMMENT ON COLUMN public.zentaotask.execution IS '执行人id';
																COMMENT ON COLUMN public.zentaotask.taskname IS '任务名称';
																COMMENT ON COLUMN public.zentaotask.estimate IS '预估工时';
																COMMENT ON COLUMN public.zentaotask.timeleft IS '剩余工时';
																COMMENT ON COLUMN public.zentaotask.consumed IS '消耗工时';
																COMMENT ON COLUMN public.zentaotask.registerhours IS '本人登记工时';
																COMMENT ON COLUMN public.zentaotask.taskstatus IS '任务状态';
																COMMENT ON COLUMN public.zentaotask.eststarted IS '开始日期';
																COMMENT ON COLUMN public.zentaotask.deadline IS '截止日期';
																COMMENT ON COLUMN public.zentaotask.taskdesc IS '任务说明';
																COMMENT ON COLUMN public.zentaotask.openedby IS '派单人';
																COMMENT ON COLUMN public.zentaotask.openeddate IS '派单日期';
																COMMENT ON COLUMN public.zentaotask.qiwangriqi IS '期望日期';
																COMMENT ON COLUMN public.zentaotask.executionname IS '项目名称';
																COMMENT ON COLUMN public.zentaotask.projectname IS '项目名称带编号';";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("overtimerecord"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.overtimerecord (
																		id varchar(50) NOT NULL,
																		plan_start_time timestamp NULL,
																		plan_end_time timestamp NULL,
																		plan_work_overtime_hour float8 NULL,
																		contract_id varchar(200) NULL,
																		contract_unit varchar(200) NULL,
																		project_name varchar(200) NULL,
																		work_date varchar(50) NULL,
																		subject_matter varchar(500) NULL,
																		real_start_time timestamp NULL,
																		real_end_time timestamp NULL,
																		real_work_overtime_hour float8 NULL,
																		orderid varchar(100) NULL,
																		CONSTRAINT overtimerecord_unique UNIQUE (id)
																	);
																	
																	-- Column comments
																	
																	COMMENT ON COLUMN public.overtimerecord.plan_start_time IS '计划加班开始时间';
																	COMMENT ON COLUMN public.overtimerecord.plan_end_time IS '计划加班结束时间';
																	COMMENT ON COLUMN public.overtimerecord.plan_work_overtime_hour IS '计划加班时长';
																	COMMENT ON COLUMN public.overtimerecord.contract_id IS '项目id';
																	COMMENT ON COLUMN public.overtimerecord.contract_unit IS '项目单位';
																	COMMENT ON COLUMN public.overtimerecord.project_name IS '项目名称';
																	COMMENT ON COLUMN public.overtimerecord.work_date IS '加班日期';
																	COMMENT ON COLUMN public.overtimerecord.subject_matter IS '加班事由';
																	COMMENT ON COLUMN public.overtimerecord.real_start_time IS '实际加班开始时间';
																	COMMENT ON COLUMN public.overtimerecord.real_end_time IS '实际加班结束时间';
																	COMMENT ON COLUMN public.overtimerecord.real_work_overtime_hour IS '实际加班时长';
																	COMMENT ON COLUMN public.overtimerecord.orderid IS '工单ID';
																	";
            _dbConnection.Execute(createTableSql);
        }

        if (_dbConnection.Query<int>("SELECT COUNT(0) FROM attendancerecord").First() == 0)
        {
            var infoResult = new AttendanceRecordResult();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", _tokenService.GetTokenAsync());
            var StartDate = DateTime.Parse("2023-07-01");
            while (StartDate < DateTime.Now)
            {
                var response = client.GetAsync(pmisInfo!.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + StartDate.ToString("yyyy-MM")).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var ResultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
                if (ResultModel is { Code: 200 })
                    if (ResultModel.Data != null)
                    {
                        _dbConnection.Execute(
                            $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{StartDate.ToString("yyyy-MM")}',{ResultModel.Data.WorkDays},{ResultModel.Data.LateDays},{ResultModel.Data.EarlyDays});");
                        if (ResultModel.Data.DayVoList != null)
                            foreach (var item in ResultModel.Data.DayVoList)
                            {
                                var flagedate = DateTime.Parse(StartDate.ToString("yyyy-MM") + "-" + item.Day);
                                if (item.WorkHours != null)
                                {
                                    _dbConnection.Execute($@"INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate)
                                                        VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{item.WorkHours},to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                                    if (item.DetailList != null)
                                        foreach (var daydetail in item.DetailList)
                                            _dbConnection.Execute($@"INSERT INTO public.attendancerecorddaydetail(id,recordid,clockintype,clockintime,attendancedate)
                                                        VALUES({daydetail.Id},{daydetail.RecordId},'{daydetail.ClockInType}',to_timestamp('{daydetail.ClockInTime}', 'yyyy-mm-dd hh24:mi:ss'),to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                                }
                            }
                    }

                //infoResult.WorkDays += ResultModel.Data.WorkDays;
                //infoResult.LateDays += ResultModel.Data.LateDays;
                //infoResult.EarlyDays += ResultModel.Data.EarlyDays;
                //infoResult.DayAvg += (double)ResultModel.Data.DayVoList.Where(e => e.WorkHours != null).Sum(e => e.WorkHours);
                StartDate = StartDate.AddMonths(1);
            }
        }

        if (!existingTables.Contains("gogsrecord"))
        {
            var dataSql = "";
            var createTableSql = @"
                                   CREATE TABLE public.gogsrecord (
                                                	id varchar(100) NULL,
                                                	repositoryname varchar(200) NULL,
                                                	branchname varchar(200) NULL,
                                                	commitsdate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
            if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "ProjectGit"))
            {
                var directories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory + "ProjectGit");
                if (directories.Length > 0)
                {
                    var targetEmail = _Configuration["GogsEmail"];
                    var allCommits = new Dictionary<string, List<Commit>>();
                    foreach (var repoPath in directories)
                    {
                        var folderName = Path.GetFileName(repoPath);
                        if (Directory.Exists(repoPath + "/.git"))
                            using (var repo = new Repository(repoPath))
                            {
                                foreach (var branch in repo.Branches) allCommits.Add(repoPath + branch.FriendlyName, branch.Commits.Where(commit => commit.Author.Email == targetEmail).ToList());
                            }
                    }

                    var uniqueCommits = new HashSet<Commit>(allCommits.SelectMany(kvp => kvp.Value));
                    foreach (var commit in uniqueCommits)
                        dataSql +=
                            @$"INSERT INTO public.gogsrecord(id,commitsdate) VALUES('{commit.Id}',to_timestamp('{commit.Committer.When.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'));";
                    if (!string.IsNullOrEmpty(dataSql)) _dbConnection.Execute(dataSql);
                }
            }
        }

        if (!existingTables.Contains("checkinwarning"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.checkinwarning (
														id varchar(100) NULL,
														name varchar(200) NULL,
														clockintime timestamp NULL
													);
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (!existingTables.Contains("autocheckinrecord"))
        {
            var createTableSql = @"
                                   CREATE TABLE public.autocheckinrecord (
														id varchar(100) NULL,
														jobid varchar(100) NULL,
														clockintime timestamp NULL,
														clockinstate float8 NULL,
														createdat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
														updateat timestamp NULL,
													    CONSTRAINT autocheckinrecord_pk PRIMARY KEY (id)
													);
													
													COMMENT ON COLUMN public.autocheckinrecord.id IS '主键ID';
													COMMENT ON COLUMN public.autocheckinrecord.jobid IS 'hangfire唯一ID';
													COMMENT ON COLUMN public.autocheckinrecord.clockintime IS '签到签退时间';
													COMMENT ON COLUMN public.autocheckinrecord.clockinstate IS '执行状态 0执行中 1成功 2失败';
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        _dbConnection.Execute(@"do $$
									BEGIN
									IF (select count(*) from  information_schema.columns where table_name = 'attendancerecordday' and table_schema = 'public' and column_name = 'yearmonth' ) = 0
									THEN
									   ALTER TABLE attendancerecordday ADD yearmonth varchar(200) NULL;
									   COMMENT ON COLUMN attendancerecordday.yearmonth IS '年月';
									END IF;
									END;
$$;
--GO
");
        _dbConnection.Execute(@"do $$
									BEGIN
									IF (select count(*) from  information_schema.columns where table_name = 'zentaotask' and table_schema = 'public' and column_name = 'projectcode' ) = 0
									THEN
									   ALTER TABLE zentaotask ADD projectcode varchar(200) NULL;
									   COMMENT ON COLUMN zentaotask.projectcode IS '项目编码';
									END IF;
									END;
$$;
--GO
");
        _dbConnection.Execute(@"do $$
									BEGIN
									IF (select count(*) from  information_schema.columns where table_name = 'zentaotask' and table_schema = 'public' and column_name = 'target' ) = 0
									THEN
									   ALTER TABLE public.zentaotask ADD target varchar(1000) NULL;
									   COMMENT ON COLUMN public.zentaotask.target IS '衡量目标';
									END IF;
									IF (select count(*) from  information_schema.columns where table_name = 'zentaotask' and table_schema = 'public' and column_name = 'planfinishact' ) = 0
									THEN
									   ALTER TABLE public.zentaotask ADD planfinishact varchar(1000) NULL;
									   COMMENT ON COLUMN public.zentaotask.planfinishact IS '计划完成成果';
									END IF;
									IF (select count(*) from  information_schema.columns where table_name = 'zentaotask' and table_schema = 'public' and column_name = 'realjob' ) = 0
									THEN
									   ALTER TABLE public.zentaotask ADD realjob varchar(1000) NULL;
									   COMMENT ON COLUMN public.zentaotask.realjob IS '实际从事工作与成果';
									END IF;
									END;
$$;
--GO
");

        // 初始化华硕路由器设备表
        if (!existingTables.Contains("asusrouterdevice"))
        {
            var createTableSql = @"
                CREATE TABLE public.asusrouterdevice (
                    id varchar(36) NOT NULL,
                    mac varchar(50) NOT NULL,
                    ip varchar(50) NULL,
                    name varchar(200) NULL,
                    nickname varchar(200) NULL,
                    vendor varchar(500) NULL,
                    vendorclass varchar(500) NULL,
                    type varchar(50) NULL,
                    defaulttype varchar(50) NULL,
                    iswl varchar(10) NULL,
                    isgateway varchar(10) NULL,
                    iswebserver varchar(10) NULL,
                    isprinter varchar(10) NULL,
                    isitunes varchar(10) NULL,
                    isonline varchar(10) NULL,
                    islogin varchar(10) NULL,
                    ssid varchar(100) NULL,
                    rssi varchar(50) NULL,
                    curtx varchar(50) NULL,
                    currx varchar(50) NULL,
                    totaltx varchar(50) NULL,
                    totalrx varchar(50) NULL,
                    wlconnecttime varchar(50) NULL,
                    ipmethod varchar(50) NULL,
                    opmode varchar(10) NULL,
                    rog varchar(10) NULL,
                    groupname varchar(100) NULL,
                    qoslevel varchar(50) NULL,
                    internetmode varchar(50) NULL,
                    internetstate varchar(10) NULL,
                    dpitype varchar(50) NULL,
                    dpidevice varchar(50) NULL,
                    isgn varchar(10) NULL,
                    macrepeat varchar(10) NULL,
                    callback varchar(500) NULL,
                    keeparp varchar(10) NULL,
                    wtfast varchar(10) NULL,
                    ostype int NULL,
                    ameshisre varchar(10) NULL,
                    ameshbindmac varchar(50) NULL,
                    ameshbindband varchar(10) NULL,
                    datasource varchar(50) NULL,
                    createdat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    updatedat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    CONSTRAINT asusrouterdevice_pk PRIMARY KEY (id),
                    CONSTRAINT asusrouterdevice_mac_unique UNIQUE (mac)
                );

                COMMENT ON TABLE public.asusrouterdevice IS '华硕路由器设备信息表';
                COMMENT ON COLUMN public.asusrouterdevice.id IS '主键ID';
                COMMENT ON COLUMN public.asusrouterdevice.mac IS 'MAC地址（设备唯一标识）';
                COMMENT ON COLUMN public.asusrouterdevice.ip IS 'IP地址';
                COMMENT ON COLUMN public.asusrouterdevice.name IS '设备名称';
                COMMENT ON COLUMN public.asusrouterdevice.nickname IS '设备昵称';
                COMMENT ON COLUMN public.asusrouterdevice.vendor IS '设备厂商';
                COMMENT ON COLUMN public.asusrouterdevice.vendorclass IS '设备厂商类别';
                COMMENT ON COLUMN public.asusrouterdevice.type IS '设备类型';
                COMMENT ON COLUMN public.asusrouterdevice.defaulttype IS '默认设备类型';
                COMMENT ON COLUMN public.asusrouterdevice.iswl IS '是否无线连接（0:有线, 1:2.4G WiFi, 2:5G WiFi）';
                COMMENT ON COLUMN public.asusrouterdevice.isgateway IS '是否为网关';
                COMMENT ON COLUMN public.asusrouterdevice.iswebserver IS '是否为Web服务器';
                COMMENT ON COLUMN public.asusrouterdevice.isprinter IS '是否为打印机';
                COMMENT ON COLUMN public.asusrouterdevice.isitunes IS '是否为iTunes设备';
                COMMENT ON COLUMN public.asusrouterdevice.isonline IS '是否在线（1:在线, 0:离线）';
                COMMENT ON COLUMN public.asusrouterdevice.islogin IS '是否登录';
                COMMENT ON COLUMN public.asusrouterdevice.ssid IS 'SSID（WiFi名称）';
                COMMENT ON COLUMN public.asusrouterdevice.rssi IS '信号强度（dBm）';
                COMMENT ON COLUMN public.asusrouterdevice.curtx IS '当前上传速度（Mbps）';
                COMMENT ON COLUMN public.asusrouterdevice.currx IS '当前下载速度（Mbps）';
                COMMENT ON COLUMN public.asusrouterdevice.totaltx IS '总上传流量';
                COMMENT ON COLUMN public.asusrouterdevice.totalrx IS '总下载流量';
                COMMENT ON COLUMN public.asusrouterdevice.wlconnecttime IS '无线连接时长';
                COMMENT ON COLUMN public.asusrouterdevice.ipmethod IS 'IP获取方式（DHCP, Manual）';
                COMMENT ON COLUMN public.asusrouterdevice.opmode IS '操作模式';
                COMMENT ON COLUMN public.asusrouterdevice.rog IS '是否为ROG设备';
                COMMENT ON COLUMN public.asusrouterdevice.groupname IS '设备分组';
                COMMENT ON COLUMN public.asusrouterdevice.qoslevel IS 'QoS等级';
                COMMENT ON COLUMN public.asusrouterdevice.internetmode IS '互联网访问模式（allow, block）';
                COMMENT ON COLUMN public.asusrouterdevice.internetstate IS '互联网状态';
                COMMENT ON COLUMN public.asusrouterdevice.dpitype IS 'DPI类型';
                COMMENT ON COLUMN public.asusrouterdevice.dpidevice IS 'DPI设备';
                COMMENT ON COLUMN public.asusrouterdevice.isgn IS '是否为GN设备';
                COMMENT ON COLUMN public.asusrouterdevice.macrepeat IS 'MAC地址是否重复';
                COMMENT ON COLUMN public.asusrouterdevice.callback IS '回调地址';
                COMMENT ON COLUMN public.asusrouterdevice.keeparp IS '是否保持ARP';
                COMMENT ON COLUMN public.asusrouterdevice.wtfast IS 'WTFast状态';
                COMMENT ON COLUMN public.asusrouterdevice.ostype IS '操作系统类型';
                COMMENT ON COLUMN public.asusrouterdevice.ameshisre IS '是否为AiMesh中继器';
                COMMENT ON COLUMN public.asusrouterdevice.ameshbindmac IS 'AiMesh绑定MAC地址';
                COMMENT ON COLUMN public.asusrouterdevice.ameshbindband IS 'AiMesh绑定频段';
                COMMENT ON COLUMN public.asusrouterdevice.datasource IS '数据来源（networkmapd, nmpClient）';
                COMMENT ON COLUMN public.asusrouterdevice.createdat IS '创建时间';
                COMMENT ON COLUMN public.asusrouterdevice.updatedat IS '更新时间';
                ";
            _dbConnection.Execute(createTableSql);
        }

        // 初始化华硕路由器设备流量统计表
        if (!existingTables.Contains("asusrouterdevicetraffic"))
        {
            var createTableSql = @"
                CREATE TABLE public.asusrouterdevicetraffic (
                    id varchar(50) NOT NULL,
                    mac varchar(50) NOT NULL,
                    statdate timestamp NOT NULL,
                    hour int NOT NULL,
                    uploadbytes bigint NOT NULL DEFAULT 0,
                    downloadbytes bigint NOT NULL DEFAULT 0,
                    createdat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    updatedat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    CONSTRAINT asusrouterdevicetraffic_pk PRIMARY KEY (id)
                );

                CREATE INDEX idx_asusrouterdevicetraffic_mac ON public.asusrouterdevicetraffic(mac);
                CREATE INDEX idx_asusrouterdevicetraffic_statdate ON public.asusrouterdevicetraffic(statdate);
                CREATE INDEX idx_asusrouterdevicetraffic_mac_statdate ON public.asusrouterdevicetraffic(mac, statdate);

                COMMENT ON TABLE public.asusrouterdevicetraffic IS '华硕路由器设备流量统计表';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.id IS '主键ID';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.mac IS '设备MAC地址';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.statdate IS '统计日期';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.hour IS '小时（0-23）';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.uploadbytes IS '上传字节数';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.downloadbytes IS '下载字节数';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.createdat IS '创建时间';
                COMMENT ON COLUMN public.asusrouterdevicetraffic.updatedat IS '更新时间';
                ";
            _dbConnection.Execute(createTableSql);
        }

        // 初始化华硕路由器设备流量详细统计表
        if (!existingTables.Contains("asusrouterdevicetrafficdetail"))
        {
            var createTableSql = @"
                CREATE TABLE public.asusrouterdevicetrafficdetail (
                    id varchar(50) NOT NULL,
                    mac varchar(50) NOT NULL,
                    statdate timestamp NOT NULL,
                    appname varchar(200) NOT NULL,
                    uploadbytes bigint NOT NULL DEFAULT 0,
                    downloadbytes bigint NOT NULL DEFAULT 0,
                    createdat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    updatedat timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                    CONSTRAINT asusrouterdevicetrafficdetail_pk PRIMARY KEY (id)
                );

                CREATE INDEX idx_asusrouterdevicetrafficdetail_mac ON public.asusrouterdevicetrafficdetail(mac);
                CREATE INDEX idx_asusrouterdevicetrafficdetail_statdate ON public.asusrouterdevicetrafficdetail(statdate);
                CREATE INDEX idx_asusrouterdevicetrafficdetail_mac_statdate ON public.asusrouterdevicetrafficdetail(mac, statdate);
                CREATE INDEX idx_asusrouterdevicetrafficdetail_appname ON public.asusrouterdevicetrafficdetail(appname);

                COMMENT ON TABLE public.asusrouterdevicetrafficdetail IS '华硕路由器设备流量详细统计表（按应用/协议分类）';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.id IS '主键ID';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.mac IS '设备MAC地址';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.statdate IS '统计日期';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.appname IS '应用/协议名称';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.uploadbytes IS '上传字节数';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.downloadbytes IS '下载字节数';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.createdat IS '创建时间';
                COMMENT ON COLUMN public.asusrouterdevicetrafficdetail.updatedat IS '更新时间';
                ";
            _dbConnection.Execute(createTableSql);
        }

        _dbConnection.Dispose();
    }

    /// <summary>
    /// 获取所有已存在的表名集合
    /// </summary>
    private HashSet<string> GetExistingTables(IDbConnection dbConnection)
    {
        var sql = @"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';";
        var tables = dbConnection.Query<string>(sql);
        return new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
    }
}