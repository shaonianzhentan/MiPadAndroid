using System;
using System.Collections.Generic;
using System.Text;
using Android.Media;
using Android.Webkit;

namespace HA
{
    public class MediaPlayerService
    {
        WebView webView;
        public int volumeLevel { get; set; }
        public double mediaPosition { get; set; }
        public double mediaDuration { get; set; }
        public string state { get; set; }
        public MediaPlayerService(WebView webView)
        {
            this.webView = webView;
        }

        public void Load(string url)
        {
            webView.EvaluateJavascript("MediaPlayer.load('" + url + "')", null);
        }

        public void Play()
        {
            webView.EvaluateJavascript("MediaPlayer.play()", null);
            this.state = "playing";
        }

        public void Pause()
        {
            webView.EvaluateJavascript("MediaPlayer.pause()", null);
            this.state = "paused";
        }

        public void Seek(string time)
        {
            webView.EvaluateJavascript("MediaPlayer.seek(" + time + ")", null);
        }

        public void SetVolume(string volume)
        {
            webView.EvaluateJavascript("MediaPlayer.setVolume(" + volume + ")", null);
        }
    }
}
