using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using VideoAudioMediaPlayer.Properties;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace VideoAudioMediaPlayer
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private string displayFileName;
        private string waveFormFileName;
        private MediaHandler _mediaHandler;
        private WaveformHandler _waveformHandler;
        private NamedPipeServer _namedPipeServer;
        private NamedPipeClient _namedPipeClient;
        private double[] peakSeconds;
        private string lastFile;
        private bool initialPlay = false;

        public MainForm()
        {
            InitializeComponent();
            InitializeHandlers();
            LoadSettings();

            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Application.StartupPath, "log.txt"), "logger"));
            Trace.AutoFlush = true;
            Trace.WriteLine("Startup");

            if (!DesignMode)
                Core.Initialize();
        }

        private void InitializeHandlers()
        {
            _mediaHandler = new MediaHandler(mainVideoView);
            _mediaHandler.Playing += OnMediaPlaying;
            _mediaHandler.TimeChanged += OnTimeChanged;

            _namedPipeServer = new NamedPipeServer(this);
            _namedPipeClient = new NamedPipeClient();

            _waveformHandler = new WaveformHandler();
        }

        private void LoadSettings()
        {
            waveFormFileName = Path.Combine(Application.StartupPath, "ffmpeg\\output.png");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (_namedPipeServer.IsFirstInstance())
            {
                _namedPipeServer.Start();
                LoadPosition();
                PlayInitialFile();
            }
            else
            {
                _namedPipeClient.Send(Environment.GetCommandLineArgs());
                Close();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SavePosition();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _namedPipeServer.Dispose();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
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

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                    PlayFile(file);
            }
        }

        private void PlayInitialFile()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                if (File.Exists(args[1]))
                {
                    PlayFile(args[1]);
                }
            }
            else
            {
                // Debug example file
                // PlayFile("C:\\Users\\JosephLevy\\Videos\\04M22S_1710605062.mp4");
                PlayFile("C:\\Users\\JosephLevy\\Videos\\2024072817\\21M09S_1722176469.mp4");
            }
        }

        public void PlayFile(string file)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { PlayFile(file); });
                return;
            }

            initialPlay = true;

            _mediaHandler.UnloadMedia();

            lastFile = file;
            displayFileName = Path.GetFileName(file);
            lblInfo.Text = displayFileName;

            _waveformHandler.GenerateWaveform(file, waveFormFileName, waveformPictureBox.Width, waveformPictureBox.Height);
            _waveformHandler.LoadWaveform(waveFormFileName, waveformPictureBox);

            peakSeconds = new PeakAnalyzer().AnalyzeFilePeaks(file);

            // play new video
            _mediaHandler.Play(file);

            this.TopMost = true;  // Bring the form to the front
            this.TopMost = false; // Reset TopMost to default
            SetForegroundWindow(this.Handle);
            this.Activate();
            this.Focus();
            SetForegroundWindow(this.Handle);
        }

        private void OnMediaPlaying(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { OnMediaPlaying(sender, e); });
                return;
            }

            if (!initialPlay)
                return;

            initialPlay = false;
            // allow mouse clicks - need to disable event handling
            _mediaHandler.SetupPlayerInputEvents();

            _mediaHandler.SampleAudioLength();

            _waveformHandler.UpdateWaveFormWithPeaks(peakSeconds, waveformPictureBox, _mediaHandler.Length);
        }

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            try
            {
                if (InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate { OnTimeChanged(sender, e); });
                    return;
                }

                if (!_mediaHandler.HandleMovement(e.Time))
                    return;

                _waveformHandler.DrawWaveformWithPosition(e.Time, waveformPictureBox, _mediaHandler.Length);

                lblInfo.Text = $"{displayFileName} - {ToMins(e.Time / 1000)} of {ToMins(_mediaHandler.Length / 1000)}";
            }
            catch { }
        }

        private string ToMins(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            long mediaTime = _mediaHandler.Time;

            switch (e.KeyCode)
            {
                case Keys.Space:
                    _mediaHandler.PlayPause();
                    break;

                case Keys.Right:
                    if (!e.Shift)
                    {
                        _mediaHandler.SeekForwardStep(mediaTime);
                    }
                    else
                    {
                        double? time = GetNextPeak(mediaTime);
                        if (time != null)
                        {
                            _mediaHandler.SeekForwardTo((long)time);
                        }
                    }
                    break;

                case Keys.Left:
                    if (!e.Shift)
                    {
                        _mediaHandler.SeekBackwardStep(mediaTime);
                    }
                    else
                    {
                        double? time = GetPreviousPeak(mediaTime);
                        if (time != null)
                        {
                            _mediaHandler.SeekBackwardTo((long)time);
                        }
                    }
                    break;
            }
        }
        private void mainVideoView_Click(object sender, EventArgs e)
        {
            _mediaHandler.PlayPause();
        }

        private void WaveformPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { WaveformPictureBox_MouseClick(sender, e); });
                return;
            }

            double clickRatio = (double)e.X / waveformPictureBox.Width;
            double newPosition = clickRatio * _mediaHandler.Length;
            _mediaHandler.SeekTo((long)newPosition);

            if (!_mediaHandler.IsPlaying)
                _mediaHandler.PlayPause();
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

        private void LoadPosition()
        {
            if (Settings.Default.WindowLocation != null)
            {
                this.Location = Settings.Default.WindowLocation;
            }

            if (Settings.Default.WindowSize != null)
            {
                this.Size = Settings.Default.WindowSize;
                if (this.Size.Height == 0 || this.Size.Width == 0)
                    this.Size = new Size(800, 600);
            }
        }

        private void SavePosition()
        {
            Settings.Default.WindowLocation = this.Location;

            if (this.WindowState == FormWindowState.Normal)
            {
                Settings.Default.WindowSize = this.Size;
            }
            else
            {
                Settings.Default.WindowSize = this.RestoreBounds.Size;
            }

            Settings.Default.Save();
        }

        private void louderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { louderToolStripMenuItem_Click(sender, e); });
                return;
            }

            _mediaHandler.CreateLibVLCWithOptions("--gain=5.5");
            PlayFile(lastFile);
        }

        private void Generic_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left)
                e.IsInputKey = true;
        }
    }
}
