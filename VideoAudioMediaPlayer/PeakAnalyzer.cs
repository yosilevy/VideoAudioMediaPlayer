using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VideoAudioMediaPlayer
{
    public class PeakAnalyzer
      {
        private int peakSamplesPerSecond = 4;

        public double[] AnalyzeFilePeaks(string inputFilePath)
        {
            Process ffprobe = new Process();
            ffprobe.StartInfo.FileName = Path.Combine(Application.StartupPath, "ffmpeg\\ffprobe");
            string outputFileName = Path.Combine(Application.StartupPath, "peaks.txt");
            ffprobe.StartInfo.Arguments = $"-f lavfi -i \"amovie={inputFilePath.Replace("\\", "/\\").Replace(":", "\\\\:")},asetnsamples=n={(int)(16000 / peakSamplesPerSecond)},astats=metadata=1:reset=1\" -show_entries frame=pkt_pts_time:frame_tags=lavfi.astats.Overall.RMS_level -of csv=p=0 -o {outputFileName}";
            ffprobe.StartInfo.UseShellExecute = false;
            ffprobe.StartInfo.RedirectStandardOutput = true;
            ffprobe.StartInfo.RedirectStandardError = true;
            ffprobe.StartInfo.CreateNoWindow = true;
            ffprobe.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            ffprobe.OutputDataReceived += Ffmpeg_OutputDataReceived;
            ffprobe.ErrorDataReceived += Ffmpeg_ErrorDataReceived;
            ffprobe.Start();
            ffprobe.BeginOutputReadLine();
            ffprobe.BeginErrorReadLine();
            ffprobe.WaitForExit();

            double[] levels = File.ReadAllLines(outputFileName).Select(line => double.Parse(line)).ToArray();
            if(levels.Length > 0)
            {
                double maxVal = levels.Max();
                double minVal = levels.Min();
                double[] normalizedLevels = levels.Select(x => (x - minVal) / (maxVal - minVal)).ToArray();

                return normalizedLevels
                    .Select((value, index) => new { Value = value, Index = index })
                    .Where(x => x.Index > 0 && (x.Value - normalizedLevels[x.Index - 1]) > 0.25)
                    .Select(x => (x.Index * 1000 * (1.0 / peakSamplesPerSecond))) // Convert index to time in ms
                    .ToArray()
                    .Compress(400);
            }
            else
            {
                return new double[0];
            }
        }

        private void Ffmpeg_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Handle error data
        }

        private void Ffmpeg_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Handle output data
        }
    }
}
