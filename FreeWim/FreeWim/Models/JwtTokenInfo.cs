namespace FreeWim.Models;

public class JwtTokenInfo
{
    public long Iat { get; set; }
    public string Id { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Cid { get; set; } = string.Empty;
    public string MainId { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Sn { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string YhloNum { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string Channel { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public CompanyInfo? Company { get; set; }
    public string TokenFrom { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public long Exp { get; set; }
}

public class CompanyInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
