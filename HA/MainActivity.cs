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
using System.Net.Sockets;
using Android.Webkit;
using Xamarin.Essentials;
using Android.Net.Wifi;
using YamlDotNet.Serialization;
using System.Net;
using Android.Net;
using Android.Speech;

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ISensorEventListener
    {
        AudioManager audioManager = null;
        IMqttClient mqttClient = null;
        HttpListener httpListenner = null;
        Button floatButton = null;
        string topic = $"android/{Android.OS.Build.Serial}/".ToLower();
        float LightSensor = 0;
        string debugTime = "";
        string debugMsg = "";
        string ha_api = "";
        string web_url = "https://ha.jiluxinqing.com";
        // 语音识别
        bool isStartRecord = false;
        int VOICE = 1;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            if (savedInstanceState != null)
            {
                ha_api = savedInstanceState.GetString("ha_api", "");
                web_url = savedInstanceState.GetString("web_url");
            }
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            audioManager = this.GetSystemService(Context.AudioService) as AudioManager;
            // 注册传感器
            SensorManager sensorManager = GetSystemService(Context.SensorService) as SensorManager;
            sensorManager.RegisterListener(this, sensorManager.GetDefaultSensor(SensorType.Light), SensorDelay.Fastest);

            WebView webView = this.FindViewById<WebView>(Resource.Id.wv);
            //系统默认会通过手机浏览器打开网页，为了能够直接通过WebView显示网页，则必须设置
            webView.Settings.AllowFileAccess = true;
            webView.Settings.AllowContentAccess = true;
            webView.Settings.AllowFileAccessFromFileURLs = true;
            webView.Settings.AllowUniversalAccessFromFileURLs = true;
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.SetRenderPriority(Android.Webkit.WebSettings.RenderPriority.High);
            webView.ScrollbarFadingEnabled = true;
            webView.SetWebViewClient(new PodWebViewClient());
            webView.LoadUrl(web_url);
            httpListenner = new HttpListener();
            httpListenner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            string url = $"http://{this.getIP()}:8124/";
            httpListenner.Prefixes.Add(url);
            httpListenner.Start();
            new System.Threading.Thread(new ThreadStart(delegate
            {
                try
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
                            dict.Add(key, value);
                            dict.Add("url_path", path);
                            dict.Add("update_time", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                switch (path)
                                {
                                    case "/set":
                                        if (!string.IsNullOrEmpty(value))
                                        {
                                            value = System.Net.WebUtility.UrlDecode(value);
                                        }
                                        switch (key)
                                        {
                                            case "url":
                                                web_url = value;
                                                webView.LoadUrl(value);
                                                break;
                                            case "float":
                                                FloatWindow(value == "1");
                                                break;
                                            case "mqtt":
                                                if (mqttClient == null)
                                                {
                                                    this.ConnectMQTT(value);
                                                    // 定时器（每5秒执行一次）
                                                    Timer timer = new Timer((state) =>
                                                    {
                                                        // 当前连接中，则进行上报状态
                                                        if (mqttClient.IsConnected)
                                                        {
                                                            this.PublishConfig();
                                                        }
                                                        else
                                                        {
                                                            // 客户端重连
                                                            this.ConnectMQTT(value);
                                                        }
                                                    }, null, 0, 12000);
                                                }
                                                break;
                                            case "system_volume":
                                                audioManager.SetStreamVolume(Stream.System, System.Convert.ToInt32(value), VolumeNotificationFlags.PlaySound);
                                                break;
                                            case "brightness":
                                                Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, System.Convert.ToInt32(value));
                                                break;
                                            case "tts":
                                                this.Speak(value);
                                                break;
                                            case "ha_api":
                                                ha_api = value;
                                                break;
                                            case "stt":
                                                this.StartRecord();
                                                break;
                                        }
                                        break;
                                    default:
                                        dict.Add("status code", "404");
                                        break;
                                }
                            });

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
                        catch (System.Exception ex)
                        {
                            System.Console.WriteLine(ex);
                        }

                    }
                }
                catch (Exception)
                {
                    // httpListenner.Stop();
                }
            })).Start();
            // 这里发送广播
            SendInitMessage();
            // 浮动窗口
            FloatWindow(true);
        }


        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutString("ha_api", ha_api);
            outState.PutString("web_url", web_url);
            base.OnSaveInstanceState(outState);
        }


        void SendInitMessage()
        {
            UdpClient udpClient = new UdpClient();
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("ip", this.getIP());
            dict.Add("api", $"http://{this.getIP()}:8124/");
            string data = JsonConvert.SerializeObject(dict);
            udpClient.Send(System.Text.Encoding.UTF8.GetBytes(data), data.Length, new IPEndPoint(IPAddress.Broadcast, 9234));
        }

        async void ConnectMQTT(string host, int port = 1883)
        {
            string clientId = System.Guid.NewGuid().ToString();
            // 设置主题
            string topic_set = $"{topic}set";            
            // 定义配置信息
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithCleanSession(true)
                .WithWillDelayInterval(5)
                .WithTcpServer(host, port);
            // 创建MQTT客户端
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
            // 连接成功事件
            mqttClient.UseConnectedHandler((action) =>
            {
                // 订阅设置主题
                mqttClient.SubscribeAsync(topic_set);
                // 发送连接状态
                this.PublishInfo("连接成功");
            });
            // 断开连接事件
            mqttClient.UseDisconnectedHandler((action) =>
            {
                // 连接中断了啊
            });
            // 消息接收事件
            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                // 获取当前消息主题
                string topic = e.ApplicationMessage.Topic;
                // 获取当前消息内容
                string payload = e.ApplicationMessage.Payload == null ? "" : System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                // 判断是否为设置主题
                if (topic == topic_set && !System.String.IsNullOrEmpty(payload))
                {
                    // 这里进行数据设置
                    try
                    {
                        var yamlReader = new System.IO.StringReader(payload);
                        Deserializer yamlDeserializer = new Deserializer();
                        Dictionary<string, object> dict = yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlReader);
                        // 设置TTS
                        if (dict.ContainsKey("tts"))
                        {
                            string tts = dict["tts"].ToString();
                            this.Speak(tts);
                            this.PublishInfo($"TTS：{tts}");
                        }
                        // 设置屏幕亮度
                        if (dict.ContainsKey("brightness"))
                        {
                            Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, System.Convert.ToInt32(dict["brightness"]));
                            this.PublishInfo($"屏幕亮度：{dict["brightness"]}");
                        }
                        // 设置音乐音量
                        if (dict.ContainsKey("music_volume"))
                        {
                            audioManager.SetStreamVolume(Stream.Music, System.Convert.ToInt32(dict["music_volume"]), VolumeNotificationFlags.PlaySound);
                            this.PublishInfo($"音乐音量：{dict["music_volume"]}");
                        }
                        // 设置闹钟音量
                        if (dict.ContainsKey("alarm_volume"))
                        {
                            audioManager.SetStreamVolume(Stream.Alarm, System.Convert.ToInt32(dict["alarm_volume"]), VolumeNotificationFlags.PlaySound);
                            this.PublishInfo($"闹钟音量：{dict["alarm_volume"]}");
                        }
                        // 设置系统音量
                        if (dict.ContainsKey("system_volume"))
                        {
                            audioManager.SetStreamVolume(Stream.System, System.Convert.ToInt32(dict["system_volume"]), VolumeNotificationFlags.PlaySound);
                            this.PublishInfo($"系统音量：{dict["system_volume"]}");
                        }
                    }
                    catch (Exception ex)
                    {
                        this.PublishInfo(ex.Message);
                    }
                }
            });
            // 开始连接...
            await mqttClient.ConnectAsync(options.Build(), CancellationToken.None);
        }

        /// <summary>
        /// 发送配置信息
        /// </summary>
        void PublishConfig()
        {
            string model = "android";
            string unique_id = $"{model}-xiaomipinban";
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("name", "小米平板");
            dict.Add("icon", "mdi:android");
            dict.Add("state_topic", $"{topic}state");
            dict.Add("json_attributes_topic", $"{topic}attributes");
            dict.Add("device_class", "battery");
            dict.Add("unit_of_measurement", "%");
            // 实体唯一ID
            dict.Add("unique_id", unique_id);
            // 设备信息
            dict.Add("device", new
            {
                identifiers = Android.OS.Build.Id,
                manufacturer = Android.OS.Build.Manufacturer,
                model = Android.OS.Build.Model,
                name = Android.OS.Build.Model,
                sw_version = Android.OS.Build.RadioVersion
            });
            // 上报信息
            mqttClient.PublishAsync($"homeassistant/sensor/{unique_id}/config", JsonConvert.SerializeObject(dict));

            this.PublishInfo();
        }


        void PublishInfo(string msg = "")
        {
            if (!string.IsNullOrEmpty(msg))
            {
                debugTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                debugMsg = msg;
            }
            Dictionary<string, object> dict = new Dictionary<string, object>();
            // 屏幕亮度
            int brightness = Settings.System.GetInt(this.ContentResolver, Settings.System.ScreenBrightness);            
            // 音乐音量
            int musicVolume = audioManager.GetStreamVolume(Stream.Music);
            int musicVolumeMax = audioManager.GetStreamMaxVolume(Android.Media.Stream.Music);
            // 闹钟音量
            int alarmVolume = audioManager.GetStreamVolume(Stream.Alarm);
            int alarmVolumeMax = audioManager.GetStreamMaxVolume(Android.Media.Stream.Alarm);
            // 系统音量
            int systemVolume = audioManager.GetStreamVolume(Stream.System);
            int systemVolumeMax = audioManager.GetStreamMaxVolume(Android.Media.Stream.System);
            // WiFi管理
            WifiManager wifiManager = this.GetSystemService(Context.WifiService) as WifiManager;
            WifiInfo wifiInfo = wifiManager.ConnectionInfo;
            // 磁盘容量
            Java.IO.File datapath = Android.OS.Environment.DataDirectory;
            StatFs dataFs = new StatFs(datapath.Path);
            // 电量
            string battery = (Xamarin.Essentials.Battery.ChargeLevel * 100).ToString();
            // 充电状态
            string[] batteryState = new string[] { "Unknown", "Charging", "Discharging", "Full", "NotCharging", "NotPresent" };

            dict.Add("设置主题", $"{topic}set");
            dict.Add("语音识别", $"{topic}stt");
            dict.Add("调试时间", debugTime);
            dict.Add("调试消息", debugMsg);
            
            dict.Add("屏幕亮度", brightness);
            dict.Add("光照传感器", LightSensor);
            dict.Add("电量", battery);
            dict.Add("充电状态", batteryState[(int)Xamarin.Essentials.Battery.State]);

            dict.Add("音乐音量", musicVolume);
            dict.Add("闹钟音量", alarmVolume);
            dict.Add("系统音量", systemVolume);
            dict.Add("音乐最大音量", musicVolumeMax);
            dict.Add("闹钟最大音量", alarmVolumeMax);
            dict.Add("系统最大音量", systemVolumeMax);

            dict.Add("存储总量", getUnit(dataFs.TotalBytes));
            dict.Add("存储剩余", getUnit(dataFs.AvailableBytes));
            dict.Add("存储使用", getUnit(dataFs.TotalBytes - dataFs.AvailableBytes));

            dict.Add("设备名称", Xamarin.Essentials.DeviceInfo.Name);
            dict.Add("设备型号", Xamarin.Essentials.DeviceInfo.Model);
            dict.Add("设备版本", Xamarin.Essentials.DeviceInfo.VersionString);
            dict.Add("应用版本", Xamarin.Essentials.AppInfo.VersionString);

            dict.Add("本机IP", getIP());
            dict.Add("WiFi名称", wifiInfo.SSID.Trim('"'));
            dict.Add("WiFi信号", wifiInfo.Rssi);
            dict.Add("Mac地址", wifiInfo.MacAddress);

            mqttClient.PublishAsync($"{topic}state", battery);
            mqttClient.PublishAsync($"{topic}attributes", JsonConvert.SerializeObject(dict));
        }

        void Speak(string value)
        {
            // 主线程异步调用方法
            MainThread.BeginInvokeOnMainThread(async () => {
                await Xamarin.Essentials.TextToSpeech.SpeakAsync(value);
            });
        }

        string getIP()
        {
            string ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Select(p => p.GetIPProperties())
                .SelectMany(p => p.UnicastAddresses)
                .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
                .FirstOrDefault()?.Address.ToString();
            return ip;
        }

        string getUnit(double size)
        {
            int index = 0;
            while (size > 1024 && index < 4)
            {
                size = size / 1024;
                index++;
            }
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            return string.Format("{0:N2}", size) + units[index];
        }

        #region
        void FloatWindow(bool flags)
        {
            if (floatButton != null)
            {
                if (flags)
                {
                    floatButton.Visibility = ViewStates.Visible;
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, 1);
                }
                else
                {
                    floatButton.Visibility = ViewStates.Invisible;
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, 125);
                }
                return;
            }
            if (flags)
            {
                WindowManagerLayoutParams layoutParams = new WindowManagerLayoutParams();
                // 判断当前Android系统版本
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    layoutParams.Type = WindowManagerTypes.ApplicationOverlay;
                }
                else
                {
                    layoutParams.Type = WindowManagerTypes.Application;
                }
                layoutParams.Format = Format.Rgba8888;
                layoutParams.Gravity = GravityFlags.Left | GravityFlags.Top;
                layoutParams.Flags = WindowManagerFlags.NotTouchModal | WindowManagerFlags.NotFocusable | WindowManagerFlags.Fullscreen;
                // 窗口的宽高和位置
                DisplayMetrics dm = new DisplayMetrics();
                WindowManager.DefaultDisplay.GetMetrics(dm);
                layoutParams.Width = dm.WidthPixels;
                layoutParams.Height = dm.HeightPixels;
                layoutParams.X = 0;
                layoutParams.Y = 0;
                // 生成一个按钮
                floatButton = new Button(this.ApplicationContext);
                // floatButton.Text = System.DateTime.Now.ToString("HH:mm:ss");
                floatButton.Text = "";
                floatButton.SetBackgroundColor(Color.Black);
                floatButton.SetTextColor(Color.White);
                floatButton.Visibility = ViewStates.Invisible;
                floatButton.Click += (s, e) =>
                {
                    floatButton.Visibility = ViewStates.Invisible;
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, 125);
                };
                WindowManager.AddView(floatButton, layoutParams);
            }    
        }
        #endregion

        #region 语音识别
        // 开始录音
        void StartRecord()
        {
            if (isStartRecord) return;
            isStartRecord = true;

            var voiceIntent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            voiceIntent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            voiceIntent.PutExtra(RecognizerIntent.ExtraPrompt, ha_api);
            voiceIntent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 1500);
            voiceIntent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 1500);
            voiceIntent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 15000);
            voiceIntent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
            voiceIntent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            StartActivityForResult(voiceIntent, VOICE);
        }

        protected override void OnPause()
        {
            if (httpListenner.IsListening)
            {
                httpListenner.Stop();
            }
            base.OnPause();
        }

        protected override void OnActivityResult(int requestCode, Result resultVal, Intent data)
        {
            if (requestCode == VOICE)
            {
                if (resultVal == Result.Ok)
                {
                    var matches = data.GetStringArrayListExtra(RecognizerIntent.ExtraResults);
                    if (matches.Count != 0)
                    {
                        string msg = matches[0];
                        WebClient wc = new WebClient();
                        byte[] bResponse = wc.DownloadData(ha_api + "?text=" + Uri.Encode(msg));
                        string strResponse =System.Text.Encoding.UTF8.GetString(bResponse);
                        System.Console.WriteLine(strResponse);
                    }
                    else
                    {
                        System.Console.WriteLine("No speech was recognised");
                    }
                }
                base.OnActivityResult(requestCode, resultVal, data);
            }
        }

        #endregion

        #region 传感器
        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            
        }

        public void OnSensorChanged(SensorEvent e)
        {
            // 光照传感器
            if (e.Sensor.Name.Contains("Light Sensor"))
            {
                    LightSensor = e.Values[0];
            }
        }
        #endregion

    }

    public class PodWebViewClient : WebViewClient

    {
        [System.Obsolete]
        public override bool ShouldOverrideUrlLoading(WebView view, string url)

        {
            view.LoadUrl(url);
            return true;
        }
    }
}