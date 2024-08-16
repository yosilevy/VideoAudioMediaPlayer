var videoPlayer;
var progressUpdateFrequency;
var progressUpdater;
var duration;

// init variable and events
function initVideoHandler(reqProgressUpdateFrequency) {
    videoPlayer = document.getElementById('videoPlayer');

    if (!reqProgressUpdateFrequency) reqProgressUpdateFrequency = 200;

    progressUpdateFrequency = reqProgressUpdateFrequency;

    postMessageToHost("duration", { duration: videoPlayer.duration });

    document.body.addEventListener('keydown', function (e) {
        postMessageToHost("keyDown", { keyCode: e.code, shiftKey: e.shiftKey });
    });

    // can't autoplay video so play it now
    playVideo();
}

// plays video and updates hosts on progress
function playVideo() {
    videoPlayer.play();
    progressUpdater = setInterval(updateCurrentTime, progressUpdateFrequency);
}

// pauses video playback and stops progress updates
function pauseVideo() {
    videoPlayer.pause();
    clearInterval(progressUpdater);
}

// toggles play/pause
function playPauseVideo() {
    if (videoPlayer.paused)
        playVideo();
    else
        pauseVideo();
}

// seek to a specific time
function seekToTime(time) {
    videoPlayer.currentTime = time;
}

// notifies host about playback progress
function updateCurrentTime() {
    var currentTime = videoPlayer.currentTime;
    var duration = videoPlayer.duration;
    postMessageToHost("currentTime", { currentTime: currentTime, duration: duration });
}

// sets volume gain
function setGain(gainLevel) {
    // create an audio context and hook up the video element as the source
    var audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    var source = audioCtx.createMediaElementSource(videoPlayer);

    // create a gain node
    var gainNode = audioCtx.createGain();
    gainNode.gain.value = gainLevel; // double the volume
    source.connect(gainNode);

    // connect the gain node to an output destination
    gainNode.connect(audioCtx.destination);
}

// posts a message to the host
function postMessageToHost(eventType, data) {
    window.chrome.webview.postMessage({ eventType: eventType, data: data });
}