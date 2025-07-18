using FreeWim.Models;
using System;
using System.Xml.Serialization;

namespace FreeWim.Models;

[XmlRoot("server")]
public class Server
{
    public int Id { get; set; }

    [XmlAttribute("name")] public string Name { get; set; }

    [XmlAttribute("country")] public string Country { get; set; }

    [XmlAttribute("sponsor")] public string Sponsor { get; set; }

    [XmlAttribute("host")] public string Host { get; set; }
    [XmlAttribute("url")] public string Url { get; set; }

    [XmlAttribute("lat")] public double lat { get; set; }

    [XmlAttribute("lon")] public double lon { get; set; }

    public double Distance { get; set; }

    public int Latency { get; set; }
    public double downloadSpeed { get; set; }
    public double uploadSpeed { get; set; }

    private Lazy<Coordinate> geoCoordinate;
    public Coordinate GeoCoordinate => geoCoordinate.Value;

    public Server()
    {
        geoCoordinate = new Lazy<Coordinate>(() => new Coordinate(lat, lon));
    }
}