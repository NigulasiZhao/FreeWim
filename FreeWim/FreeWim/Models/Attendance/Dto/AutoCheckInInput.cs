using System.ComponentModel;
namespace FreeWim.Models.Attendance.Dto;

public class AutoCheckInInput
{
    [Description("选择自动打卡时间")] public DateTime? SelectTime { get; set; }
    [Description("任务ID")] public string? jobId { get; set; }
}