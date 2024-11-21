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
                double positionRatio = (double)mediaTime / mediaLength;
                int x = (int)(positionRatio * pictureBox.Width);
                g.DrawLine(Pens.Green, x, 0, x, pictureBox.Height);

                // Draw the yellow timeline if mediaLength is greater than or equal to 20
                if (mediaLength >= 20)
                {
                    int timelineY = pictureBox.Height / 2;
                    g.DrawLine(Pens.Yellow, 0, timelineY, pictureBox.Width, timelineY); // Horizontal timeline

                    double interval = mediaLength < 60 ? 10 : 30; // Time intervals
                    for (double t = interval; t <= mediaLength; t += interval)
                    {
                        double tRatio = t / mediaLength;
                        int tickX = (int)(tRatio * pictureBox.Width);

                        // Determine if it's a major or minor tick
                        bool isMajorTick = mediaLength >= 60 && ((t % 60) == 0);

                        // Draw the tick mark
                        int tickHeight = isMajorTick ? 10 : 5; // Major ticks are taller
                        g.DrawLine(Pens.Yellow, tickX, timelineY - tickHeight, tickX, timelineY + tickHeight);

                        // Draw labels only for major ticks
                        if (isMajorTick || mediaLength < 60)
                        {
                            string label = mediaLength < 60 ? $"{(int)t}s" : TimeSpan.FromSeconds(t).ToString(@"mm\:ss");
                            SizeF labelSize = g.MeasureString(label, SystemFonts.DefaultFont); // Measure label size
                            int labelX = tickX - (int)(labelSize.Width / 2); // Center label around the tick
                            int labelY = timelineY + tickHeight + 5; // Move label further down for major ticks

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
