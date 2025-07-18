namespace FreeWim.Models.Gogs;

public class WebhookPayload
{
    public string Ref { get; set; }
    public string Before { get; set; }
    public string After { get; set; }
    public string Compare_Url { get; set; }
    public List<WebhookCommit> Commits { get; set; }
    public WebhookRepository Repository { get; set; }
    public WebhookPusher Pusher { get; set; }
    public WebhookSender Sender { get; set; }
}

public class WebhookCommit
{
    public string Id { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
    public Author Author { get; set; }
    public WebhookCommitter Committer { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Author
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
}

public class WebhookCommitter
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
}

public class WebhookRepository
{
    public int Id { get; set; }
    public WebhookOwner Owner { get; set; }
    public string Name { get; set; }
    public string Full_Name { get; set; }
    public string Description { get; set; }
    public bool Private { get; set; }
    public bool Fork { get; set; }
    public string Html_Url { get; set; }
    public string Ssh_Url { get; set; }
    public string Clone_Url { get; set; }
    public string Website { get; set; }
    public int StarsCount { get; set; }
    public int ForksCount { get; set; }
    public int WatchersCount { get; set; }
    public int OpenIssuesCount { get; set; }
    public string Default_Branch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WebhookOwner
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string Full_Name { get; set; }
    public string Email { get; set; }
    public string Avatar_Url { get; set; }
    public string Username { get; set; }
}

public class WebhookPusher
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string Full_Name { get; set; }
    public string Email { get; set; }
    public string Avatar_Url { get; set; }
    public string Username { get; set; }
}

public class WebhookSender
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string Full_Name { get; set; }
    public string Email { get; set; }
    public string Avatar_Url { get; set; }
    public string Username { get; set; }
}