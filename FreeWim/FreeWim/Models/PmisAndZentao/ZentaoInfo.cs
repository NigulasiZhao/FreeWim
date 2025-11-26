namespace FreeWim.Models.PmisAndZentao;

public class ZentaoInfo
{
    public string? Url { get; set; }
    public string? Account { get; set; }
    public string? Password { get; set; }
}

public class ZentaoResponse
{
    public string? status { get; set; }
    public string? data { get; set; }
    public string? md5 { get; set; }
}