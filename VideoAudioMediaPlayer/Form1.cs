using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Microsoft.VisualBasic.Logging;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO.Pipes;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using VideoAudioMediaPlayer.Properties;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace VideoAudioMediaPlayer
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private string displayFileName;
        private Bitmap waveformImage = null;
        private string waveFormFileName;

        private LibVLC _libVLC;
        private string[] defaultLibVLCOptions = new string[] { "--input-repeat=2" };

        private MediaPlayer _mediaPlayer;

        // named pipe + mutex
        private const string MutexName = "MUTEX_SINGLEINSTANCEANDNAMEDPIPE";
        private bool _firstApplicationInstance;
        private Mutex _mutexApplication;

        private const string PipeName = "PIPE_SINGLEINSTANCEANDNAMEDPIPE";
        private readonly object _namedPiperServerThreadLock = new object();
        private NamedPipeServerStream _namedPipeServerStream;
        private NamedPipeXmlPayload _namedPipeXmlPayload;

        double[] peakSeconds;
        string lastFile;
        bool initialPlay = false;
        int keyEventCounter = 0;
        long targetSeekTime = 0;
        int targetSeekDirection = 1;
        bool nextCheckStep = false;
        long seekStep = 8 * 1000;
        long seekFromTime;
        int peakSamplesPerSecond = 4;
        long maxAudioPos = 0;
        long minAudioPos = long.MaxValue;
        long maxAudioLength = long.MaxValue;

        public Form1()
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Application.StartupPath, "log.txt"), "logger"));
            Trace.AutoFlush = true;

            Trace.WriteLine("Startup");

            if (!DesignMode)
                Core.Initialize();

            InitializeComponent();

            SuspendLayout();

            waveFormFileName = Path.Combine(Application.StartupPath, "ffmpeg\\output.png");

            // init vlc (option = playback loop)
            CreateLibVLC();
            CreateMediaPlayer();

            ResumeLayout();
        }

        private void CreateLibVLC(params string[] playerOptions)
        {
            if (_libVLC != null)
            {
                mainVideoView.MediaPlayer = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
                _libVLC.Dispose();
                _libVLC = null;
            }

            List<string> vlcOptions = new List<string>(defaultLibVLCOptions);
            vlcOptions.AddRange(playerOptions);

            _libVLC = new LibVLC(vlcOptions.ToArray());
        }

        private void CreateMediaPlayer()
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.TimeChanged += _mediaPlayer_TimeChanged;
            _mediaPlayer.Playing += _mediaPlayer_Playing;

            mainVideoView.MediaPlayer = _mediaPlayer;
        }

        private void _mediaPlayer_Playing(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    _mediaPlayer_Playing(sender, e);
                });
                return;
            }

            if (!initialPlay)
                return;

            Trace.WriteLine("Playing");

            // allow mouse clicks - need to disable event handling
            _mediaPlayer.EnableMouseInput = false;
            _mediaPlayer.EnableKeyInput = false;

            maxAudioLength = _mediaPlayer.Length;

            // update peaks
            UpdateWaveFormWithPeaks(peakSeconds);

            // indicate playing already fired
            initialPlay = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Trace.WriteLine("FormClosing");
            SavePosition();

            Trace.WriteLine("FormClosing end");
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Trace.WriteLine("Before dispose");

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

            Trace.WriteLine("After dispose");
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

        private void DrawWaveformWithPosition(long mediaTime)
        {
            if (waveformImage == null)
                return;

            Debug.WriteLine($"Drawing at {mediaTime}");
            // Create a new bitmap to draw on
            Bitmap tempImage = new Bitmap(waveformPictureBox.Width, waveformPictureBox.Height);
            using (Graphics g = Graphics.FromImage(tempImage))
            {
                // Stretch the waveform image to fit the PictureBox
                g.DrawImage(waveformImage, new Rectangle(0, 0, waveformPictureBox.Width, waveformPictureBox.Height));

                // Draw the red line indicating the current playback position
                double positionRatio = (double)((double)mediaTime / (double)_mediaPlayer.Length);
                int x = (int)(positionRatio * waveformPictureBox.Width);
                g.DrawLine(Pens.Red, x, 0, x, waveformPictureBox.Height);
            }

            waveformPictureBox.Image = tempImage;
        }

        private void UpdateWaveFormWithPeaks(double[] peaks)
        {
            // to complete
            using (Graphics g = Graphics.FromImage(waveformImage))
            {
                foreach (var peak in peaks)
                {
                    double positionRatio = (double)((double)(peak) / (double)_mediaPlayer.Length);
                    int x = (int)(positionRatio * waveformImage.Width);
                    g.DrawRectangle(Pens.Blue, x, (int)(waveformImage.Height * 0.3), 1, (int)(waveformImage.Height * 0.4));
                }
            }
        }

        private void WaveformPictureBox_MouseClick(object? sender, MouseEventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    WaveformPictureBox_MouseClick(sender, e);
                });
                return;
            }
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
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    PlayFile(file);
                });
                return;
            }

            initialPlay = true;

            if (_mediaPlayer.IsPlaying)
                _mediaPlayer.Pause();

            _mediaPlayer.Media = null;

            lastFile = file;
            displayFileName = Path.GetFileName(file);
            lblInfo.Text = displayFileName;

            GenerateWaveform(file, waveFormFileName);

            LoadWaveform();

            peakSeconds = AnalyzeFilePeaks(file);

            // play video
            _mediaPlayer.Play(new Media(_libVLC, file, FromType.FromPath));

            this.TopMost = true;  // Bring the form to the front
            this.TopMost = false; // Reset TopMost to default
            SetForegroundWindow(this.Handle);
            this.Activate();      // Focus the form
            this.Focus();
            SetForegroundWindow(this.Handle);

            //TODO:
            // process path and enumerate files. Find file position.
            // add to key handler code that loads the previous or next files according to the list
            //ProcessPath();
        }

        /// <summary>
        /// Uses ffmpeg to generate waveform image
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputFilePath"></param>
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

        /// <summary>
        /// Uses ffprobe to get RMS levels
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <returns></returns>
        private double[] AnalyzeFilePeaks(string inputFilePath)
        {
            // run ffprobe - ffprobe -f lavfi -i "amovie=04M22S_1710605062.mp4,asetnsamples=n=16000,astats=metadata=1:reset=1" -show_entries frame=pkt_pts_time:frame_tags=lavfi.astats.Overall.RMS_level -of csv=p=0 >out.txt
            // remove summary
            // then normalize using formula 
            Process ffprobe = new Process();
            ffprobe.StartInfo.FileName = Path.Combine(Application.StartupPath, "ffmpeg\\ffprobe");
            string outputFileName = Path.Combine(Application.StartupPath, "peaks.txt");
            // 16000 or 8000
            ffprobe.StartInfo.Arguments = $"-f lavfi -i \"amovie={inputFilePath.Replace("\\", "/\\").Replace(":", "\\\\:")},asetnsamples=n={(int)(16000/peakSamplesPerSecond)},astats=metadata=1:reset=1\" -show_entries frame=pkt_pts_time:frame_tags=lavfi.astats.Overall.RMS_level -of csv=p=0 -o {outputFileName}";
            //ffprobe -f lavfi -i "amovie=04M22S_1710605062.mp4,asetnsamples=n=16000,astats=metadata=1:reset=1" -show_entries frame=pkt_pts_time:frame_tags=lavfi.astats.Overall.RMS_level -of csv=p=0 >out.txt
            //        -f lavfi - i "amovie=C:\Users\JosephLevy\Videos\04M22S_1710605062.mp4,asetnsamples=n=16000,astats=metadata=1:reset=1" - show_entries frame = pkt_pts_time:frame_tags = lavfi.astats.Overall.RMS_level - of csv = p = 0 - o peaks.txt
            ffprobe.StartInfo.UseShellExecute = false;
            // do not redirect - otherwise must read till end
            ffprobe.StartInfo.RedirectStandardOutput = true;
            ffprobe.StartInfo.RedirectStandardError = true;
            ffprobe.StartInfo.CreateNoWindow = true;
            // for debug
            ffprobe.OutputDataReceived += Ffmpeg_OutputDataReceived;
            ffprobe.ErrorDataReceived += Ffmpeg_ErrorDataReceived;
            ffprobe.Start();
            ffprobe.BeginOutputReadLine();
            ffprobe.BeginErrorReadLine();
            ffprobe.WaitForExit();

            double[] levels = File.ReadAllLines(outputFileName).Select(line => double.Parse(line)).ToArray();

            // now normalize
            double maxVal = levels.Max();
            double minVal = levels.Min();

            double[] normalizedLevels = levels.Select(x => (x - minVal) / (maxVal - minVal)).ToArray();

            double notedChange = 0.25; // (amplitude change of total)
            int compressionDistance = 400; // ms of changes in proximity to combine/compress
            return normalizedLevels
            .Select((value, index) => new { Value = value, Index = index })
            .Where(x => x.Index > 0 && (x.Value - normalizedLevels[x.Index - 1]) > notedChange)
            .Select(x => (x.Index * 1000 * (1/(double)peakSamplesPerSecond))) // convert index to time in ms
            .ToArray().Compress(compressionDistance);
        }

        private void Ffmpeg_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            //File.AppendAllText(Path.Combine(Application.StartupPath, "out.txt"), e.Data + "\n");
        }

        private void Ffmpeg_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            //File.AppendAllText(Path.Combine(Application.StartupPath, "error.txt"), e.Data + "\n");
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
                else
                    //PlayFile("C:\\Users\\JosephLevy\\Videos\\04M22S_1710605062.mp4");
                    PlayFile("C:\\Users\\JosephLevy\\Videos\\2024072817\\21M09S_1722176469.mp4");
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

        private void _mediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            try // try catch due to VLC dispose issues
            {
                if (InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        _mediaPlayer_TimeChanged(sender, e);
                    });
                    return;
                }

                Debug.WriteLine($"Attempt to move to {e.Time}");

                // right
                // if loop finished - reset max
                if (Math.Abs(maxAudioPos - maxAudioLength) < 200 || e.Time == 0)
                    maxAudioPos = 0;

                // if moving forward - make sure we don't move backwards
                if (targetSeekDirection > 0 && e.Time < maxAudioPos)
                    return;

                // save max visited time
                if (maxAudioPos < e.Time)
                    maxAudioPos = e.Time;

                // if gone left - track minimum
                if (targetSeekDirection < 0)
                {
                    if(e.Time < minAudioPos)
                        minAudioPos = e.Time;
                }

                // left - if moved too far off the minimum - not good - ignore
                if (targetSeekDirection < 0 && e.Time > minAudioPos + (((seekFromTime - targetSeekTime) / 2)))
                    return;

                // if moved further than min but not too much - stand down and don't validate any more
                if(targetSeekDirection < 0  && (e.Time > minAudioPos + ((seekFromTime - targetSeekTime) / 4)))
                {
                    targetSeekDirection = 0;
                    minAudioPos = long.MaxValue;
                    maxAudioPos = 0;
                }

                // all is well
                // update and draw

                lblInfo.Text = string.Format("{0} - {1} of {2}", displayFileName, ToMins(e.Time / 1000), ToMins(_mediaPlayer.Length / 1000));

                DrawWaveformWithPosition(e.Time);
            }
            catch
            { }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            long mediaTime = _mediaPlayer.Time;

            switch (e.KeyCode)
            {
                case Keys.Space:
                    PlayPause();
                    break;

                case Keys.Right:
                    if (!e.Shift)
                    {
                        if (mediaTime < _mediaPlayer.Length - seekStep)
                        {
                            long targetTime = mediaTime + seekStep;
                            targetSeekDirection = 0;
                            targetSeekTime = targetTime;
                            targetSeekDirection = 1;
                            seekFromTime = mediaTime;

                            ThreadPool.QueueUserWorkItem(_ => { _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(targetTime)); });
                        }
                    }
                    else
                    {
                        // seek to next peak
                        double? time = GetNextPeak(mediaTime);
                        if (time != null)
                        {
                            targetSeekDirection = 0;
                            targetSeekTime = (long)time.Value;
                            targetSeekDirection = 1;
                            seekFromTime = mediaTime;

                            ThreadPool.QueueUserWorkItem(_ => { _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(time.Value)); });
                        }
                    }
                    break;

                case Keys.Left:
                    if (!e.Shift)
                    {
                        if (mediaTime > seekStep)
                        {
                            long targetTime = mediaTime - seekStep;
                            targetSeekDirection = 0;
                            targetSeekTime = targetTime;
                            targetSeekDirection = -1;
                            seekFromTime = mediaTime;

                            ThreadPool.QueueUserWorkItem(_ => { _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(targetTime)); });
                        }
                    }
                    else
                    {
                        // seek to previous peak
                        double? time = GetPreviousPeak(mediaTime);
                        if (time != null)
                        {
                            targetSeekDirection = 0;
                            targetSeekTime = (long)time.Value;
                            targetSeekDirection = -1;
                            seekFromTime = mediaTime;

                            ThreadPool.QueueUserWorkItem(_ => { _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(time.Value)); });
                        }
                    }
                    break;
            }
        }

        private double? GetPreviousPeak(double mediaTime)
        {
            return peakSeconds.Select((value, index) => new { value, index })
            .FirstOrDefault(x => x.value >= mediaTime)?.index is int index && index > 0
            ? peakSeconds[index - 1]
            : (double?)null;
        }

        private double? GetNextPeak(double mediaTime)
        {
            return peakSeconds.FirstOrDefault(n => n > mediaTime);
        }

        private void PlayPause()
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    PlayPause();
                });
                return;
            }

            _mediaPlayer.Pause();
        }

        private void mainVideoView_Click(object sender, EventArgs e)
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
                if (this.Size.Height == 0 || this.Size.Width == 0)
                    this.Size = new Size(800, 600);
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

        /// <summary>
        /// Make louder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void louderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    louderToolStripMenuItem_Click(sender, e);
                });

                return;
            }

            // redefine player with gain and reload file
            CreateLibVLC("--gain=5.5");
            CreateMediaPlayer();
            // replay file
            PlayFile(lastFile);
        }
    }
    public class NamedPipeXmlPayload
    {
        /// <summary>
        ///     A list of command line arguments.
        /// </summary>
        [XmlElement("CommandLineArguments")]
        public List<string> CommandLineArguments { get; set; } = new List<string>();
    }

    public static class StringArrayExtensions
    {
        public static double[] Compress(this double[] source, int distance)
        {
            if (source == null || source.Length == 0)
                return new double[0];

            List<double> result = new List<double>();
            double? lastAdded = null;

            foreach (var number in source)
            {
                // assuming increasing numbers only
                if (lastAdded == null || (number - lastAdded.Value >= distance))
                {
                    result.Add(number);
                    lastAdded = number;
                }
            }

            return result.ToArray();
        }
    }
}
