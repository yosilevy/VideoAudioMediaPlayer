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
        private List<double> transcriptionFragmentsStartTimes;
        private int currentTranscriptionLineIndex = -1;
        private List<string> currentFolderFiles;
        private int currentFolderFileIndex = -1;
        private string currentFolderPath;
        private static readonly HashSet<string> SupportedMediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm",
            ".mp3", ".wav", ".aac", ".m4a", ".flac", ".ogg", ".wma"
        };

        public MainForm()
        {
            InitializeComponent();
            LoadSettings();
            InitializeHandlers();

            // Attach handler for list box item click
            transcriptionListBox.SelectedIndexChanged += TranscriptionListBox_SelectedIndexChanged;

            // Attach handler for splitter movement
            mainSplitContainer.SplitterMoved += MainSplitContainer_SplitterMoved;

            transcriptionListBox.DrawMode = DrawMode.OwnerDrawVariable;
            transcriptionListBox.MeasureItem += TranscriptionListBox_MeasureItem;

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
                //PlayFile("C:\\Users\\JosephLevy\\Videos\\���� �� ���.mp4");
                PlayFile("C:\\Users\\JosephLevy\\Videos\\2025082311\\09M14S_1755936554.mp4");
                //PlayFile("C:\\Users\\JosephLevy\\Videos\\From nas\\xiaomi_camera_videos\\607ea4123be4\\2025072209\\00M58S_1753164058.mp4");
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

            // Build or reuse the folder file list for navigation
            string fileDirectory = Path.GetDirectoryName(file);
            if (!string.Equals(fileDirectory, currentFolderPath, StringComparison.OrdinalIgnoreCase)
                || currentFolderFiles == null || currentFolderFiles.Count == 0)
            {
                RefreshFolderListing(fileDirectory);
            }

            // Track the index of the current file in the cached list
            currentFolderFileIndex = currentFolderFiles?.FindIndex(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase)) ?? -1;

            lastFile = file;
            lastGain = 1;

            displayFileName = Path.GetFileName(file);
            setFormText(displayFileName);

            // Load associated .txt file into transcriptionListBox
            LoadTranscriptionForVideo(file);

            // load new video
            _mediaHandler.Load(file);

            // bring to front and focus windows
            WindowsInteropConnector.FocusAndForegroundForm(this);
        }

        private void LoadTranscriptionForVideo(string videoFilePath)
        {
            if (transcriptionListBox.InvokeRequired)
            {
                transcriptionListBox.Invoke((MethodInvoker)delegate { LoadTranscriptionForVideo(videoFilePath); });
                return;
            }
            transcriptionListBox.Items.Clear();
            transcriptionFragmentsStartTimes = new List<double>();
            currentTranscriptionLineIndex = -1;

            string txtFile = Path.ChangeExtension(videoFilePath, ".txt");

            // First check if there's a "transcriptions" subfolder
            string videoDirectory = Path.GetDirectoryName(videoFilePath);
            string transcriptionsFolder = Path.Combine(videoDirectory, "transcriptions");
            string txtFileInTranscriptions = Path.Combine(transcriptionsFolder, Path.GetFileName(txtFile));

            // Use the file in transcriptions folder if it exists, otherwise use the original location
            string finalTxtFile = Directory.Exists(transcriptionsFolder) && File.Exists(txtFileInTranscriptions)
                ? txtFileInTranscriptions
                : txtFile;

            bool found = false;
            if (File.Exists(finalTxtFile))
            {
                var lines = File.ReadAllLines(finalTxtFile);
                if (lines.Length > 1)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            // Parse start time from the line
                            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"\[(?<start>[0-9]+\.?[0-9]*)s? *->");
                            double startTime = -1;
                            if (match.Success && double.TryParse(match.Groups["start"].Value, out double parsedStartTime))
                            {
                                startTime = parsedStartTime;
                            }
                            transcriptionFragmentsStartTimes.Add(startTime);

                            // Remove all brackets and their contents, then trim
                            string displayLine = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[[^\]]*\]", "").Trim();
                            transcriptionListBox.Items.Add(new TranscriptionLine
                            {
                                DisplayText = displayLine,
                                StartTime = startTime,
                                OriginalLine = lines[i]
                            });
                        }
                    }
                    found = transcriptionListBox.Items.Count > 0;
                }
            }
            transcriptionListBox.Visible = found;
            mainSplitContainer.Panel2Collapsed = !found;
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

            // Highlight current transcription line
            HighlightCurrentTranscriptionLine(e.Time);
        }

        private void HighlightCurrentTranscriptionLine(double currentTime)
        {
            if (transcriptionFragmentsStartTimes == null || transcriptionFragmentsStartTimes.Count == 0)
                return;

            // Find the current line based on time
            int newCurrentLineIndex = -1;
            for (int i = 0; i < transcriptionFragmentsStartTimes.Count; i++)
            {
                if (transcriptionFragmentsStartTimes[i] >= 0 && transcriptionFragmentsStartTimes[i] <= currentTime)
                {
                    newCurrentLineIndex = i;
                }
                else if (transcriptionFragmentsStartTimes[i] > currentTime)
                {
                    break;
                }
            }

            // Update current line index if different
            if (currentTranscriptionLineIndex != newCurrentLineIndex)
            {
                currentTranscriptionLineIndex = newCurrentLineIndex;
                transcriptionListBox.Invalidate(); // Trigger redraw
            }
        }

        private string ToMins(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private void OnVideoPlayerKeyDown(object? sender, VideoPlayerKeyDownEventArgs e)
        {
            // e.Key is already normalized ("ArrowRight", "Enter", etc.)
            HandleMediaKeyDown(e.Key, e.ShiftKey, e.CtrlKey);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Normalize Keys to string for HandleMediaKeyDown
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            string key = e.KeyData switch
            {
                Keys.OemQuestion => "?",
                Keys.Space => " ",
                Keys.Enter => "Enter",
                Keys.Add => "+",
                Keys.Subtract => "-",
                Keys.Right => "ArrowRight",
                Keys.Left => "ArrowLeft",
                _ => null
            };
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            if (key != null)
            {
                HandleMediaKeyDown(key, e.Shift, e.Control);
            }
        }

        // Add this new method to MainForm
        private void HandleMediaKeyDown(string key, bool shift, bool ctrl)
        {
            var mediaTime = _mediaHandler.Time;

            switch (key)
            {
                case "?":
                    ShowHelpDialog();
                    break;

                case " ":
                case "Enter":
                    _mediaHandler.PlayPause();
                    break;

                case "+":
                    lastGain += 1;
                    _mediaHandler.SetGain(lastGain);
                    _mediaHandler.Play();
                    break;

                case "-":
                    lastGain -= 1;
                    _mediaHandler.SetGain(lastGain);
                    _mediaHandler.Play();
                    break;

                case "ArrowRight":
                    if (ctrl)
                    {
                        NavigateToAdjacentFile(+1);
                        break;
                    }
                    if (!shift)
                    {
                        _mediaHandler.SeekForwardStep(mediaTime, ctrl);
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
                    if (ctrl)
                    {
                        NavigateToAdjacentFile(-1);
                        break;
                    }
                    if (!shift)
                    {
                        _mediaHandler.SeekBackwardStep(mediaTime, ctrl);
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

        private void ShowHelpDialog()
        {
            string help =
                "Keyboard shortcuts\r\n\r\n" +
                "Space / Enter: Play/Pause\r\n" +
                "+ / -: Increase / Decrease volume gain\r\n" +
                "Arrow Right: Seek forward\r\n" +
                "Shift + Arrow Right: Jump to next peak\r\n" +
                "Arrow Left: Seek backward\r\n" +
                "Shift + Arrow Left: Jump to previous peak\r\n" +
                "Ctrl + Arrow Right: Next file in folder\r\n" +
                "Ctrl + Arrow Left: Previous file in folder\r\n" +
                "Click waveform: Seek to clicked position\r\n" +
                "Click transcription line: Seek near that time";

            MessageBox.Show(this, help, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool IsSupportedMediaFile(string path)
        {
            string ext = Path.GetExtension(path) ?? string.Empty;
            return SupportedMediaExtensions.Contains(ext);
        }

        private void RefreshFolderListing(string directory)
        {
            currentFolderPath = directory;
            try
            {
                currentFolderFiles = Directory
                    .EnumerateFiles(currentFolderPath)
                    .Where(IsSupportedMediaFile)
                    .OrderBy(Path.GetFileName)
                    .ToList();
            }
            catch
            {
                currentFolderFiles = new List<string>();
            }
        }

        private void NavigateToAdjacentFile(int offset)
        {
            if (currentFolderFiles == null || currentFolderFiles.Count == 0 || currentFolderFileIndex < 0)
                return;

            int nextIndex = currentFolderFileIndex + offset;
            if (nextIndex < 0 || nextIndex >= currentFolderFiles.Count)
                return;

            string nextPath = currentFolderFiles[nextIndex];
            PlayFile(nextPath);
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

            // Load ListBox width
            int listBoxWidth = Settings.Default.InfoListBoxWidth;
            if (listBoxWidth < 500) listBoxWidth = 500; // Ensure minimum width
            mainSplitContainer.SplitterDistance = mainSplitContainer.Width - listBoxWidth;
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

            // Save ListBox width
            int listBoxWidth = mainSplitContainer.Width - mainSplitContainer.SplitterDistance;
            if (listBoxWidth < 500) listBoxWidth = 500; // Ensure minimum width
            Settings.Default.InfoListBoxWidth = listBoxWidth;

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

        private void TranscriptionListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (transcriptionListBox.SelectedIndex == -1)
                return;
            if (transcriptionListBox.Items[transcriptionListBox.SelectedIndex] is not TranscriptionLine item)
                return;
            if (item.StartTime >= 0)
            {
                _mediaHandler.SeekTo(Math.Max(0, item.StartTime - 1));
            }
        }

        private void TranscriptionListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            // Determine colors based on item state
            Color backgroundColor;
            Color textColor;

            if (e.Index == currentTranscriptionLineIndex)
            {
                backgroundColor = Color.LightBlue;
                textColor = Color.Black;
            }
            else if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                backgroundColor = Color.DarkBlue;
                textColor = Color.White;
            }
            else
            {
                backgroundColor = Color.White;
                textColor = Color.Black;
            }

            using (var brush = new SolidBrush(backgroundColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Draw right-aligned, wrapped text (RTL)
            if (e.Index < transcriptionListBox.Items.Count)
            {
                var item = transcriptionListBox.Items[e.Index] as TranscriptionLine;
                string text = item?.DisplayText ?? transcriptionListBox.Items[e.Index].ToString();
                using (var brush = new SolidBrush(textColor))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        FormatFlags = StringFormatFlags.DirectionRightToLeft,
                        Trimming = StringTrimming.None
                    };
                    Rectangle textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, e.Bounds.Width - 8, e.Bounds.Height - 6);
                    e.Graphics.DrawString(text, e.Font, brush, textRect, format);
                }
            }

            // Draw a fine delimiter line at the bottom of the item
            using (var pen = new Pen(Color.LightGray, 1))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            e.DrawFocusRectangle();
        }

        private void TranscriptionListBox_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= transcriptionListBox.Items.Count)
                return;

            string text = transcriptionListBox.Items[e.Index].ToString();
            using (Graphics g = transcriptionListBox.CreateGraphics())
            {
                // Use the same rectangle width as in DrawItem
                int width = transcriptionListBox.Width - 8;
                SizeF size = g.MeasureString(text, transcriptionListBox.Font, width, new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.DirectionRightToLeft,
                    Trimming= StringTrimming.None
                });
                e.ItemHeight = (int)Math.Ceiling(size.Height) + 6; // Add some padding
            }
        }

        private void MainSplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            // Calculate ListBox width (total width minus splitter distance)
            int listBoxWidth = mainSplitContainer.Width - mainSplitContainer.SplitterDistance;

            // Ensure minimum width of 500px
            if (listBoxWidth < 500)
            {
                mainSplitContainer.SplitterDistance = mainSplitContainer.Width - 500;
                listBoxWidth = 500;
            }

            // Save the width
            Settings.Default.InfoListBoxWidth = listBoxWidth;
            Settings.Default.Save();
        }

        private void TranscriptionListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Allow ALT+F4 to close the app
            if (e.KeyCode == Keys.F4 && e.Alt)
            {
                // Do not suppress ALT+F4
                return;
            }
            // Handle special keys if needed
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void TranscriptionListBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Ignore key presses
            e.Handled = true;
        }
    }
    internal class TranscriptionLine
    {
        public string DisplayText { get; set; }
        public double StartTime { get; set; }
        public string OriginalLine { get; set; }
        public override string ToString() => DisplayText;
    }
}
