using System;
using System.Xml.Serialization;

namespace EkstaziSharp.Model
{
    
    public class TrackedMethodRef
    {
        [XmlAttribute("uid")]
        public UInt32 UniqueId { get; set; }

        // visit count
        [XmlAttribute("vc")]
        public int VisitCount { get; set; }

    }


    [Serializable]
    public sealed class TrackedMethod
    {
        
        [XmlAttribute("uid")]
        public UInt32 UniqueId { get; set; }

        
        [XmlAttribute("token")]
        public int MetadataToken { get; set; }

        
        [XmlAttribute("name")]
        public string Name { get; set; }

        
        [XmlAttribute("strategy")]
        public string Strategy { get; set; }

    }
}
