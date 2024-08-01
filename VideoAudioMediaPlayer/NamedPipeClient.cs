using System.IO.Pipes;
using System.Xml.Serialization;

namespace VideoAudioMediaPlayer
{
    public class NamedPipeClient
    {
        private const string PipeName = "PIPE_SINGLEINSTANCEANDNAMEDPIPE";

        public void Send(string[] commandLineArgs)
        {
            try
            {
                using (var namedPipeClientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    namedPipeClientStream.Connect(3000);

                    var namedPipeXmlPayload = new NamedPipeXmlPayload
                    {
                        CommandLineArguments = commandLineArgs.ToList()
                    };

                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    xmlSerializer.Serialize(namedPipeClientStream, namedPipeXmlPayload);
                }
            }
            catch (Exception)
            {
                // Error connecting or sending
            }
        }
    }
}
