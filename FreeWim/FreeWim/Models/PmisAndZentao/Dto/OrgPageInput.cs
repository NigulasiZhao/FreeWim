namespace FreeWim.Models.PmisAndZentao.Dto;

public class OrgPageInput
{
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string[] OrgIds { get; set; } = Array.Empty<string>();
}

// public class PersonPageInput
// {
//     public string CreateDate { get; set; } = string.Empty;
// }