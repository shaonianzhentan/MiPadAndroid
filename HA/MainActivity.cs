using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Telephony;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Linq;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System.Threading;
using System.Threading.Tasks;
using static Android.OS.PowerManager;
using System.Collections.Generic;
using Android.Support.Design.Widget;
using Java.Sql;
using Newtonsoft.Json;
using Android.Text.Method;
using Java.Lang;
using Android.Hardware;
using Android.Graphics.Drawables;
using System.Net;
using Android.Webkit;

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private Handler handler = null;
        MediaPlayerService mps = null;
        AudioManager audioManager = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            handler = new Handler();
            audioManager = this.GetSystemService(Context.AudioService) as AudioManager;

            string ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                            .Select(p => p.GetIPProperties())
                            .SelectMany(p => p.UnicastAddresses)
                            .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
                            .FirstOrDefault()?.Address.ToString();

            // 本机IP
            TextInputEditText txtIP = this.FindViewById<TextInputEditText>(Resource.Id.txtIP);
            txtIP.Text = ip;
            // 远程IP
            TextInputEditText txtRemoteIP = this.FindViewById<TextInputEditText>(Resource.Id.textRemoteip);
            txtRemoteIP.Text = "192.168.1.101";

            WebView webView = this.FindViewById<WebView>(Resource.Id.webView1);
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.AllowContentAccess = true;
            webView.Settings.AllowFileAccess = true;
            webView.Settings.AllowFileAccessFromFileURLs = true;
            webView.Settings.MediaPlaybackRequiresUserGesture = false;
            webView.Settings.JavaScriptCanOpenWindowsAutomatically = true;

            Button button = this.FindViewById<Button>(Resource.Id.button1);
            button.Click += (ss, ee) =>
            {
                
                webView.LoadUrl("http://"+ txtRemoteIP.Text.Trim() + ":8123/ha_cloud_music-web/android.html?ip=" + ip + "&v=" + System.DateTime.Now.ToString("yyyyMMddHHmmss"));
                if (mps == null)
                {
                    mps = new MediaPlayerService(webView);

                    HttpListener httpListenner;
                    httpListenner = new HttpListener();
                    httpListenner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                    httpListenner.Prefixes.Add($"http://{ip}:8124/");
                    httpListenner.Start();

                    new System.Threading.Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            loop(httpListenner);
                        }
                        catch (Exception)
                        {
                            httpListenner.Stop();
                        }
                    })).Start();
                }           
            };
        }

        public void loop(HttpListener httpListenner)
        {
            while (true)
            {
                try
                {
                    HttpListenerContext context = httpListenner.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    string path = request.Url.LocalPath;
                    string key = request.QueryString["key"];
                    string value = request.QueryString["value"];
                    if (request.HttpMethod == "GET")
                    {
                    }
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    dict.Add("path", path);
                    dict.Add("update_time", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    switch (path)
                    {
                        case "/get":
                            switch (key)
                            {
                                case "info": // 设备信息
                                    dict.Add("brightness", Settings.System.GetInt(this.ContentResolver, Settings.System.ScreenBrightness));
                                    dict.Add("volume", audioManager.GetStreamVolume(Stream.Music));
                                    // 获取电量
                                    Intent intent = new ContextWrapper(this).RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
                                    dict.Add("battery", intent.GetIntExtra(BatteryManager.ExtraLevel, -1) * 100 / intent.GetIntExtra(BatteryManager.ExtraScale, -1));
                                    break;
                                case "music": // 音乐信息
                                    dict.Add("volume_level", mps.volumeLevel);
                                    dict.Add("media_position", mps.mediaPosition);
                                    dict.Add("media_duration", mps.mediaDuration);
                                    dict.Add("state", mps.state);
                                    break;
                            }
                            break;
                        case "/set":
                            if (!string.IsNullOrEmpty(value))
                            {
                                value = System.Net.WebUtility.UrlDecode(value);
                            }

                            handler.Post(() =>
                            {
                                switch (key)
                                {
                                    case "brightness": // 屏幕亮度
                                        Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, System.Convert.ToInt32(value));
                                        break;
                                    case "volume": // 音乐声音
                                        audioManager.SetStreamVolume(Stream.Music, System.Convert.ToInt32(value), VolumeNotificationFlags.PlaySound);
                                        break;
                                    case "music_url": // 播放音乐
                                        mps.Load(value);
                                        break;
                                    case "music_set": // 设置音乐信息
                                        string[] str = value.Split(",");
                                        mps.volumeLevel = System.Convert.ToInt32(str[0]);
                                        mps.mediaPosition = System.Convert.ToSingle(str[1]);
                                        mps.mediaDuration = System.Convert.ToSingle(str[2]);
                                        mps.state = str[3];
                                        break;
                                    case "music_reset":
                                        mps.Pause();
                                        mps.mediaPosition = 0;
                                        mps.mediaDuration = 0;
                                        mps.state = "idle";
                                        break;
                                    case "music_play":
                                        mps.Play();
                                        break;
                                    case "music_pause":
                                        mps.Pause();
                                        break;
                                    case "music_seek":
                                        mps.Seek(value);
                                        break;
                                    case "msuic_set_volume":
                                        mps.SetVolume(value);
                                        break;
                                }
                            });

                            
                            break;
                        default:
                            dict.Add("status code", "404");
                            break;
                    }

                    string responseString = JsonConvert.SerializeObject(dict);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    //对客户端输出相应信息.
                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    //关闭输出流，释放相应资源
                    output.Close();
                }
                catch(System.Exception ex)
                {
                    System.Console.WriteLine(ex);
                }
                
            }
        }
    }
}