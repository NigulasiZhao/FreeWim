namespace FreeWim.Models.PmisAndZentao;

public class ZentaoTaskResponse
{
    public List<ZentaoTaskItem> tasks { get; set; }
}

public class ZentaoTaskItem
{
    public int? id { get; set; }
    public int? project { get; set; }
    public int? parent { get; set; }
    public int? isParent { get; set; }
    public string path { get; set; }
    public int? execution { get; set; }
    public int? module { get; set; }
    public int? design { get; set; }
    public int? story { get; set; }
    public int? storyVersion { get; set; }
    public int? designVersion { get; set; }
    public int? fromBug { get; set; }
    public int? feedback { get; set; }
    public int? fromIssue { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public string mode { get; set; }
    public int? pri { get; set; }
    public double? estimate { get; set; }
    public double? consumed { get; set; }
    public double? left { get; set; }
    public string deadline { get; set; }
    public string status { get; set; }
    public string subStatus { get; set; }
    public string color { get; set; }
    public string mailto { get; set; }
    public string keywords { get; set; }
    public string desc { get; set; }
    public int? version { get; set; }
    public string openedBy { get; set; }
    public string openedDate { get; set; }
    public string assignedTo { get; set; }
    public string assignedDate { get; set; }
    public string estStarted { get; set; }
    public string realStarted { get; set; }
    public string finishedBy { get; set; }
    public string finishedDate { get; set; }
    public string finishedList { get; set; }
    public string canceledBy { get; set; }
    public string canceledDate { get; set; }
    public string closedBy { get; set; }
    public string closedDate { get; set; }
    public int? planDuration { get; set; }
    public int? realDuration { get; set; }
    public string closedReason { get; set; }
    public string lastEditedBy { get; set; }
    public string lastEditedDate { get; set; }
    public string activatedDate { get; set; }
    public int? order { get; set; }
    public int? repo { get; set; }
    public int? mr { get; set; }
    public string entry { get; set; }
    public string lines { get; set; }
    public string v1 { get; set; }
    public string v2 { get; set; }
    public string deleted { get; set; }
    public string vision { get; set; }
    public string qiwangriqi { get; set; }
    public int? to_product { get; set; }
    public int? executionID { get; set; }
    public string executionName { get; set; }
    public string projectName { get; set; }
    public string executionMultiple { get; set; }
    public string executionType { get; set; }
    public string storyID { get; set; }
    public string storyTitle { get; set; }
    public string storyStatus { get; set; }
    public string latestStoryVersion { get; set; }
    public string priOrder { get; set; }
    public bool? needConfirm { get; set; }
    public string assignedToRealName { get; set; }
    public int? progress { get; set; }
    public int? rawParent { get; set; }
    public string estimateLabel { get; set; }
    public string consumedLabel { get; set; }
    public string leftLabel { get; set; }
    public bool? canBeChanged { get; set; }
    public bool? isChild { get; set; }
    public string parentName { get; set; }
}

public class TaskItem
{
    public int Id { get; set; }
    public double Timeleft { get; set; }
    public double TimeConsuming { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class FinishZentaoTaskResponse
{
    public int id { get; set; }
    public float consumed { get; set; }
    public string status { get; set; }
}