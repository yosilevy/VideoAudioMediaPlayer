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
        public string Key;

        public bool ShiftKey;

        public bool CtrlKey;

        public VideoPlayerKeyDownEventArgs(string key, bool shiftKey, bool ctrlKey)
        {
            this.Key = key;
            this.ShiftKey = shiftKey;
            this.CtrlKey = ctrlKey;
        }
    }
}