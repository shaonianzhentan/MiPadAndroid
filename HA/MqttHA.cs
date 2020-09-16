using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;
using Microsoft.International.Converters.PinYinConverter;

namespace HA
{
    class MqttHA
    {
        public string host { get; set; }
        public int port { get; set; }
        public string user { get; set; }
        public string password { get; set; }

        public IMqttClient mqttClient = null;

        public Dictionary<string, Action<string>> subscribeList = new Dictionary<string, Action<string>>();

        public MqttHA(string host, string port, string user, string password)
        {
            this.host = host;
            this.port = int.Parse(port);
            this.user = user;
            this.password = password;
        }

        async public void  Connect(Action<MQTTnet.Client.Connecting.MqttClientConnectedEventArgs> action, Action<MQTTnet.Client.Disconnecting.MqttClientDisconnectedEventArgs> disconnectAction)
        {
            // 连接MQTT服务
            string clientId = Guid.NewGuid().ToString();
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithCleanSession(true)
                .WithWillDelayInterval(5)
                .WithTcpServer(host, port);
            if (!string.IsNullOrEmpty(user))
            {
                options.WithCredentials(user, password);
            };
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();
            mqttClient.UseConnectedHandler(action);
            mqttClient.UseDisconnectedHandler(disconnectAction);
            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = e.ApplicationMessage.Payload == null ? "" : System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                if (subscribeList.Keys.Contains(topic))
                {
                    subscribeList[topic](payload);
                }
            });
            this.mqttClient = mqttClient;
            await mqttClient.ConnectAsync(options.Build(), CancellationToken.None);
        }

        public Dictionary<string, string> ConfigSensor(string id, string name, string icon,string unit = "", string device_class = "")
        {
            string pinyin = "";
            foreach (char item in name)
            {
                try
                {
                    ChineseChar cc = new ChineseChar(item);
                    if (cc.Pinyins.Count > 0 && cc.Pinyins[0].Length > 0)
                    {
                        string temp = cc.Pinyins[0].ToString();
                        pinyin += temp.Substring(0, temp.Length - 1);
                    }
                }
                catch (Exception)
                {
                    pinyin += item.ToString();
                }
            }
            pinyin = pinyin.Replace(" ", "_");
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["state_topic"] = $"android/{id}/{pinyin}/state";
            dict["attributes_topic"] = $"android/{id}/{pinyin}/attributes";
            string uuid = id.Replace(".", "_");
            Dictionary<string, object> sensorDict = new Dictionary<string, object>();
            sensorDict.Add("name", name);
            sensorDict.Add("icon", icon);
            if (!string.IsNullOrEmpty(device_class))
            {
                sensorDict.Add("device_class", device_class);
            }
            if (!string.IsNullOrEmpty(unit))
            {
                sensorDict.Add("unit_of_measurement", unit);
            }
            sensorDict.Add("state_topic", dict["state_topic"]);
            sensorDict.Add("json_attributes_topic", dict["attributes_topic"]);            
            this.Config("sensor", $"{pinyin}_{uuid}", sensorDict);
            return dict;
        }

        // 配置
        public void Config(string component,string object_id, Dictionary<string, object> dict)
        {
            string model = "android";
            string unique_id = $"{model}-{object_id}";
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
            string payload = JsonConvert.SerializeObject(dict);
            this.Publish($"homeassistant/{component}/{model}/{unique_id}/config", payload);
        }

        // 订阅
        public void AddSubscribe(string topic, Action<string> action)
        {
            if (!subscribeList.ContainsKey(topic))
            {
                subscribeList.Add(topic, action);
                mqttClient.SubscribeAsync(topic);
            }
        }

        // 发布
        public void Publish(string topic, string payload)
        {
            mqttClient.PublishAsync(topic, payload);
        }

        public void PublishJson(string topic, Dictionary<string,string> payload)
        {
            mqttClient.PublishAsync(topic, JsonConvert.SerializeObject(payload));
        }
    }
}