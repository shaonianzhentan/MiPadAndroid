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
using Xamarin.Essentials;

namespace HA
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ISensorEventListener
    {
        TextInputEditText txtLog;
        TextInputEditText txtIP;
        TextInputEditText txtPort;
        TextInputEditText txtUser;
        TextInputEditText txtPassword;
        AudioManager audioManager = null;
        // 配置文件
        string configFile;
        // 日志行数
        int logLine = 0;
        // MQTT
        Button floatButton = null;
        MqttHA mqttHA = null;
        Dictionary<string, string> dictScreen;
        Dictionary<string, string> dictLock;
        Dictionary<string, string> dictLightSensor;
        Dictionary<string, string> dictBattery;
        Dictionary<string, string> dictCamera;
        Dictionary<string, string> dictVolume;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            audioManager = this.GetSystemService(Context.AudioService) as AudioManager;
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
                    Button_Click(null, null);
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

            // 提示常驻通知
            // AddNotification();
        }

        private void Button_Click(object sender, System.EventArgs e)
        {
            string ip = txtIP.Text.Trim();
            string port = txtPort.Text.Trim();
            string user = txtUser.Text.Trim();
            string password = txtPassword.Text.Trim();
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                log("IP和端口必填");
                return;
            }

            log("开始连接MQTT服务。。。");
            mqttHA = new MqttHA(ip, port, user, password, "我的平板");
            if (!mqttHA.TcpClientCheck(ip, int.Parse(port)))
            {
                mqttHA = null;
                log("连接MQTT服务失败！远程服务未开启");
                return;
            }

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
                this.PublishConfig();
                // 自动发现
                mqttHA.Subscribe("homeassistant/status", (payload) =>
                {
                    if (payload == "online")
                    {
                        this.PublishConfig();
                    }
                });
                // 屏幕亮度                
                mqttHA.Subscribe(dictScreen["command"], (string payload) =>
                {
                    mqttHA.Publish(dictScreen["state"], payload);
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, payload == "OFF" ? 1 : 200);
                });
                mqttHA.Subscribe(dictScreen["brightness"], (string payload) =>
                {
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, System.Convert.ToInt32(payload));
                    log($"设置亮度：{payload}");
                });
                // 锁屏
                mqttHA.Subscribe(dictLock["command"], (string payload) =>
                {
                    mqttHA.Publish(dictLock["state"], payload);
                    switch (payload)
                    {
                        case "UNLOCK":
                            this.FloatWindow(false);
                            break;
                        case "LOCK":
                            this.FloatWindow(true);
                            break;
                    }
                });
                // 音量
                mqttHA.Subscribe(dictVolume["command"], (string payload) =>
                {
                    audioManager.SetStreamVolume(Android.Media.Stream.System, int.Parse(payload), VolumeNotificationFlags.PlaySound);
                });
                // 发送信息
                this.PublishInfo();
            });
            txtIP.Enabled = false;
            txtPort.Enabled = false;
            txtUser.Enabled = false;
            txtPassword.Enabled = false;
        }

        void PublishConfig()
        {
            int systemVolumeMin = audioManager.GetStreamMinVolume(Android.Media.Stream.System);
            int systemVolumeMax = audioManager.GetStreamMaxVolume(Android.Media.Stream.System);

            dictLightSensor = mqttHA.ConfigSensor("光照传感器", "lx", "illuminance");
            dictScreen = mqttHA.ConfigLight("我的平板");
            dictCamera = mqttHA.ConfigCamera("我的平板");
            dictLock = mqttHA.ConfigLock("我的平板");
            dictBattery = mqttHA.ConfigSensor("平板电量", "%", "battery");
            dictVolume = mqttHA.ConfigNumber("平板音量", systemVolumeMin, systemVolumeMax);
        }

        void PublishInfo()
        {
            // 屏幕截图
            this.PublishScreenshot();
            // 电量
            string[] batteryState = new string[] { "Unknown", "Charging", "Discharging", "Full", "NotCharging", "NotPresent" };
            mqttHA.Publish(dictBattery["state"], (Xamarin.Essentials.Battery.ChargeLevel * 100).ToString());
            Dictionary<string, string> dictBatteryAttributes = new Dictionary<string, string>();
            dictBatteryAttributes.Add("BatteryState", batteryState[(int)Xamarin.Essentials.Battery.State]);
            mqttHA.PublishJson(dictBattery["attributes"], dictBatteryAttributes);
            // 音量
            int systemVolume = audioManager.GetStreamVolume(Android.Media.Stream.System);
            mqttHA.Publish(dictVolume["state"], systemVolume.ToString());
        }

        void log(string msg)
        {
            this.RunOnUiThread(() =>
            {
                // 100次日志后，清空
                logLine++;
                if (logLine > 100)
                {
                    txtLog.Text = "";
                }

                txtLog.Append($"\n[{System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]\n{msg}\n");
                txtLog.MovementMethod = ScrollingMovementMethod.Instance;
                txtLog.SetSelection(txtLog.Text.Length, txtLog.Text.Length);
            });
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
                if(mqttHA!=null && mqttHA.mqttClient.IsConnected)
                {
                    mqttHA.Publish(dictLightSensor["state"], e.Values[0].ToString());
                }
                // this.log(e.Sensor.Name + " : " + e.Values[0].ToString());
            }
        }

        #region
        /// <summary>
        /// 
        /// </summary>
        /// <param name="flags">显示黑屏：true,  关闭黑屏：false</param>
        void FloatWindow(bool flags)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (floatButton == null)
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
                    // floatButton.Text = 
                    floatButton.Text = System.DateTime.Now.ToString("HH:mm:ss");
                    floatButton.SetTextSize(ComplexUnitType.Sp, 200);
                    floatButton.SetBackgroundColor(Color.Black);
                    floatButton.SetTextColor(Color.White);
                    // floatButton.Visibility = ViewStates.Invisible;
                    floatButton.Click += (s, e) =>
                    {
                        floatButton.Visibility = ViewStates.Invisible;
                        Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, 200);
                    };
                    WindowManager.AddView(floatButton, layoutParams);

                    Timer timer = new Timer((state) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (floatButton.Visibility == ViewStates.Visible)
                            {
                                floatButton.Text = System.DateTime.Now.ToString("HH:mm:ss");
                            }
                        });

                    }, null, 0, 1000);
                }
                floatButton.Visibility = flags ? ViewStates.Visible : ViewStates.Invisible;
                if (flags)
                {
                    Settings.System.PutInt(this.ContentResolver, Settings.System.ScreenBrightness, 1);
                }
            });
        }
        #endregion


        private void PublishScreenshot()
        {
            View view = this.Window.DecorView;
            view.DrawingCacheEnabled = true;
            view.BuildDrawingCache();
            Bitmap bmp = view.DrawingCache;

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Compress(Bitmap.CompressFormat.Png, 100, ms);
                MQTTnet.MqttApplicationMessage msg = new MQTTnet.MqttApplicationMessage();
                msg.Topic = dictCamera["state"];
                msg.Payload = ms.ToArray();
                msg.Retain = false;
                mqttHA.mqttClient.PublishAsync(msg);
            }
        }


        // 添加通知
        public void AddNotification()
        {
            try
            {
                NotificationManager nm = this.GetSystemService(Context.NotificationService) as NotificationManager;
                Notification n = new Notification();
                n.Icon = Resource.Drawable.abc_ic_menu_share_mtrl_alpha;
                n.When = System.DateTime.Now.Ticks;
                n.Flags = NotificationFlags.OngoingEvent;

                Intent intent = new Intent(this, typeof(MainActivity));
                PendingIntent pendingIntent = PendingIntent.GetActivity(this, 0, intent, 0);
                n.SetLatestEventInfo(this, "HA", "正在运行中...", pendingIntent);
                nm.Notify(123, n);
            }
            catch (Exception ex)
            {
                log(ex.Message);
            }
          
        }
    }
}