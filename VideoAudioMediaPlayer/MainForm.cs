using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Formats.Tar;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using VideoAudioMediaPlayer.Properties;

namespace VideoAudioMediaPlayer
{
    public partial class MainForm : Form
    {
        private string displayFileName;
        private string waveFormFileName;
        private MediaHandler _mediaHandler;
        private WaveformHandler _waveformHandler;
        private NamedPipeServer _namedPipeServer;
        private NamedPipeClient _namedPipeClient;
        private double[] peakSeconds;
        private string lastFile;
        private double lastGain = 1;

        public MainForm()
        {
            InitializeComponent();
            LoadSettings();
            InitializeHandlers();

            // debugging...
            //Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Application.StartupPath, "log.txt"), "logger"));
            //Trace.AutoFlush = true;
            //Trace.WriteLine("Startup");
        }

        private void InitializeHandlers()
        {
            // init single app mechanism
            _namedPipeServer = new NamedPipeServer(this);
            _namedPipeClient = new NamedPipeClient();

            // init media player
            _mediaHandler = new MediaHandler(videoWebView);
            _mediaHandler.TimeChanged += OnVideoPlayerTimeChanged;
            _mediaHandler.VideoPlayerKeyDown += OnVideoPlayerKeyDown;
            _mediaHandler.DurationKnown += OnVideoPlayerDurationKnown;

            // init wave form handler
            _waveformHandler = new WaveformHandler();
        }

        private void LoadSettings()
        {
            waveFormFileName = Path.Combine(Application.StartupPath, "ffmpeg\\output.png");
        }

        private async void MainForm_Load(object sender, System.EventArgs e)
        {
            // if first instance - handle file
            if (!_namedPipeServer.IsFirstInstance())
            {
                _namedPipeClient.Send(Environment.GetCommandLineArgs());
                Close();
                return;
            }

            // we're the only instance
            _namedPipeServer.Start();
            LoadPosition();

            await PlayInitialFile();
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

        private async Task PlayInitialFile()
        {
            // init web view
            await _mediaHandler.InitializeWebViewAsync();

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
                //PlayFile("C:\\Users\\JosephLevy\\Videos\\04M22S_1710605062.mp4");
                PlayFile("C:\\Users\\JosephLevy\\Videos\\From nas\\xiaomi_camera_videos\\607ea4123be4\\2025072209\\00M58S_1753164058.mp4");
            }
        }

        public void PlayFile(string file)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { PlayFile(file); });
                return;
            }

            waveFormShown = false;

            lastFile = file;
            lastGain = 1;

            displayFileName = Path.GetFileName(file);
            setFormText(displayFileName);

            // Load associated .txt file into infoListView
            LoadInfoListForVideo(file);

            // load new video
            _mediaHandler.Load(file);

            // bring to front and focus windows
            WindowsInteropConnector.FocusAndForegroundForm(this);
        }

        private void LoadInfoListForVideo(string videoFilePath)
        {
            if (infoListBox.InvokeRequired)
            {
                infoListBox.Invoke((MethodInvoker)delegate { LoadInfoListForVideo(videoFilePath); });
                return;
            }
            infoListBox.Items.Clear();
            string txtFile = Path.ChangeExtension(videoFilePath, ".txt");
            if (File.Exists(txtFile))
            {
                var lines = File.ReadAllLines(txtFile);
                if (lines.Length > 1)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                            infoListBox.Items.Add(lines[i]);
                    }
                }
            }
        }

        private void OnVideoPlayerDurationKnown(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { OnVideoPlayerDurationKnown(sender, e); });
                return;
            }

            if (_mediaHandler.Length > 90)
                return;

            HandleWaveform();
        }

        bool waveFormShown = false;

        private void HandleWaveform()
        {
            if (waveFormShown)
                return;

            waveFormShown = true;

            Task.Run(() =>
            {
                _waveformHandler.GenerateWaveform(lastFile, waveFormFileName, waveformPictureBox.Width, waveformPictureBox.Height);
                _waveformHandler.LoadWaveform(waveFormFileName, waveformPictureBox);

                peakSeconds = new PeakAnalyzer().AnalyzeFilePeaks(lastFile);

                // update waveform
                _waveformHandler.UpdateWaveFormWithPeaks(peakSeconds, waveformPictureBox, _mediaHandler.Length);
            });
        }

        private void OnVideoPlayerTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { OnVideoPlayerTimeChanged(sender, e); });
                return;
            }

            if (!_mediaHandler.HandleMovement(e.Time))
                return;

            if (_mediaHandler.Length == 0)
                return;

            _waveformHandler.DrawWaveformWithPosition(e.Time, waveformPictureBox, _mediaHandler.Length);

            setFormText(displayFileName, e.Time);
        }

        private string ToMins(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private void OnVideoPlayerKeyDown(object? sender, VideoPlayerKeyDownEventArgs e)
        {
            var mediaTime = _mediaHandler.Time;

            switch (e.Key)
            {
                case "?":
                    _mediaHandler.ShowHelp();
                    break;

                case " ":
                case "Enter":
                    _mediaHandler.PlayPause();
                    break;

                case "+":
                    lastGain += 1;

                    // double the volume
                    _mediaHandler.SetGain(lastGain);
                    _mediaHandler.Play();

                    break;

                case "-":
                    lastGain -= 1;

                    // double down the volume
                    _mediaHandler.SetGain(lastGain);
                    _mediaHandler.Play();

                    break;

                case "ArrowRight":
                    if (!e.ShiftKey)
                    {
                        _mediaHandler.SeekForwardStep(mediaTime, e.CtrlKey);
                    }
                    else
                    {
                        double? time = GetNextPeak(mediaTime);
                        if (time != null)
                        {
                            _mediaHandler.SeekForwardTo(time.Value);
                        }
                    }
                    break;

                case "ArrowLeft":
                    if (!e.ShiftKey)
                    {
                        _mediaHandler.SeekBackwardStep(mediaTime, e.CtrlKey);
                    }
                    else
                    {
                        double? time = GetPreviousPeak(mediaTime);
                        if (time != null)
                        {
                            _mediaHandler.SeekBackwardTo(time.Value);
                        }
                    }
                    break;
            }
        }

        private void mainVideoView_Click(object sender, System.EventArgs e)
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
            _mediaHandler.SeekTo(newPosition);

            _mediaHandler.Play();
        }

        private double? GetPreviousPeak(double mediaTime)
        {
            // get the peak that is closest to the current time - 0.1
            return peakSeconds.Select((value, index) => new { value, index })
                              .FirstOrDefault(x => x.value >= (mediaTime - 0.1))?.index is int index && index > 0
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
                if (this.Size.Height < 400 || this.Size.Width < 400)
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

        private void setFormText(string displayFileName)
        {
            setFormText(displayFileName, null);
        }

        private void setFormText(string displayFileName, double? time)
        {
            this.Text = $"Smart Player - {displayFileName}" +
                (time.HasValue
                    ? $" - {ToMins(time.Value)} of {ToMins(_mediaHandler.Length)}"
                    : ""
                    );
        }

        private void waveformPictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            HandleWaveform();
        }

        private void InfoListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (infoListBox.SelectedIndex == -1)
                return;
            string line = infoListBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(line))
                return;
            // Try to find a time in the format [xx.xx or xx.xx s]
            var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(?<start>[0-9]+\.?[0-9]*)s? *->");
            if (match.Success && double.TryParse(match.Groups["start"].Value, out double seconds))
            {
                // seek to a bit before
                _mediaHandler.SeekTo(Math.Max(0, seconds - 2));
            }
        }
    }
}
