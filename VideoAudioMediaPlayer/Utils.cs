using System.Runtime.InteropServices;

namespace VideoAudioMediaPlayer
{
    public class TimeChangedEventArgs
    {
        public double Time;

        public double Duration;

        public TimeChangedEventArgs(double time, double duration) {
            this.Time = time;
            this.Duration = duration;
        }
    }
    public class VideoPlayerKeyDownEventArgs
    {
        public string KeyCode;

        public bool ShiftKey;

        public VideoPlayerKeyDownEventArgs(string KeyCode, bool ShiftKey)
        {
            this.KeyCode = KeyCode;
            this.ShiftKey = ShiftKey;
        }
    }
}