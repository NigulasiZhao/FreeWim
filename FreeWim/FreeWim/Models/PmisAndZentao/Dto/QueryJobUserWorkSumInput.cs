namespace FreeWim.Models.PmisAndZentao.Dto;

public class QueryJobUserWorkSumInput
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class QueryUserWorkSumDetailInput
{
    public string CreateDate { get; set; } = string.Empty;
}

public class queryUserWorkWeekSumDetailInput
{
    public string WeekStart { get; set; } = string.Empty;
}