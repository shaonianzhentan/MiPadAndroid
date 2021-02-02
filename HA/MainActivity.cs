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

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ISensorEventListener
    {
        AudioManager audioManager = null;
        IMqttClient mqttClient = null;
        string topic = $"android/{Android.OS.Build.Serial}/".ToLower();
        bool isStartRecord = false;
        float LightSensor = 0;
        string debugTime = "";
        string debugMsg = "";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            // SetContentView(Resource.Layout.activity_main);
            audioManager = this.GetSystemService(Context.AudioService) as AudioManager;
            // 注册传感器
            SensorManager sensorManager = GetSystemService(Context.SensorService) as SensorManager;
            sensorManager.RegisterListener(this, sensorManager.GetDefaultSensor(SensorType.Light), SensorDelay.Fastest);

            LinearLayout layout = new LinearLayout(this);
            layout.Orientation = Android.Widget.Orientation.Vertical;
            SetContentView(layout);
            EditText editText = new EditText(this);
            Button bt2 = new Button(this);
            editText.Text = getIP();
            editText.InputType = Android.Text.InputTypes.ClassNumber;
            editText.SetSelection(editText.Text.Length);
            bt2.Text = "连接MQTT";
            bt2.Click += (s, ev) =>
            {
                this.ConnectMQTT(editText.Text.Trim());
                bt2.Enabled = false;
                bt2.Text = "连接成功，如果失败请退出重试";
            };
            layout.AddView(editText);
            layout.AddView(bt2);
            SetContentView(layout);

            // 定时器（每5秒执行一次）
            Timer timer = new Timer((state) =>
            {
                // 在UI线程运行
                this.RunOnUiThread(() =>
                {
                    // 判断客户端是否启动
                    if (mqttClient != null)
                    {
                        // 当前连接中，则进行上报状态
                        if (mqttClient.IsConnected)
                        {
                            this.PublishConfig();
                        }
                        else
                        {
                            // 客户端重连
                            this.ConnectMQTT(editText.Text.Trim());
                        }
                    }
                });
            }, null, 0, 12000);
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
                        // 语音识别
                        if (dict.ContainsKey("voice"))
                        {
                            // 提示方式
                            string tips = dict.ContainsKey("tips") ? dict["tips"].ToString() : "";
                            // 录音时长
                            int voiceTime = 5;
                            if (dict.ContainsKey("voice_time"))
                            {
                                voiceTime = System.Convert.ToInt32(dict["voice_time"]);
                                if (voiceTime > 5) voiceTime = 5;
                                if (voiceTime < 2) voiceTime = 2;
                            }
                            this.StartRecord(tips, dict["voice"].ToString(), voiceTime);
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

        #region 语音识别
        // 开始录音
        void StartRecord(string tips, string url, int voiceTime)
        {
            if (isStartRecord) return;
            isStartRecord = true;
            
            if (tips == "play")
            {
                Ringtone rt = RingtoneManager.GetRingtone(this, RingtoneManager.GetDefaultUri(RingtoneType.Notification));
                rt.Play();
            }
            else
            {
                // 震动一下
                Xamarin.Essentials.Vibration.Vibrate(500);
            }
            try
            {
                int bufferSizeInBytes = AudioRecord.GetMinBufferSize(16000, ChannelIn.Mono, Encoding.Pcm16bit);
                AudioRecord audioRecord = new AudioRecord(AudioSource.Mic, 16000, ChannelIn.Mono, Encoding.Pcm16bit, bufferSizeInBytes);
                // 开始录音
                audioRecord.StartRecording();
                int readsize = 0;
                byte[] audiodata = new byte[bufferSizeInBytes];
                // 创建pcm文件
                Java.IO.File audioFile = Java.IO.File.CreateTempFile("record_", ".pcm");
                string audioFilePath = audioFile.AbsolutePath;
                Java.IO.FileOutputStream fos = new Java.IO.FileOutputStream(audioFilePath);
                // 当前时间
                System.DateTime today = System.DateTime.Now;
                // 录音5秒
                while (System.DateTime.Now.Subtract(today).TotalSeconds < voiceTime)
                {
                    readsize = audioRecord.Read(audiodata, 0, bufferSizeInBytes);
                    if (-3 != readsize)
                    {
                        try
                        {
                            fos.Write(audiodata);
                        }
                        catch (Exception ioEx)
                        {
                            // log(ioEx.Message);
                        }
                    }
                }
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Xamarin.Essentials.Vibration.Vibrate(200);
                    // 将文件上传到指定地址
                    string res = HttpHelper.HttpUploadFile(url, audioFilePath, null);
                    this.PublishInfo(res);
                });                
            }
            catch (Exception ex)
            {
                this.PublishInfo(ex.Message);
            }
            finally
            {
                isStartRecord = false;
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
}