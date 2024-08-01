using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace VideoAudioMediaPlayer
{
    public class MediaHandler
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private long seekStep = 8 * 1000;
        private long targetSeekTime;
        private int targetSeekDirection;
        private long seekFromTime;
        long maxAudioPos = 0;
        long minAudioPos = long.MaxValue;
        long maxAudioLength = long.MaxValue;

        public long Length => _mediaPlayer.Length;
        public long Time => _mediaPlayer.Time;
        public bool IsPlaying => _mediaPlayer.IsPlaying;

        public event EventHandler Playing;
        public event EventHandler<MediaPlayerTimeChangedEventArgs> TimeChanged;

        public MediaHandler(VideoView videoView)
        {
            _videoView = videoView;
            InitializeLibVLC();
            InitializeMediaPlayer();
        }

        private void InitializeLibVLC()
        {
            _libVLC = new LibVLC("--input-repeat=2");
        }

        private void InitializeMediaPlayer()
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.TimeChanged += (sender, e) => TimeChanged?.Invoke(sender, e);
            _mediaPlayer.Playing += (sender, e) => Playing?.Invoke(sender, e);
            _videoView.MediaPlayer = _mediaPlayer;
        }

        public void Play(string file)
        {
            _mediaPlayer.Play(new Media(_libVLC, file, FromType.FromPath));
        }

        public void PlayPause()
        {
            _mediaPlayer.Pause();
        }
        public void UnloadMedia()
        {
            if (_mediaPlayer.IsPlaying)
                _mediaPlayer.Pause();

            _mediaPlayer.Media = null;

            maxAudioLength = 0;
            maxAudioPos = 0;
            minAudioPos = long.MaxValue;
        }


        public void SeekForwardStep(long mediaTime)
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

        public void SeekBackwardStep(long mediaTime)
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
        public void SeekForwardTo(long targetTime)
        {
            SeekTo(targetTime, 1);
        }

        public void SeekBackwardTo(long targetTime)
        {
            SeekTo(targetTime, -1);
        }

        public void SeekTo(long targetTime, int direction = 0)
        {
            targetSeekDirection = 0;
            targetSeekTime = targetTime;
            targetSeekDirection = direction;
            seekFromTime = _mediaPlayer.Time;

            ThreadPool.QueueUserWorkItem(_ => { _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(targetTime)); });
        }

        public void CreateLibVLCWithOptions(params string[] options)
        {
            if (_libVLC != null)
            {
                _videoView.MediaPlayer = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
                _libVLC.Dispose();
                _libVLC = null;
            }

            _libVLC = new LibVLC(options);
            InitializeMediaPlayer();
        }

        public bool HandleMovement(long time)
        {
            // right
            // if loop finished - reset max
            if (Math.Abs(maxAudioPos - maxAudioLength) < 200 || time == 0)
                maxAudioPos = 0;

            // if moving forward - make sure we don't move backwards
            if (targetSeekDirection > 0 && time < maxAudioPos)
                return false;

            // save max visited time
            if (maxAudioPos < time)
                maxAudioPos = time;

            // if gone left - track minimum
            if (targetSeekDirection < 0)
            {
                // reset the other direction's max
                maxAudioPos = 0;
                if (time < minAudioPos)
                    minAudioPos = time;
            }
            else
                // reset the other direction's min
                minAudioPos = long.MaxValue;

            // left - if moved too far off the minimum - not good - ignore
            if (targetSeekDirection < 0 && time > minAudioPos + (((seekFromTime - targetSeekTime) / 2)))
                return false;

            // if moved further than min but not too much - stand down and don't validate any more
            if (targetSeekDirection < 0 && (time > minAudioPos + ((seekFromTime - targetSeekTime) / 4)))
                targetSeekDirection = 0; // might need to reset min and max ???

            return true;
        }

        internal void SetupPlayerInputEvents()
        {
            _mediaPlayer.EnableMouseInput = false;
            _mediaPlayer.EnableKeyInput = false;
        }

        internal void SampleAudioLength()
        {
            maxAudioLength = _mediaPlayer.Length;
        }
    }
}
