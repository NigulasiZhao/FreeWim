namespace FreeWim.Models.Attendance;

public class AddressBookInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ParentId { get; set; }
    public int Weight { get; set; }
    public string? NodeType { get; set; }
    public string? TreeId { get; set; }
    public string? Sn { get; set; }
    public string? Pinyin { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public int Sex { get; set; }
    public string? YhloNum { get; set; }
    public string? MainId { get; set; }
    public string? OrgId { get; set; }
    public string? Job { get; set; }
    public string? JobName { get; set; }
    public string? OrgName { get; set; }
    public int OnlineState { get; set; }
}

public class ListOfPersonnel
{
    public string? RealName { get; set; }
    public string? FlowerName { get; set; }
}