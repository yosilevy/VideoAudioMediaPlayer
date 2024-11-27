using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace VideoAudioMediaPlayer
{
    public class WaveformHandler
    {
        private Bitmap waveformImage;
        private long maxAudioPos = 0;
        private long minAudioPos = long.MaxValue;
        private long maxAudioLength = long.MaxValue;
        private long targetSeekTime = 0;
        private int targetSeekDirection = 1;
        private long seekFromTime;

        public void GenerateWaveform(string inputFilePath, string outputFilePath, int width, int height)
        {
            Process ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = Path.Combine(Application.StartupPath, "ffmpeg\\ffmpeg");
            ffmpeg.StartInfo.Arguments = $"-i \"{inputFilePath}\" -filter_complex \"compand=gain=6,showwavespic=s={width}x{height}\" -frames:v 1 \"{outputFilePath}\" -y";
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardOutput = true;
            ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.StartInfo.CreateNoWindow = true;
            ffmpeg.OutputDataReceived += Ffmpeg_OutputDataReceived;
            ffmpeg.ErrorDataReceived += Ffmpeg_ErrorDataReceived;
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.WaitForExit();
        }

        public void LoadWaveform(string filePath, PictureBox pictureBox)
        {
            try
            {
                using (var tempImage = new Bitmap(filePath))
                {
                    waveformImage = new Bitmap(tempImage);
                    pictureBox.Image = waveformImage;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading waveform image: {ex.Message}");
            }
        }

        public void DrawWaveformWithPosition(double mediaTime, PictureBox pictureBox, double mediaLength)
        {
            Bitmap tempImage = new Bitmap(pictureBox.Width, pictureBox.Height);
            using (Graphics g = Graphics.FromImage(tempImage))
            {
                // Draw the waveform image or default line
                if (waveformImage != null)
                    g.DrawImage(waveformImage, new Rectangle(0, 0, pictureBox.Width, pictureBox.Height));
                else
                    g.DrawLine(Pens.White, new Point(0, pictureBox.Height / 2), new Point(pictureBox.Width, pictureBox.Height / 2));

                // Draw the green position indicator line
                double positionRatio = mediaTime / mediaLength;
                int x = (int)(positionRatio * pictureBox.Width);
                g.DrawLine(Pens.Green, x, 0, x, pictureBox.Height);

                // Draw the yellow timeline if mediaLength is greater than or equal to 20
                if (mediaLength >= 20)
                {
                    int timelineY = pictureBox.Height / 2;
                    g.DrawLine(Pens.Yellow, 0, timelineY, pictureBox.Width, timelineY); // Horizontal timeline

                    // Calculate maximum number of ticks that fit without overlapping labels
                    SizeF maxLabelSize = g.MeasureString(TimeSpan.FromSeconds(mediaLength).ToString(@"mm\:ss"), SystemFonts.DefaultFont);
                    int minSpacing = (int)(maxLabelSize.Width * 1.2); // Minimum spacing (label width + 20%)
                    int maxTicks = pictureBox.Width / minSpacing;

                    // Ensure at least 2 ticks (start and end)
                    maxTicks = Math.Max(2, maxTicks);

                    // Calculate the time interval between ticks
                    double interval = mediaLength / (maxTicks - 1);

                    for (int i = 0; i < maxTicks; i++)
                    {
                        double t = i * interval; // Current time for this tick
                        int tickX = (int)((t / mediaLength) * pictureBox.Width);

                        // Draw the tick mark
                        g.DrawLine(Pens.Yellow, tickX, timelineY - 5, tickX, timelineY + 5);

                        // Draw the label
                        string label = TimeSpan.FromSeconds(t).ToString(@"mm\:ss");
                        SizeF labelSize = g.MeasureString(label, SystemFonts.DefaultFont);

                        if (i == 0) // First label (aligned to start and above the timeline)
                        {
                            int labelX = 0; // Align to the left
                            int labelY = waveformImage != null ? 5 : timelineY - (int)labelSize.Height - 5; // Above the timeline
                            g.DrawString(label, SystemFonts.DefaultFont, Brushes.Yellow, labelX, labelY);
                        }
                        else if (i == maxTicks - 1) // Last label (aligned to end and above the timeline)
                        {
                            int labelX = pictureBox.Width - (int)labelSize.Width; // Align to the right
                            int labelY = waveformImage != null ? 5 : timelineY - (int)labelSize.Height - 5; // Above the timeline
                            g.DrawString(label, SystemFonts.DefaultFont, Brushes.Yellow, labelX, labelY);
                        }
                        else // Intermediate labels (centered and below the timeline)
                        {
                            int labelX = tickX - (int)(labelSize.Width / 2); // Center label under the tick
                            int labelY = waveformImage != null ? pictureBox.Height - (int)labelSize.Height - 5 : timelineY + 8; // Below the timeline
                            g.DrawString(label, SystemFonts.DefaultFont, Brushes.Yellow, labelX, labelY);
                        }
                    }
                }
            }

            pictureBox.Image = tempImage;
        }

        public void UpdateWaveFormWithPeaks(double[] peaks, PictureBox pictureBox, double mediaLength)
        {
            using (Graphics g = Graphics.FromImage(waveformImage))
            {
                foreach (var peak in peaks)
                {
                    double positionRatio = peak / mediaLength;
                    int x = (int)(positionRatio * pictureBox.Image.Width);
                    g.DrawRectangle(Pens.Blue, x, (int)(pictureBox.Image.Height * 0.3), 1, (int)(pictureBox.Image.Height * 0.4));
                }
            }
        }

        private void Ffmpeg_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Handle error data
            File.AppendAllText(Path.Combine(Application.StartupPath, "ffmpeg\\error.txt"), e.Data);

        }

        private void Ffmpeg_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Handle output data
            File.AppendAllText(Path.Combine(Application.StartupPath, "ffmpeg\\output.txt"), e.Data);
        }
    }
}
