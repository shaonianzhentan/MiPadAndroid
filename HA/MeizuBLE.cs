using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HA
{
    public class MeizuBLE
    {
        const string SERVICE_UUID = "000016f2-0000-1000-8000-00805f9b34fb";

        public string mac { get; set; }

        public MeizuBLE(string mac)
        {
            this.mac = mac;
            BluetoothAdapter localAdapter = BluetoothAdapter.DefaultAdapter;


        }

        public void Update()
        {

        }
    }
}