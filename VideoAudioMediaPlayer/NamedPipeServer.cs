using System;
using System.IO.Pipes;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace VideoAudioMediaPlayer
{
    public class NamedPipeServer : IDisposable
    {
        private const string PipeName = "PIPE_SINGLEINSTANCEANDNAMEDPIPE";
        private const string MutexName = "MUTEX_SINGLEINSTANCEANDNAMEDPIPE";
        private NamedPipeServerStream _namedPipeServerStream;
        private NamedPipeXmlPayload _namedPipeXmlPayload;
        private MainForm _mainForm;
        private Mutex _mutexApplication;
        private bool _firstApplicationInstance;
        private readonly object _namedPipeServerThreadLock = new object();

        public NamedPipeServer(MainForm mainForm)
        {
            _mainForm = mainForm;
        }

        public bool IsFirstInstance()
        {
            if (_mutexApplication == null)
            {
                _mutexApplication = new Mutex(true, MutexName, out _firstApplicationInstance);
            }

            return _firstApplicationInstance;
        }

        public void Start()
        {
            _namedPipeServerStream = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0);

            _namedPipeServerStream.BeginWaitForConnection(NamedPipeServerConnectionCallback, _namedPipeServerStream);
        }

        private void NamedPipeServerConnectionCallback(IAsyncResult iAsyncResult)
        {
            try
            {
                _namedPipeServerStream.EndWaitForConnection(iAsyncResult);

                lock (_namedPipeServerThreadLock)
                {
                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    _namedPipeXmlPayload = (NamedPipeXmlPayload)xmlSerializer.Deserialize(_namedPipeServerStream);

                    _mainForm.Invoke((MethodInvoker)delegate
                    {
                        _mainForm.PlayFile(_namedPipeXmlPayload.CommandLineArguments[1]);
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                _namedPipeServerStream.Dispose();
            }

            Start();
        }

        public void Dispose()
        {
            _namedPipeServerStream?.Dispose();
            _mutexApplication?.Dispose();
        }
    }
}
