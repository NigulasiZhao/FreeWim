namespace FreeWim.Models.Email;

public class EmaliInfo
{
    public string? Host { get; set; }
    public int Port { get; set; }

    public bool UseSsl { get; set; }

    public string? UserName { get; set; }

    public string? PassWord { get; set; }

    public List<ReceiveIndo>? ReceiveList { get; set; }
}

public class ReceiveIndo
{
    public string? Address { get; set; }
    public string? Name { get; set; }
}