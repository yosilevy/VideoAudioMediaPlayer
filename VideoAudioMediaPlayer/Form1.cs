using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System.Diagnostics;
using System.IO.Pipes;
using System.Xml.Serialization;
using VideoAudioMediaPlayer.Properties;

namespace VideoAudioMediaPlayer
{
    public partial class Form1 : Form
    {
        private string displayFileName;
        private Bitmap waveformImage;
        private string waveFormFileName;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;

        // named pipe + mutex
        private const string MutexName = "MUTEX_SINGLEINSTANCEANDNAMEDPIPE";
        private bool _firstApplicationInstance;
        private Mutex _mutexApplication;

        private const string PipeName = "PIPE_SINGLEINSTANCEANDNAMEDPIPE";
        private readonly object _namedPiperServerThreadLock = new object();
        private NamedPipeServerStream _namedPipeServerStream;
        private NamedPipeXmlPayload _namedPipeXmlPayload;

        public Form1()
        {
            InitializeComponent();

            waveFormFileName = Path.Combine(Application.StartupPath, "ffmpeg\\output.png");

            Core.Initialize();

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Paused += _mediaPlayer_Paused;
            _mediaPlayer.Playing += _mediaPlayer_Playing;

            _videoView = new VideoView { MediaPlayer = _mediaPlayer };
            _videoView.Dock = DockStyle.Fill;
            _videoView.PreviewKeyDown += Generic_PreviewKeyDown;
            // TODO: doesn't work
            _videoView.Click += _videoView_Click;
            this.Controls.Add(_videoView);

            // Initialize and configure Timer
            playbackTimer.Interval = 100; // Update every 100 ms
            playbackTimer.Tick += PlaybackTimer_Tick;

            // Initialize and configure PictureBox for waveform
            waveformPictureBox.MouseClick += WaveformPictureBox_MouseClick;

            this.Controls.Add(waveformPictureBox);
        }

