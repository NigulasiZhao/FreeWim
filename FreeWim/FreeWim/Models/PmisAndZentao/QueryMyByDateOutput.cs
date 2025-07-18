namespace SpeedTest_CN.Models.PmisAndZentao;

public class QueryMyByDateOutput
{
    public int Code { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public QueryMyByDateOutputResponse Response { get; set; }
}

public class QueryMyByDateOutputResponse
{
    public int total { get; set; }
    public int size { get; set; }
    public int current { get; set; }
    public List<object> orders { get; set; }
    public bool optimizeCountSql { get; set; }
    public bool searchCount { get; set; }
    public object maxLimit { get; set; }
    public object countId { get; set; }
    public int pages { get; set; }
    public List<QueryMyByDateOutputRow> rows { get; set; }
}

public class QueryMyByDateOutputRow
{
    public string id { get; set; }
    public string userId { get; set; }
    public string userName { get; set; }
    public object systemId { get; set; }
    public object systemName { get; set; }
    public string groupId { get; set; }
    public string groupName { get; set; }
    public string fillDate { get; set; }
    public long submitTime { get; set; }
    public int status { get; set; }
    public int hasFile { get; set; }
    public object responsibility1 { get; set; }
    public object responsibilityFile1 { get; set; }
    public object responsibility2 { get; set; }
    public object responsibilityFile2 { get; set; }
    public object responsibility3 { get; set; }
    public object responsibilityFile3 { get; set; }
    public object responsibility4 { get; set; }
    public object responsibilityFile4 { get; set; }
    public object responsibility5 { get; set; }
    public object responsibilityFile5 { get; set; }
    public object responsibility6 { get; set; }
    public object responsibilityFile6 { get; set; }
    public object responsibility7 { get; set; }
    public object responsibilityFile7 { get; set; }
    public object responsibility8 { get; set; }
    public object responsibilityFile8 { get; set; }
    public object responsibility9 { get; set; }
    public object responsibilityFile9 { get; set; }
    public object otherContent { get; set; }
    public object otherFile { get; set; }
    public object suggestContent { get; set; }
    public int hasComment { get; set; }
    public int hasNewComment { get; set; }
    public object created { get; set; }
    public object updater { get; set; }
    public object creater { get; set; }
    public object updated { get; set; }
    public int syncPms { get; set; }
    public object beginDate { get; set; }
    public object endDate { get; set; }
    public object dataRange { get; set; }
    public bool restDay { get; set; }
    public object viewStatus { get; set; }
    public bool takeRest { get; set; }
    public string checkInRule { get; set; }
    public List<QueryMyByDateOutputGoWork> goWorks { get; set; }
    public List<QueryMyByDateOutputOffWork> offWorks { get; set; }
    public QueryMyByDateOutputOvertime overtime { get; set; }
    public bool haveData { get; set; }
    public bool commentAuth { get; set; }
    public object responsibilityList { get; set; }
    public List<object> details { get; set; }
    public object comments { get; set; }
    public object recipientId { get; set; }
    public object recipientName { get; set; }
    public object ccTo { get; set; }
    public object ccToName { get; set; }
}

public class QueryMyByDateOutputGoWork
{
    public string time { get; set; }
    public int clockInStatus { get; set; }
    public string clockInStatusName { get; set; }
}

public class QueryMyByDateOutputOffWork
{
    public string time { get; set; }
    public int clockInStatus { get; set; }
    public string clockInStatusName { get; set; }
}

public class QueryMyByDateOutputOvertime
{
    public string overTime { get; set; }
    public List<object> timeRange { get; set; }
}