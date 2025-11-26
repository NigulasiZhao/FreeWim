namespace FreeWim.Models.EventInfo;

#region MyRegion

public class KeepResponse
{
    public bool ok { get; set; }
    public KeepResponseData? data { get; set; }

    public int errorCode { get; set; }

    public string? text { get; set; }
}

public class KeepResponseData
{
    public List<KeepResponseDataDetail>? records { get; set; }
}

public class KeepResponseDataDetail
{
    public string? date { get; set; }
    public string? calorieSum { get; set; }
    public string? durationSum { get; set; }
    public List<DailyList>? logs { get; set; }
}

public class DailyList
{
    public string? type { get; set; }
    public LogStats? stats { get; set; }
}

public class LogStats
{
    public string? id { get; set; }

    public string? type { get; set; }
    public string? name { get; set; }
    public string? nameSuffix { get; set; }
    public long startTime { get; set; }
    public long endTime { get; set; }
    public string? steps { get; set; }
    public string? doneDate { get; set; }
    public string? calorie { get; set; }
}

#endregion

#region SportLog

public class SportLogResponse
{
    public bool ok { get; set; }
    public SportLogData? data { get; set; }

    public int errorCode { get; set; }

    public string? text { get; set; }
}

public class SportLogData
{
    public string? logId { get; set; }
    public List<SportLogSections>? sections { get; set; }

    public string? userId { get; set; }
}

public class SportLogSections
{
    public string? style { get; set; }
    public SportLogContent? content { get; set; }
}

public class SportLogContent
{
    public List<SportLogContentList>? list { get; set; }
}

public class SportLogContentList
{
    public string? title { get; set; }
    public string? valueStr { get; set; }
    public string? unit { get; set; }
    public string? gridType { get; set; }
    public string? chartIcon { get; set; }
    public string? privacyInfo { get; set; }
}

#endregion

#region V1

//public class KeepResponse
//{
//    public bool ok { get; set; }
//    public KeepResponseData data { get; set; }

//    public int errorCode { get; set; }

//    public string text { get; set; }

//}
//public class KeepResponseData
//{
//    public KeepResponseDataDetail data { get; set; }
//}
//public class KeepResponseDataDetail
//{
//    public int maxShowCount { get; set; }
//    public int batchStageLimit { get; set; }

//    public List<DailyList> dailyList { get; set; }
//}
//public class DailyList
//{
//    public string title { get; set; }
//    public List<LogList> logList { get; set; }
//}
//public class LogList
//{
//    public string id { get; set; }
//    public string name { get; set; }
//    public string nameSuffix { get; set; }
//    public string endTimeText { get; set; }

//    public List<string> indicatorList { get; set; }
//}

#endregion