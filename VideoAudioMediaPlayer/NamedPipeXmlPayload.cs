using System.Collections.Generic;
using System.Xml.Serialization;

namespace VideoAudioMediaPlayer
{
    public class NamedPipeXmlPayload
    {
        [XmlElement("CommandLineArguments")]
        public List<string> CommandLineArguments { get; set; } = new List<string>();
    }
}
