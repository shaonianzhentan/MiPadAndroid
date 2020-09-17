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
using System.IO;
using Newtonsoft.Json;
using Android.Text.Method;
using Java.Lang;
using Android.Hardware;
using Android.Graphics.Drawables;

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ISensorEventListener
    {
        private Handler handler = null;
        DeviceInfo deviceInfo = null;
        bool isStart = false;
        TextInputEditText txtLog;
        TextInputEditText txtIP;
        TextInputEditText txtPort;
        TextInputEditText txtUser;
        TextInputEditText txtPassword;
        // 配置文件
        string configFile;
        // 日志行数
        int logLine = 0;
        // MQTT
        MqttHA mqttHA = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            handler = new Handler();

            configFile = FilesDir + "/ha_config.json";

            txtLog = this.FindViewById<TextInputEditText>(Resource.Id.txtLog);
            txtIP = this.FindViewById<TextInputEditText>(Resource.Id.txtIP);
            txtPort = this.FindViewById<TextInputEditText>(Resource.Id.txtPort);
            txtUser = this.FindViewById<TextInputEditText>(Resource.Id.txtUser);
            txtPassword = this.FindViewById<TextInputEditText>(Resource.Id.txtPassword);

            Button button = this.FindViewById<Button>(Resource.Id.button1);
            button.Click += Button_Click;

            if(Build.VERSION.SdkInt >=  BuildVersionCodes.M)
            {
                // 浮到最上层
                if (!Settings.CanDrawOverlays(this))
                {
                    StartActivityForResult(new Intent(Settings.ActionManageOverlayPermission, Uri.Parse("package:" + PackageName)), 0);
                }
                // 查看系统信息
                if (!Settings.System.CanWrite(this))
                {
                    StartActivityForResult(new Intent(Settings.ActionManageWriteSettings, Uri.Parse("package:" + PackageName)), 0);
                }

                // 申请权限
                if (this.CheckSelfPermission("android.permission.WRITE_EXTERNAL_STORAGE") != Android.Content.PM.Permission.Granted)
                {
                    this.RequestPermissions(new string[] { "android.permission.READ_EXTERNAL_STORAGE", "android.permission.WRITE_EXTERNAL_STORAGE" }, 1);
                }
                if (this.CheckSelfPermission("android.permission.READ_PHONE_STATE") != Android.Content.PM.Permission.Granted)
                {
                    this.RequestPermissions(new string[] { "android.permission.READ_PHONE_STATE" }, 1);
                }
            }
            else
            {
                
            }

            // 读取配置
            if (File.Exists(configFile))
            {
                try
                {
                    string haConfig = File.ReadAllText(configFile);
                    var dict = JsonConvert.DeserializeObject(haConfig) as Newtonsoft.Json.Linq.JObject;
                    txtIP.Text = dict["ip"].ToString();
                    txtPort.Text = dict["port"].ToString();
                    txtUser.Text = dict["user"].ToString();
                    txtPassword.Text = dict["password"].ToString();
                }
                catch(System.Exception ex)
                {
                    log($"读取配置文件失败：{ex.Message}");
                }
            }
            else
            {
                txtPort.Text = "1883";
                txtUser.Text = "admin";
                txtPassword.Text = "public";
            }

            // 注册传感器
            SensorManager sensorManager = GetSystemService(Context.SensorService) as SensorManager;
            sensorManager.RegisterListener(this, sensorManager.GetDefaultSensor(SensorType.Light), SensorDelay.Fastest);
            // sensorManager.RegisterListener(this, sensorManager.GetDefaultSensor(SensorType.Proximity), SensorDelay.Normal);
        }

        private void Button_Click(object sender, System.EventArgs e)
        {
            if (deviceInfo == null) deviceInfo = new DeviceInfo(this);
            string ip = txtIP.Text.Trim();
            string port = txtPort.Text.Trim();
            string user = txtUser.Text.Trim();
            string password = txtPassword.Text.Trim();
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                log("IP和端口必填");
                return;
            }

            else if (isStart == false)
            {
                isStart = true;

                #region 生成浮动像素点
                // 生成浮动像素点
                /*
                WindowManagerLayoutParams layoutParams = new WindowManagerLayoutParams();
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    layoutParams.Type = WindowManagerTypes.ApplicationOverlay;
                }
                else
                {
                    layoutParams.Type = WindowManagerTypes.ApplicationPanel;
                }

                layoutParams.Format = Format.Rgba8888;
                layoutParams.Gravity = GravityFlags.Left | GravityFlags.Top;
                layoutParams.Flags = WindowManagerFlags.NotTouchModal | WindowManagerFlags.NotFocusable;
                layoutParams.Width = 200;
                layoutParams.Height = 200;
                layoutParams.X = 0;
                layoutParams.Y = 0;
                Button button = new Button(this.ApplicationContext);
                button.Text = "语音识别";
                button.SetBackgroundColor(Color.Argb(200, 0, 0, 0));
                button.SetTextColor(Color.Red);
                bool isRecord = false;
                button.Click += (s, ev) =>
                {
                    if (isRecord) return;
                    isRecord = true;

                    Vibrator vibrator = GetSystemService(Context.VibratorService) as Vibrator;
                    vibrator.Vibrate(500);
                    Task.Run(() =>
                    {
                        try
                        {
                            handler.Post(()=>
                            {
                                button.Text = "开始录音";
                            });
                            this.log("开始录音");

                            int bufferSizeInBytes = AudioRecord.GetMinBufferSize(16000, ChannelIn.Mono, Encoding.Pcm16bit);
                            AudioRecord audioRecord = new AudioRecord(AudioSource.Mic, 16000, ChannelIn.Mono, Encoding.Pcm16bit, bufferSizeInBytes);
                            audioRecord.StartRecording();
                            byte[] audiodata = new byte[bufferSizeInBytes];
                            int readsize = 0;
                            var audioFile = Java.IO.File.CreateTempFile("record_", ".pcm");
                            Java.IO.FileOutputStream fos = new Java.IO.FileOutputStream(audioFile.AbsolutePath);

                            System.DateTime stopTime = System.DateTime.Now.AddSeconds(4);
                            while (System.DateTime.Now.Ticks < stopTime.Ticks)
                            {
                                readsize = audioRecord.Read(audiodata, 0, bufferSizeInBytes);
                                if (-3 != readsize)
                                {
                                    try
                                    {
                                        fos.Write(audiodata);
                                    }
                                    catch (IOException ex)
                                    {

                                    }
                                }
                            }

                            this.log("正在识别");
                            handler.Post(() =>
                            {
                                button.Text = "正在识别";
                            });

                            var ai = new Baidu.Aip.Speech.Asr("17944158", "HLhr7GE05bY0gAzalObMHtUE", "fzFiBnLYKSMeFddsDGVZBnsyV0O0WACT");
                            ai.Timeout = 60000;
                            var data = File.ReadAllBytes(audioFile.AbsolutePath);
                            // 可选参数
                            var options = new Dictionary<string, object>();
                            options.Add("dev_pid", 1537);
                            ai.Timeout = 120000; // 若语音较长，建议设置更大的超时时间. ms
                            var result = ai.Recognize(data, "pcm", 16000, options);
                            if (System.Convert.ToInt32(result["err_no"]) == 0)
                            {
                                string msg = result["result"][0].ToString();
                                this.log(msg);
                                mqttHA.Publish($"android/voice/text", msg);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            this.log($"录音异常： {ex.Message}");
                        }
                        this.log("语音识别结束");
                        isRecord = false;
                        handler.Post(() =>
                        {
                            button.Text = "语音识别";
                        });
                    });
                };
                WindowManager.AddView(button, layoutParams);
                */
                #endregion

                log("开始连接MQTT服务。。。");
                mqttHA = new MqttHA(ip, port, user, password);
                mqttHA.Connect(mqttEvent =>
                {
                    // 保存配置
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    dict.Add("ip", ip);
                    dict.Add("port", port);
                    dict.Add("user", user);
                    dict.Add("password", password);
                    File.WriteAllText(configFile, JsonConvert.SerializeObject(dict));

                    log("连接MQTT服务成功");
                    // 语音识别
                    string voiceTopic = $"android/{deviceInfo.DeviceId}/voice";
                    string voiceTextTopic = $"android/{deviceInfo.DeviceId}/voice/text";
                    log($"订阅【语音识别】：{voiceTopic}");
                    Java.IO.File audioFile = null;
                    bool isRecording = false;
                    mqttHA.AddSubscribe(voiceTopic, (payload) =>
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                // 开始录音
                                if (payload == "start" && !isRecording)
                                {
                                    isRecording = true;
                                    // 震动一下
                                    Vibrator vibrator = GetSystemService(Context.VibratorService) as Vibrator;
                                    vibrator.Vibrate(VibrationEffect.CreateOneShot(500, 1));

                                    log("开始录音");
                                    int bufferSizeInBytes = AudioRecord.GetMinBufferSize(16000, ChannelIn.Mono, Encoding.Pcm16bit);
                                    AudioRecord audioRecord = new AudioRecord(AudioSource.Mic, 16000, ChannelIn.Mono, Encoding.Pcm16bit, bufferSizeInBytes);
                                    audioRecord.StartRecording();
                                    int readsize = 0;
                                    byte[] audiodata = new byte[bufferSizeInBytes];
                                    audioFile = Java.IO.File.CreateTempFile("record_", ".pcm");
                                    Java.IO.FileOutputStream fos = new Java.IO.FileOutputStream(audioFile.AbsolutePath);
                                    System.DateTime today = System.DateTime.Now;
                                    while (isRecording)
                                    {
                                        readsize = audioRecord.Read(audiodata, 0, bufferSizeInBytes);
                                        if (-3 != readsize)
                                        {
                                            try
                                            {
                                                fos.Write(audiodata);
                                            }
                                            catch (IOException ioEx)
                                            {
                                                log(ioEx.Message);
                                            }
                                        }
                                        // 判断是否超过十秒
                                        if (System.DateTime.Now.Subtract(today).TotalSeconds > 10)
                                        {
                                            if (isRecording)
                                            {
                                                throw new System.Exception("录音超过10秒, 还没有结束，所以中止掉");
                                            }
                                        }
                                    }
                                }
                                // 结束录音
                                if (payload == "stop" && isRecording && audioFile != null)
                                {
                                    log("正在识别");
                                    // 使用百度语音识别
                                    string result = RecognizeText(audioFile.AbsolutePath);
                                    if (string.IsNullOrEmpty(result))
                                    {
                                        log("语音识别结果错误");
                                    }
                                    else
                                    {
                                        log(result);
                                        mqttHA.Publish(voiceTextTopic, result);
                                    }
                                    audioFile = null;
                                    isRecording = false;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                log($"录音异常： {ex.Message}");
                                isRecording = false;
                            }
                        });
                    });

                    // 设置屏幕亮度
                    string brightness_topic = $"android/{deviceInfo.DeviceId}/brightness/set";
                    log($"订阅【设置亮度】：{brightness_topic}");
                    mqttHA.AddSubscribe(brightness_topic, (payload) =>
                    {
                        log($"设置亮度：{payload}");
                        deviceInfo.Brightness = int.Parse(payload);
                    });
                    // 设置Music音量
                    string volumeMusicTopic = $"android/{deviceInfo.DeviceId}/volume/music/set";
                    log($"订阅【设置音量】：{volumeMusicTopic}");
                    mqttHA.AddSubscribe(volumeMusicTopic, (payload) =>
                    {
                        log($"设置音量：{payload}");
                        deviceInfo.Volume = int.Parse(payload);
                    });

                    // 设置System音量
                    string volumeSystemTopic = $"android/{deviceInfo.DeviceId}/volume/system/set";
                    log($"订阅【设置System音量】：{volumeSystemTopic}");
                    mqttHA.AddSubscribe(volumeSystemTopic, (payload) =>
                    {
                        log($"设置System音量：{payload}");
                        deviceInfo.audioManager.SetStreamVolume(Android.Media.Stream.System, int.Parse(payload), VolumeNotificationFlags.PlaySound);
                    });

                    // 设置Alarm音量
                    string volumeAlarmTopic = $"android/{deviceInfo.DeviceId}/volume/alarm/set";
                    log($"订阅【设置Alarm音量】：{volumeAlarmTopic}");
                    mqttHA.AddSubscribe(volumeAlarmTopic, (payload) =>
                    {
                        log($"设置Alarm音量：{payload}");
                        deviceInfo.audioManager.SetStreamVolume(Android.Media.Stream.Alarm, int.Parse(payload), VolumeNotificationFlags.PlaySound);               
                    });

                    System.Threading.Thread thread = new System.Threading.Thread(() =>
                    {
                        while (true)
                        {
                            Dictionary<string, string> pad = mqttHA.ConfigSensor(deviceInfo.DeviceId, deviceInfo.DeviceName, "mdi:tablet", "");
                            Dictionary<string,string> battery = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} 电量", "mdi:battery", "%", "battery");
                            Dictionary<string, string> volume = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} 音量", "mdi:volume-high");
                            Dictionary<string, string> lx = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} 光照传感器", "mdi:brightness-5", "lx");
                            Dictionary<string, string> brightness = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} 屏幕亮度", "mdi:brightness-6");
                            Dictionary<string, string> storage = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} 存储空间", "mdi:harddisk");
                            Dictionary<string, string> wifi = mqttHA.ConfigSensor(deviceInfo.DeviceId, $"{deviceInfo.DeviceName} WiFi", "mdi:wifi", "");

                            System.Threading.Thread.Sleep(2000);
                            // 状态
                            mqttHA.Publish(pad["state_topic"], Android.OS.Build.Brand);
                            mqttHA.Publish(volume["state_topic"], deviceInfo.Volume.ToString());
                            mqttHA.Publish(wifi["state_topic"], deviceInfo.wifiInfo.SSID.Trim('"'));
                            mqttHA.Publish(storage["state_topic"], deviceInfo.StorageAvailable);
                            mqttHA.Publish(battery["state_topic"], deviceInfo.Battery.ToString());
                            mqttHA.Publish(lx["state_topic"], deviceInfo.LightSensor.ToString());
                            mqttHA.Publish(brightness["state_topic"], deviceInfo.Brightness.ToString());
                            // 音量
                            Dictionary<string, string> volumeAttributes = new Dictionary<string, string>();
                            volumeAttributes.Add("VoiceCall", deviceInfo.audioManager.GetStreamVolume(Android.Media.Stream.VoiceCall).ToString());
                            volumeAttributes.Add("Music", deviceInfo.audioManager.GetStreamVolume(Android.Media.Stream.Music).ToString());
                            volumeAttributes.Add("Music Max", deviceInfo.audioManager.GetStreamMaxVolume(Android.Media.Stream.Music).ToString());
                            volumeAttributes.Add("System", deviceInfo.audioManager.GetStreamVolume(Android.Media.Stream.System).ToString());
                            volumeAttributes.Add("System Max", deviceInfo.audioManager.GetStreamMaxVolume(Android.Media.Stream.System).ToString());
                            volumeAttributes.Add("Alarm", deviceInfo.audioManager.GetStreamVolume(Android.Media.Stream.Alarm).ToString());
                            volumeAttributes.Add("Alarm Max", deviceInfo.audioManager.GetStreamMaxVolume(Android.Media.Stream.Alarm).ToString());
                            volumeAttributes.Add("Music Topic", volumeMusicTopic);
                            volumeAttributes.Add("System Topic", volumeSystemTopic);
                            volumeAttributes.Add("Alarm Topic", volumeAlarmTopic);
                            mqttHA.PublishJson(volume["attributes_topic"], volumeAttributes);
                            // WiFi
                            Dictionary<string, string> wifiAttributes = new Dictionary<string, string>();
                            wifiAttributes.Add("SSID", deviceInfo.wifiInfo.SSID.Trim('"'));
                            wifiAttributes.Add("Rssi", deviceInfo.wifiInfo.Rssi.ToString());
                            wifiAttributes.Add("IpAddress", deviceInfo.wifiInfo.IpAddress.ToString());
                            wifiAttributes.Add("MacAddress", deviceInfo.wifiInfo.MacAddress);
                            mqttHA.PublishJson(wifi["attributes_topic"], wifiAttributes);
                            // 小米平板
                            Dictionary<string, string> padAttributes = new Dictionary<string, string>();
                            padAttributes.Add("IP Address", deviceInfo.IP);
                            padAttributes.Add("DeviceId", deviceInfo.DeviceId);
                            padAttributes.Add("UpdatedTime", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            padAttributes.Add("Battery", deviceInfo.Battery.ToString());
                            padAttributes.Add("VoiceTopic", voiceTopic);
                            padAttributes.Add("VoiceTextTopic", voiceTextTopic);
                            mqttHA.PublishJson(pad["attributes_topic"], padAttributes);
                            // 存储空间
                            Dictionary<string, string> storageAttributes = new Dictionary<string, string>();
                            storageAttributes.Add("total", deviceInfo.StorageTotal);
                            storageAttributes.Add("available", deviceInfo.StorageAvailable);
                            storageAttributes.Add("free", deviceInfo.StorageFree);
                            mqttHA.PublishJson(storage["attributes_topic"], storageAttributes);
                            // 屏幕亮度
                            Dictionary<string, string> brightnessAttributes = new Dictionary<string, string>();
                            brightnessAttributes.Add("topic", brightness_topic);
                            mqttHA.PublishJson(brightness["attributes_topic"], brightnessAttributes);

                            System.Threading.Thread.Sleep(5000);
                        }
                    });
                    thread.Start();
                }, disEvent=>
                {
                    log("断开连接了哦");
                });

                txtIP.Enabled = false;
                txtPort.Enabled = false;
                txtUser.Enabled = false;
                txtPassword.Enabled = false;
            }
        }

        string RecognizeText(string filePath)
        {
            // 使用百度语音识别
            var ai = new Baidu.Aip.Speech.Asr("17944158", "HLhr7GE05bY0gAzalObMHtUE", "fzFiBnLYKSMeFddsDGVZBnsyV0O0WACT");
            ai.Timeout = 60000;
            var data = File.ReadAllBytes(filePath);
            // 可选参数
            var options = new Dictionary<string, object>();
            options.Add("dev_pid", 1537);
            ai.Timeout = 120000; // 若语音较长，建议设置更大的超时时间. ms
            var result = ai.Recognize(data, "pcm", 16000, options);
            if (System.Convert.ToInt32(result["err_no"]) == 0)
            {
                return result["result"][0].ToString();
            }
            return "";
        }

        void log(string msg)
        {
            Runnable runnable = new Runnable(() =>
            {
                // 100次日志后，清空
                logLine++;
                if(logLine > 100)
                {
                    txtLog.Text = "";
                }
                
                txtLog.Append($"\n[{System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]\n{msg}\n");
                txtLog.MovementMethod = ScrollingMovementMethod.Instance;
                txtLog.SetSelection(txtLog.Text.Length, txtLog.Text.Length);
            });

            handler.Post(runnable);            
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            
        }

        public void OnSensorChanged(SensorEvent e)
        {
            // 光照传感器
            if(e.Sensor.Name.Contains("Light Sensor"))
            {
                
                if(deviceInfo != null)
                {
                    deviceInfo.LightSensor = e.Values[0];
                    this.log(e.Sensor.Name + " : " + e.Values[0].ToString());
                }
            }
        }
    }
}