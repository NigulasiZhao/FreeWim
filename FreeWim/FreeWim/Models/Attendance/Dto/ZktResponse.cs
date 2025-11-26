namespace FreeWim.Models.Attendance.Dto;

public class ZktResponse
{
    public int Ret { get; set; }
    public required string Msg { get; set; }
    public ZktData? Data { get; set; }
}

public class ZktData
{
    public int Count { get; set; }
    public List<ZktItem>? Items { get; set; }
}

public class ZktItem
{
    public int Id { get; set; }
    public string? Pin { get; set; }
    public string? Ename { get; set; }
    public string? Deptnumber { get; set; }
    public string? Deptname { get; set; }
    public string? Checktime { get; set; }
    public string? Sn { get; set; }
    public string? Alias { get; set; }
    public int Verify { get; set; }
    public string? Stateno { get; set; }
    public string? State { get; set; }
}