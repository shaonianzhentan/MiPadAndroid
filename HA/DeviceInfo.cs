using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Provider;
using static Android.OS.PowerManager;
using Android.Media;
using Android.App.Admin;
using Newtonsoft.Json;
using Android.Telephony;
using Android.Net.Wifi;
using Java.Net;
using Java.Util;
using Java.IO;

namespace HA
{
    public class DeviceInfo
    {
        private MainActivity activity;
        private WakeLock wakeLock;
        public AudioManager audioManager;
        public DevicePolicyManager policyManager;
        public PowerManager pm;

        // 电量      
        public readonly int Battery;
        public readonly string DeviceId;
        public readonly string DeviceName;
        public readonly string IP;

        // 光照传感器
        public float LightSensor = 0;
        public string StorageTotal;
        public string StorageAvailable;
        public string StorageFree;
        public WifiInfo wifiInfo;

        public DeviceInfo(MainActivity activity)
        {
            try
            {
                this.activity = activity;
                this.pm = activity.GetSystemService(Context.PowerService) as PowerManager;
                this.policyManager = activity.GetSystemService(Context.DevicePolicyService) as DevicePolicyManager;
                this.wakeLock = pm.NewWakeLock(WakeLockFlags.Full, "test");
                // 媒体声音
                this.audioManager = activity.GetSystemService(Context.AudioService) as AudioManager;
                // 电量
                Intent intent = new ContextWrapper(activity).RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
                this.Battery = intent.GetIntExtra(BatteryManager.ExtraLevel, -1) * 100 / intent.GetIntExtra(BatteryManager.ExtraScale, -1);
                // 手机信息(获取不到，会出现异常)
                // TelephonyManager telephonyManager = activity.GetSystemService(Context.TelephonyService) as TelephonyManager;            
                this.DeviceId = Android.OS.Build.Serial;
                this.DeviceName = Android.OS.Build.Model;

                // Wifi信息
                WifiManager wifiManager = activity.GetSystemService(Context.WifiService) as WifiManager;
                this.wifiInfo = wifiManager.ConnectionInfo;
                File datapath = Android.OS.Environment.DataDirectory;
                StatFs dataFs = new StatFs(datapath.Path);

                this.StorageTotal = getUnit(dataFs.TotalBytes);
                this.StorageAvailable = getUnit(dataFs.AvailableBytes);
                this.StorageFree = getUnit(Math.Abs(dataFs.FreeBlocksLong * dataFs.BlockSizeLong));

                // IP地址
                this.IP = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                            .Select(p => p.GetIPProperties())
                            .SelectMany(p => p.UnicastAddresses)
                            .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
                            .FirstOrDefault()?.Address.ToString();
            }
            catch(System.Exception ex)
            {
                Toast.MakeText(activity, ex.Message, ToastLength.Long);
            }
        }

        private string getUnit(long size)
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

        // 亮度
        public int Brightness
        {
            get
            {
                return  Settings.System.GetInt(activity.ContentResolver, Settings.System.ScreenBrightness);
            }
            set
            {
                Settings.System.PutInt(activity.ContentResolver, Settings.System.ScreenBrightness, value);
            }
        }

        // 锁屏
        public bool WakeLock
        {
            get
            {
                return wakeLock.IsHeld;
            }
            set
            {
                if (value && !wakeLock.IsHeld)
                {
                    wakeLock.Acquire();
                }
                else if (value == false && wakeLock.IsHeld)
                {
                    wakeLock.Release(WakeLockFlags.Full);
                }
            }
        }

        // 音量大小
        public int Volume
        {
            get
            {
                return audioManager.GetStreamVolume(Stream.Music);
            }
            set
            {
                audioManager.SetStreamVolume(Stream.Music, value, VolumeNotificationFlags.PlaySound);
            }
        }


        public override string ToString()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("Brightness", this.Brightness.ToString());
            dict.Add("Battery", this.Battery.ToString());
            dict.Add("Volume", this.Volume.ToString());
            dict.Add("WakeLock", this.WakeLock.ToString());
            dict.Add("IP", this.IP);
            return JsonConvert.SerializeObject(dict);
        }
    }
}