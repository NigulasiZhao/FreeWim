namespace FreeWim.Models.PmisAndZentao.Dto;

public class OvertimeQueryRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? DepartmentName { get; set; }
    public string? ApplicantName { get; set; }
    public int Index { get; set; } = 1;
    public int Size { get; set; } = 30;
}
