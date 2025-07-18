using System.Xml.Serialization;

namespace FreeWim.Models;

[XmlRoot("server-config")]
public class ServerConfig
{
    [XmlAttribute("ignoreids")] public string IgnoreIds { get; set; }
}