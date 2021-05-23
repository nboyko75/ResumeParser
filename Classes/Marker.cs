using System;

namespace ResumeParser.Classes
{
    public class MarkerInfo 
    {
        public string Category { get; set; }
        public string Attribute { get; set; }
        public string[] Markers { get; set; }
    }

    public class Marker
    {
        public MarkerInfo[] Items { get; set; }
    }
}
