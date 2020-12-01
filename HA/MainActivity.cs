using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Graphics;
using Android.Media;
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
using System.Net.Sockets;
using Android.Webkit;
using Xamarin.Essentials;

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity
    {
        WebView webView = null;
        AudioManager audioManager = null;
        IMqttClient mqttClient = null;
        string ip = "";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            audioManager = this.GetSystemService(Context.AudioService) as AudioManager;

            ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                            .Select(p => p.GetIPProperties())
                            .SelectMany(p => p.UnicastAddresses)
                            .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
                            .FirstOrDefault()?.Address.ToString();
            webView = this.FindViewById<WebView>(Resource.Id.webView1);
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.AllowContentAccess = true;
            webView.Settings.AllowFileAccess = true;
            webView.Settings.AllowFileAccessFromFileURLs = true;
            webView.Settings.AllowUniversalAccessFromFileURLs = true;
            webView.Settings.MediaPlaybackRequiresUserGesture = false;
            webView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            webView.SetWebViewClient(new WebViewClientService());
            webView.LoadData(ip, "text/html", "utf-8");
            // 启动HTTP服务
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
            // 创建UDP服务
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Parse(ip), 8124));
            new System.Threading.Thread(new ThreadStart(delegate
            {
                while (true)
                {
                    // 接收数据
                    EndPoint point = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = new byte[1024 * 1024];
                    int length = server.ReceiveFrom(buffer, ref point);
                    string message = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
                    try
                    {
                        Dictionary<string, object> dict = this.ActionService(message);
                        // 发送数据
                        string responseString = JsonConvert.SerializeObject(dict);
                        server.SendTo(System.Text.Encoding.UTF8.GetBytes(responseString), point);
                    }
                    catch (Exception ex)
                    {
                        server.SendTo(System.Text.Encoding.UTF8.GetBytes("出现异常：" + ex.Message), point);
                    }
                }
            })).Start();

            this.FloatButton();
        }
        // 连接MQTT
        async void ConnectMQTT(string host, int port)
        {
            string clientId = System.Guid.NewGuid().ToString();
            string topic_name = $"android/{ip}/set";
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithCleanSession(true)
                .WithWillDelayInterval(5)
                .WithTcpServer(host, port);
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
            mqttClient.UseConnectedHandler((action) =>
            {
                // 连接成功
                mqttClient.SubscribeAsync(topic_name);
            });
            mqttClient.UseDisconnectedHandler((action) =>
            {
                // 连接中断了啊
            });
            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = e.ApplicationMessage.Payload == null ? "" : System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                if (topic_name == $"android/{ip}/set")
                {
                    mqttClient.PublishAsync($"android/{ip}/get", "");
                }
            });
            await mqttClient.ConnectAsync(options.Build(), CancellationToken.None);
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
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    if (request.HasEntityBody)
                    {
                        System.IO.StreamReader reader = new System.IO.StreamReader(request.InputStream);
                        string text = reader.ReadToEnd();
                        dict = this.ActionService(text);
                    }else if(request.HttpMethod == "GET")
                    {
                        dict.Add("update_time", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        switch (path)
                        {
                            case "/test":
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    webView.LoadUrl("https://www.baidu.com");
                                });
                                break;
                        }
                    }


                    dict.Add("path", path);
                    string responseString = JsonConvert.SerializeObject(dict);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    //对客户端输出相应信息.
                    context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
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

        public Dictionary<string, object> ActionService(string text)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("update_time", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Dictionary<string, string> body = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
            if (body.ContainsKey("app_type"))
            {
                string app_type = body["app_type"];
                string value = body.ContainsKey("value") ? body["value"] : "";
                switch (app_type)
                {
                    // 获取设备信息
                    case "device_info":
                        dict.Add("brightness", Settings.System.GetInt(this.ContentResolver, Settings.System.ScreenBrightness));
                        dict.Add("volume", audioManager.GetStreamVolume(Stream.Music));
                        // 获取电量
                        Intent intent = new ContextWrapper(this).RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
                        dict.Add("battery", intent.GetIntExtra(BatteryManager.ExtraLevel, -1) * 100 / intent.GetIntExtra(BatteryManager.ExtraScale, -1));
                        break;

                    // 设置屏幕亮度
                    case "set_screen_brightness":
                        Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, System.Convert.ToInt32(value));
                        break;

                    // 设置系统音量
                    case "set_system_volume":
                        audioManager.SetStreamVolume(Stream.Music, System.Convert.ToInt32(value), VolumeNotificationFlags.PlaySound);
                        break;
                    // 设置网页URL
                    case "set_web_url":
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            webView.LoadUrl(value);
                        });
                        break;
                    // 执行js方法
                    case "exec_js":
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            webView.EvaluateJavascript(value, null);
                        });
                        break;
                    // TTS播放
                    case "tts":
                        MainThread.BeginInvokeOnMainThread(async() => {
                            await Xamarin.Essentials.TextToSpeech.SpeakAsync(value);
                        });
                        break;
                    // 蓝牙设置
                    case "ble":

                        break;
                    // http代理
                    case "http_proxy":

                        break;
                    // MQTT
                    case "mqtt":
                        string[] arr = value.Split(":");
                        this.ConnectMQTT(arr[0], System.Convert.ToInt32(arr[1]));
                        break;
                }
            }
            return dict;
        }

        // 生成像素点
        public void FloatButton()
        {
            WindowManagerLayoutParams layoutParams = new WindowManagerLayoutParams();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                layoutParams.Type = WindowManagerTypes.ApplicationOverlay;
            }
            else
            {
                layoutParams.Type = WindowManagerTypes.SystemOverlay;
            }
            layoutParams.Format = Format.Rgba8888;
            layoutParams.Gravity = GravityFlags.Left | GravityFlags.Top;
            layoutParams.Flags = WindowManagerFlags.NotTouchModal | WindowManagerFlags.NotFocusable;
            layoutParams.Width = 200;
            layoutParams.Height = 100;
            layoutParams.X = 0;
            layoutParams.Y = 0;
            Button pixButton = new Button(this.ApplicationContext);
            pixButton.Text = System.DateTime.Now.ToString("HH:mm:ss");
            pixButton.SetBackgroundColor(Color.Argb(200, 0, 0, 0));
            pixButton.SetTextColor(Color.Red);
            WindowManager.AddView(pixButton, layoutParams);

            Timer timer = new Timer((state) => {
                this.RunOnUiThread(() =>
                {
                    pixButton.Text = System.DateTime.Now.ToString("HH:mm:ss");
                    // 如果mqtt未连接，则重连
                    if(mqttClient != null && mqttClient.IsConnected == false)
                    {
                        mqttClient.ReconnectAsync();
                    }
                });
            }, null, 0, 5000);
        }
    }

    public class WebViewClientService: WebViewClient
    {
        [System.Obsolete]
        public override bool ShouldOverrideUrlLoading(WebView view, string url)
        {
            view.LoadUrl(url);
            return true;
        }
    }
}