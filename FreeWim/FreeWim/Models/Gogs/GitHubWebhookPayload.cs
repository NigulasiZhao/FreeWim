using LibGit2Sharp;
using System.Reflection;

namespace FreeWim.Models.Gogs;

public class GitHubWebhookPayload
{
    public string? @ref { get; set; } // ref字段
    public string? before { get; set; } // before字段
    public string? after { get; set; } // after字段
    public Repository repository { get; set; } // repository字段
    public Pusher pusher { get; set; } // pusher字段
    public Sender sender { get; set; } // sender字段
    public bool created { get; set; } // created字段
    public bool deleted { get; set; } // deleted字段
    public bool forced { get; set; } // forced字段
    public string? compare { get; set; } // compare字段
    public List<Commit> commits { get; set; } // commits字段
    public Commit head_commit { get; set; } // head_commit字段
}

public class Repository
{
    public long id { get; set; } // id字段
    public string? node_id { get; set; } // node_id字段
    public string? name { get; set; } // name字段
    public string? full_name { get; set; } // full_name字段
    public bool @private { get; set; } // private字段
    public Owner owner { get; set; } // owner字段
    public List<string?> topics { get; set; } // topics字段
    public string? visibility { get; set; } // visibility字段
    public int forks { get; set; } // forks字段
    public int open_issues { get; set; } // open_issues字段
    public int watchers { get; set; } // watchers字段
    public string? default_branch { get; set; } // default_branch字段
    public int stargazers { get; set; } // stargazers字段
    public string? master_branch { get; set; } // master_branch字段
}

public class Owner
{
    public string? name { get; set; } // name字段
    public string? email { get; set; } // email字段
    public string? login { get; set; } // login字段
    public int id { get; set; } // id字段
}

public class Pusher
{
    public string? name { get; set; } // name字段
    public string? email { get; set; } // email字段
}

public class Sender
{
    public string? login { get; set; } // login字段
    public int id { get; set; } // id字段
    public string? node_id { get; set; } // node_id字段
    public string? avatar_url { get; set; } // avatar_url字段
    public string? gravatar_id { get; set; } // gravatar_id字段
}

public class Commit
{
    public string? id { get; set; } // id字段
    public string? tree_id { get; set; } // tree_id字段
    public bool distinct { get; set; } // distinct字段
    public string? message { get; set; } // message字段
    public DateTime timestamp { get; set; } // timestamp字段
    public string? url { get; set; } // url字段
    public Author author { get; set; } // author字段
    public Author committer { get; set; } // committer字段
    public List<string?> added { get; set; } // added字段
    public List<string?> removed { get; set; } // removed字段
    public List<string?> modified { get; set; } // modified字段
}