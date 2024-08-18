using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace VideoAudioMediaPlayer
{
    public class MediaHandler
    {
        WebView2 _videoView;
        string[] options = new string[]
        {
            "--allow-file-access-from-files",
            "--autoplay-policy=no-user-gesture-required",
            //"--enable-hardware-accelerated-video-decode",
            //"--disable-gpu-vsync",
            //"--ignore-gpu-blocklist",
            //"--disable-software-rasterizer",
            //"--enable-features=VaapiVideoDecoder",
        };

        private double videoDuration = 0;
        private double videoTime = 0;
        private double seekStep = 8; // seconds
        private double targetSeekTime;
        private int targetSeekDirection;
        private double seekFromTime;
        double maxAudioPos = 0;
        double minAudioPos = double.MaxValue;

        public double Length
        {
            get
            {
                return videoDuration;
            }
        }

        public double Time
        {
            get
            {
                return videoTime;
            }
        }

        public event EventHandler<TimeChangedEventArgs> TimeChanged;

        public event EventHandler<VideoPlayerKeyDownEventArgs> VideoPlayerKeyDown;

        public event EventHandler<EventArgs> DurationKnown;

        public MediaHandler(WebView2 videoView)
        {
            _videoView = videoView;
        }

        public async Task InitializeWebViewAsync()
        {
            _videoView.WebMessageReceived += VideoWebView_WebMessageReceived;
            var environment = await CoreWebView2Environment.CreateAsync(null, null,
                new CoreWebView2EnvironmentOptions(String.Join(" ", options)));

            _videoView.NavigationCompleted += _videoView_NavigationCompleted;

            await _videoView.EnsureCoreWebView2Async(environment);
        }

        // handles updates from the video player in the html
        private void VideoWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var messageText = e.WebMessageAsJson;
            var message = System.Text.Json.JsonDocument.Parse(messageText);
            switch (message.RootElement.GetProperty("eventType").GetString())
            {
                case "currentTime":
                    {
                        var payload = System.Text.Json.JsonDocument.Parse(message.RootElement.GetProperty("data").ToString());

                        double currentTime = payload.RootElement.GetProperty("currentTime").GetDouble();
                        double duration = payload.RootElement.GetProperty("duration").GetDouble();

                        this.videoTime = currentTime;
                        
                        //todo - maybe don't need event and don't need duration.

                        // raise event 
                        TimeChanged?.Invoke(sender, new TimeChangedEventArgs(currentTime, duration));
                    }
                    break;

                case "keyDown":
                    {
                        var payload = System.Text.Json.JsonDocument.Parse(message.RootElement.GetProperty("data").ToString());

                        string keyCode = payload.RootElement.GetProperty("keyCode").GetString();
                        bool shiftKey = payload.RootElement.GetProperty("shiftKey").GetBoolean();

                        // raise event
                        VideoPlayerKeyDown?.Invoke(sender, new VideoPlayerKeyDownEventArgs(keyCode, shiftKey));
                    }

                    break;

                case "duration":
                    {
                        var payload = System.Text.Json.JsonDocument.Parse(message.RootElement.GetProperty("data").ToString());

                        this.videoDuration = payload.RootElement.GetProperty("duration").GetDouble();

                        // raise event
                        DurationKnown?.Invoke(sender, new EventArgs());
                    }

                    break;
            }

        }

        string lastLoadedUri;

        public void Load(string fileName)
        {
            #region "Old method"
            //string html = $@"
            //<html>
            //    <script src='http://Resources/VideoPlayer.js'></script>
            //    <body onload='initVideoHandler(1000)'>
            //        <video id='videoPlayer' loop src='http://Source/{Path.GetFileName(fileName)}' type='video/mp4'
            //            width='100%' height='100%'></video>
            //    </body>
            //</html>";
            //_videoView.CoreWebView2.SetVirtualHostNameToFolderMapping("Source", Path.GetDirectoryName(fileName), CoreWebView2HostResourceAccessKind.Allow);
            //_videoView.CoreWebView2.SetVirtualHostNameToFolderMapping("Resources", Path.Combine(Application.StartupPath, "Web"), CoreWebView2HostResourceAccessKind.Allow);
            //_videoView.NavigateToString(html);

            // with pause
            //_videoView.NavigationCompleted += (sender, e) => { _videoView.CoreWebView2.ExecuteScriptAsync($"setTimeout(\"initVideoHandler('{new Uri(fileName).AbsoluteUri}', 300)\", 3000)"); };
            #endregion

            // save the file to be loaded upon navigation
            lastLoadedUri = new Uri(fileName).AbsoluteUri;

            // navigate to the HTML host - once navigation completes script will be run to load the file
            _videoView.CoreWebView2.Navigate(new Uri(Path.Combine(Application.StartupPath, "Web\\MediaHandler.html")).AbsoluteUri);

            // from unloadmedia
            maxAudioPos = 0;
            minAudioPos = double.MaxValue;
        }

        private void _videoView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // video host loaded - load the last requested file
            _videoView.CoreWebView2.ExecuteScriptAsync($"initVideoHandler('{lastLoadedUri}')");
        }

        public void Play()
        {
            _videoView.CoreWebView2.ExecuteScriptAsync("playVideo()");
        }

        public void PlayPause()
        {
            _videoView.CoreWebView2.ExecuteScriptAsync("playPauseVideo()");
        }

        public void SeekForwardStep(double mediaTime)
        {
            if (mediaTime < videoDuration - seekStep)
            {
                double targetTime = mediaTime + seekStep;
                targetSeekDirection = 0;
                targetSeekTime = targetTime;
                targetSeekDirection = 1;
                seekFromTime = mediaTime;

                _videoView.CoreWebView2.ExecuteScriptAsync($"seekToTime({targetTime})");
            }
        }

        public void SeekBackwardStep(double mediaTime)
        {
            if (mediaTime > seekStep)
            {
                double targetTime = mediaTime - seekStep;
                targetSeekDirection = 0;
                targetSeekTime = targetTime;
                targetSeekDirection = -1;
                seekFromTime = mediaTime;

                _videoView.CoreWebView2.ExecuteScriptAsync($"seekToTime({targetTime})");
            }
        }
        public void SeekForwardTo(double targetTime)
        {
            SeekTo(targetTime, 1);
        }

        public void SeekBackwardTo(double targetTime)
        {
            SeekTo(targetTime, -1);
        }

        public void SeekTo(double targetTime, int direction = 0)
        {
            targetSeekDirection = 0;
            targetSeekTime = targetTime;
            targetSeekDirection = direction;
            seekFromTime = videoTime;

            _videoView.CoreWebView2.ExecuteScriptAsync($"seekToTime({targetTime})");
        }

        public bool HandleMovement(double time)
        {
            // right
            // if loop finished - reset max
            if (Math.Abs(maxAudioPos - videoDuration) < 200 || time == 0)
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
                minAudioPos = double.MaxValue;

            // left - if moved too far off the minimum - not good - ignore
            if (targetSeekDirection < 0 && time > minAudioPos + (((seekFromTime - targetSeekTime) / 2)))
                return false;

            // if moved further than min but not too much - stand down and don't validate any more
            if (targetSeekDirection < 0 && (time > minAudioPos + ((seekFromTime - targetSeekTime) / 4)))
                targetSeekDirection = 0; // might need to reset min and max ???

            return true;
        }

        // set gain (1 = no gain, <1 - lower volume, >1 - increase volume
        public void SetGain(double gain)
        {
            _videoView.CoreWebView2.ExecuteScriptAsync("playPauseVideo()");
        }
    }
}