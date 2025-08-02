var videoPlayer;
var progressUpdateFrequency;
var progressUpdater;
var duration;
var timeDisplay;

// init variable and events
function initVideoHandler(videoFile, reqProgressUpdateFrequency) {
    videoPlayer = document.getElementById('videoPlayer');
    timeDisplay = document.getElementById('timeDisplay');

    // when video loaded - report duration
    videoPlayer.addEventListener('canplaythrough', function (e) {
        postMessageToHost("duration", { duration: videoPlayer.duration })
        }, { once: true });

    document.body.addEventListener('keydown', function (e) {
        postMessageToHost("keyDown", { key: e.key, shiftKey: e.shiftKey, ctrlKey: e.ctrlKey });
    });
    document.body.addEventListener('click', function (e) {
        // simulate a keydown to space
        postMessageToHost("keyDown", { key: " ", shiftKey: false, ctrlKey: false });
    });

    if (reqProgressUpdateFrequency == null || reqProgressUpdateFrequency == 0)
        progressUpdateFrequency = 100;
    else
        progressUpdateFrequency = reqProgressUpdateFrequency;

    // load video
    videoPlayer.src = videoFile;

    // can't autoplay video so play it now
    playVideo();
}

// plays video and updates hosts on progress
function playVideo() {
    videoPlayer.play();

    progressUpdater = setInterval(updateCurrentTime, progressUpdateFrequency);
}

function onVideoReady() {
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

    var dateObj = new Date(currentTime * 1000);
    hours = dateObj.getUTCHours();
    minutes = dateObj.getUTCMinutes();
    seconds = dateObj.getSeconds();
    timeDisplay.innerText = hours.toString().padStart(2, '0') + ':' +
        minutes.toString().padStart(2, '0') + ':' +
        seconds.toString().padStart(2, '0');
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

function showHelp() {
    pauseVideo();

    helpText = "Play/Pause - Space/Enter\n" +
        "Right/Left - seek forward/back\n" +
        "Shift + Right/Left - seek to next audio peak\n";
    alert(helpText);
}