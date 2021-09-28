using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HA
{

    public class BluetoothBLEGattCallback: BluetoothGattCallback {

        public int step = 0;

        public override void OnConnectionStateChange(BluetoothGatt gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
        {
            if(status == GattStatus.Success)
            {
                gatt.DiscoverServices();
                
            }
            base.OnConnectionStateChange(gatt, status, newState);
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, [GeneratedEnum] GattStatus status)
        {
            byte[] buffer = characteristic.GetValue();
            if (buffer != null && buffer.Length >= 5)
            {   
                if(buffer.Length == 8)
                {
                    byte[] temphex = new byte[] { buffer[4], buffer[5] };
                    byte[] humihex = new byte[] { buffer[6], buffer[7] };
                    Console.WriteLine("温度：{0}", BitConverter.ToInt16(temphex, 0) / 100.0f);
                    Console.WriteLine("湿度：{0}", BitConverter.ToInt16(humihex, 0) / 100.0f);
                }
                else if(buffer.Length == 5)
                {
                    Console.WriteLine("电压：{0}", buffer[4] / 10.0f);
                }
            }
            base.OnCharacteristicRead(gatt, characteristic, status);
        }

        public override void OnServicesDiscovered(BluetoothGatt gatt, [GeneratedEnum] GattStatus status)
        {


            foreach (var service in gatt.Services)
            {
                if (service.Uuid.ToString() == "000016f0-0000-1000-8000-00805f9b34fb")
                {
                    foreach (var ch in service.Characteristics)
                    {
                        Console.WriteLine(ch.Uuid.ToString());
                        // 温湿度传感器
                        if (ch.Uuid.ToString() == "000016f2-0000-1000-8000-00805f9b34fb")
                        {
                            switch (step)
                            {
                                case 0:
                                    gatt.ReadCharacteristic(ch);
                                    break;
                                case 1:
                                    // 写入电量
                                    // ch.SetValue(new byte[] { 85, 3, 1, 16 });
                                    // 温湿度
                                    ch.SetValue(new byte[] { 85, 3, 8, 17 });
                                    gatt.WriteCharacteristic(ch);
                                    break;
                                case 2:
                                    gatt.ReadCharacteristic(ch);
                                    break;
                                case 3:
                                    ch.SetValue(new byte[] { 85, 3, 8, 17 });
                                    gatt.WriteCharacteristic(ch);
                                    break;
                                default:
                                    step = -1;
                                    break;
                            }
                            step += 1;
                        }

                    }
                }
            }
      

            base.OnServicesDiscovered(gatt, status);
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);
        }
    }

    public class BluetoothBLE
    {
        const string SERVICE_UUID = "000016f2-0000-1000-8000-00805f9b34fb";

        public string mac { get; set; }

        public BluetoothBLE(Activity activity)
        {
            
            BluetoothAdapter localAdapter = BluetoothAdapter.DefaultAdapter;
            // localAdapter.StartDiscovery();
            BluetoothDevice bluetoothDevice = localAdapter.GetRemoteDevice("68:3E:34:CC:E0:67");
            bluetoothDevice.ConnectGatt(activity, true, new BluetoothBLEGattCallback());

            // BluetoothGattService service = new BluetoothGattService(Java.Util.UUID.FromString("000016f2-0000-1000-8000-00805f9b34fb"), GattServiceType.Primary);

            // mGattCharacteristic = new BluetoothGattCharacteristic()


        }

        public void Update()
        {

        }
    }
}