        private void _mediaPlayer_Playing(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    playbackTimer.Start();
                });
            }
            else
                playbackTimer.Start();
        }

        private void _mediaPlayer_Paused(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    playbackTimer.Stop();
                });
            }
            else
                playbackTimer.Stop();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SavePosition();

            _mediaPlayer.Dispose();
            _libVLC.Dispose();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Dispose the named pipe steam
            if (_namedPipeServerStream != null)
            {
                _namedPipeServerStream.Dispose();
            }
            // Close and dispose our mutex.
            if (_mutexApplication != null)
            {
                _mutexApplication.Dispose();
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            lblInfo.Text = string.Format("{0} - {1} of {2}", displayFileName, ToMins(_mediaPlayer.Time / 1000), ToMins(_mediaPlayer.Length / 1000));

            DrawWaveformWithPosition();
        }

        private void LoadWaveform()
        {
            try
            {
                using (var tempImage = new Bitmap(waveFormFileName))
                {
                    waveformImage = new Bitmap(tempImage);
                    waveformPictureBox.Image = waveformImage;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading waveform image: {ex.Message}");
            }
        }

        private void DrawWaveformWithPosition()
        {
            if (waveformImage == null)
                return;

            // Create a new bitmap to draw on
            Bitmap tempImage = new Bitmap(waveformPictureBox.Width, waveformPictureBox.Height);
            using (Graphics g = Graphics.FromImage(tempImage))
            {
                // Stretch the waveform image to fit the PictureBox
                g.DrawImage(waveformImage, new Rectangle(0, 0, waveformPictureBox.Width, waveformPictureBox.Height));

                // Draw the red line indicating the current playback position
                double positionRatio = (double)((double)_mediaPlayer.Time / (double)_mediaPlayer.Length);
                int x = (int)(positionRatio * waveformPictureBox.Width);
                g.DrawLine(Pens.Red, x, 0, x, waveformPictureBox.Height);
            }

            waveformPictureBox.Image = tempImage;
        }

        private void WaveformPictureBox_MouseClick(object? sender, MouseEventArgs e)
        {
            // Calculate the clicked position ratio
            double clickRatio = (double)e.X / waveformPictureBox.Width;
            double newPosition = clickRatio * _mediaPlayer.Length;
            _mediaPlayer.Time = (long)newPosition;

            if (!_mediaPlayer.IsPlaying)
                _mediaPlayer.Play();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            Application.DoEvents();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Handle the files
                foreach (string file in files)
                    PlayFile(file);
            }
        }

        public void PlayFile(string file)
        {
            if (_mediaPlayer.IsPlaying)
            {
                playbackTimer.Stop();

                this.Invoke((MethodInvoker)delegate
                {
                    _mediaPlayer.Pause();
                    _mediaPlayer.Media = null;
                });
            }

            displayFileName = Path.GetFileName(file);
            lblInfo.Text = displayFileName;

            GenerateWaveform(file, waveFormFileName);

            LoadWaveform();

            // play video
            _mediaPlayer.Play(new Media(_libVLC, file, FromType.FromPath));

            this.TopMost = true;  // Bring the form to the front
            this.TopMost = false; // Reset TopMost to default
            this.Activate();      // Focus the form
        }

        private void GenerateWaveform(string inputFilePath, string outputFilePath)
        {
            Process ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = Path.Combine(Application.StartupPath, "ffmpeg\\ffmpeg");
            ffmpeg.StartInfo.Arguments = $"-i \"{inputFilePath}\" -filter_complex \"compand=gain=6,showwavespic=s={waveformPictureBox.Width}x{waveformPictureBox.Height}:colors=#9cf42f\" -frames:v 1 \"{outputFilePath}\" -y";
            //https://stackoverflow.com/questions/32254818/generating-a-waveform-using-ffmpeg
            //https://ffmpeg.org/ffmpeg-filters.html
            ffmpeg.StartInfo.UseShellExecute = false;
            // do not redirect - otherwise must read till end
            ffmpeg.StartInfo.RedirectStandardOutput = true;
            ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.StartInfo.CreateNoWindow = true;
            // for debug
            ffmpeg.OutputDataReceived += Ffmpeg_OutputDataReceived;
            ffmpeg.ErrorDataReceived += Ffmpeg_ErrorDataReceived;
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.WaitForExit();
        }


        private void Ffmpeg_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            File.AppendAllText(Path.Combine(Application.StartupPath, "out.txt"), e.Data + "\n");
        }

        private void Ffmpeg_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            File.AppendAllText(Path.Combine(Application.StartupPath, "error.txt"), e.Data + "\n");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // First instance
            if (IsApplicationFirstInstance())
            {
                // Create a new pipe - it will return immediately and async wait for connections
                NamedPipeServerCreateServer();

                // Do something
                LoadPosition();

                string[] args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    if (File.Exists(args[1]))
                        PlayFile(args[1]);
                }
                // debug
                //else
                //    PlayFile("C:\\Users\\JosephLevy\\Videos\\23M54S_1710570234.mp4");
            }
            else
            {
                // We are not the first instance, send the named pipe message with our payload and stop loading
                var namedPipeXmlPayload = new NamedPipeXmlPayload
                {
                    CommandLineArguments = Environment.GetCommandLineArgs().ToList()
                };

                // Send the message
                NamedPipeClientSendOptions(namedPipeXmlPayload);

                // Stop loading form and quit
                Close();
            }
        }

        private void Generic_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left)
                e.IsInputKey = true;  // this will trigger the KeyDown event
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            int seekStep = 8 * 1000;
            switch (e.KeyCode)
            {
                case Keys.Space:
                    PlayPause();
                    break;

                case Keys.Right:
                    if (_mediaPlayer.Time < _mediaPlayer.Length - seekStep)
                        _mediaPlayer.Time += seekStep;
                    break;

                case Keys.Left:
                    if (_mediaPlayer.Time > seekStep)
                        _mediaPlayer.Time -= seekStep;
                    break;
            }
        }

        private void PlayPause()
        {
            _mediaPlayer.Pause();
        }

        private void _videoView_Click(object? sender, EventArgs e)
        {
            PlayPause();
        }

        #region "Position"

        private void LoadPosition()
        {
            // Set window location
            if (Settings.Default.WindowLocation != null)
            {
                this.Location = Settings.Default.WindowLocation;
            }

            // Set window size
            if (Settings.Default.WindowSize != null)
            {
                this.Size = Settings.Default.WindowSize;
            }
        }

        private void SavePosition()
        {
            // Copy window location to app settings
            Settings.Default.WindowLocation = this.Location;

            // Copy window size to app settings
            if (this.WindowState == FormWindowState.Normal)
            {
                Settings.Default.WindowSize = this.Size;
            }
            else
            {
                Settings.Default.WindowSize = this.RestoreBounds.Size;
            }

            // Save settings
            Settings.Default.Save();
        }
        #endregion

        private string ToMins(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        #region "Named pipe & mutex"
        private bool IsApplicationFirstInstance()
        {
            // Allow for multiple runs but only try and get the mutex once
            if (_mutexApplication == null)
            {
                _mutexApplication = new Mutex(true, MutexName, out _firstApplicationInstance);
            }

            return _firstApplicationInstance;
        }

        /// <summary>
        ///     Starts a new pipe server if one isn't already active.
        /// </summary>
        private void NamedPipeServerCreateServer()
        {
            //// Create a new pipe accessible by local authenticated users, disallow network
            //var sidNetworkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            //var sidWorld = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            //var pipeSecurity = new PipeSecurity();

            //// Deny network access to the pipe
            //var accessRule = new PipeAccessRule(sidNetworkService, PipeAccessRights.ReadWrite, AccessControlType.Deny);
            //pipeSecurity.AddAccessRule(accessRule);

            //// Alow Everyone to read/write
            //accessRule = new PipeAccessRule(sidWorld, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            //pipeSecurity.AddAccessRule(accessRule);

            //// Current user is the owner
            //SecurityIdentifier sidOwner = WindowsIdentity.GetCurrent().Owner;
            //if (sidOwner != null)
            //{
            //    accessRule = new PipeAccessRule(sidOwner, PipeAccessRights.FullControl, AccessControlType.Allow);
            //    pipeSecurity.AddAccessRule(accessRule);
            //}

            // Create pipe and start the async connection wait
            _namedPipeServerStream = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0);


            // Begin async wait for connections
            _namedPipeServerStream.BeginWaitForConnection(NamedPipeServerConnectionCallback, _namedPipeServerStream);
        }

        /// <summary>
        ///     The function called when a client connects to the named pipe. Note: This method is called on a non-UI thread.
        /// </summary>
        /// <param name="iAsyncResult"></param>
        private void NamedPipeServerConnectionCallback(IAsyncResult iAsyncResult)
        {
            try
            {
                // End waiting for the connection
                _namedPipeServerStream.EndWaitForConnection(iAsyncResult);

                // Read data and prevent access to _namedPipeXmlPayload during threaded operations
                lock (_namedPiperServerThreadLock)
                {
                    // Read data from client
                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    _namedPipeXmlPayload = (NamedPipeXmlPayload)xmlSerializer.Deserialize(_namedPipeServerStream);

                    this.Invoke((MethodInvoker)delegate
                    {
                        PlayFile(_namedPipeXmlPayload.CommandLineArguments[1]);
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                // EndWaitForConnection will exception when someone closes the pipe before connection made
                // In that case we dont create any more pipes and just return
                // This will happen when app is closing and our pipe is closed/disposed
                return;
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                // Close the original pipe (we will create a new one each time)
                _namedPipeServerStream.Dispose();
            }

            // Create a new pipe for next connection
            NamedPipeServerCreateServer();
        }

        /// <summary>
        ///     Uses a named pipe to send the currently parsed options to an already running instance.
        /// </summary>
        /// <param name="namedPipePayload"></param>
        private void NamedPipeClientSendOptions(NamedPipeXmlPayload namedPipePayload)
        {
            try
            {
                using (var namedPipeClientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    namedPipeClientStream.Connect(3000); // Maximum wait 3 seconds

                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    xmlSerializer.Serialize(namedPipeClientStream, namedPipePayload);
                }
            }
            catch (Exception)
            {
                // Error connecting or sending
            }
        }
        #endregion

    }
    public class NamedPipeXmlPayload
    {
        /// <summary>
        ///     A list of command line arguments.
        /// </summary>
        [XmlElement("CommandLineArguments")]
        public List<string> CommandLineArguments { get; set; } = new List<string>();
    }
}
