namespace FreeWim.Models.Gogs;

public class GitInfoModel
{
    public string? JenkinsBuildUrl { get; set; }

    public string? BranchName { get; set; }
    public List<Dictionary<string, string>>? BuildParameters { get; set; }
}