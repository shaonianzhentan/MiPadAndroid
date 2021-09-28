using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;
using Microsoft.International.Converters.PinYinConverter;
using System.Net;
using System.Net.Sockets;


public class MqttDevice
{
    public string identifiers { get; set; }
    public string manufacturer { get; set; }
    public string model { get; set; }
    public string name { get; set; }
    public string sw_version { get; set; }
}

public class MqttHA
{
    public string ip { get; set; }
    public string host { get; set; }
    public int port { get; set; }
    public string user { get; set; }
    public string password { get; set; }

    MqttDevice device { get; set; }

    public IMqttClient mqttClient = null;

    // 订阅列表
    Dictionary<string, List<Action<string>>> subscribeList = new Dictionary<string, List<Action<string>>>();

    public MqttHA(string host, string port, string user, string password, MqttDevice device)
    {
        this.host = host;
        this.port = int.Parse(port);
        this.user = user;
        this.password = password;
        this.ip = GetIP();
        this.device = device;
    }

    public string PinYin(string name)
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
        return pinyin;
    }

    public Dictionary<string, string> GetTopic(string name, string type)
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();
        string pinyin = this.PinYin(name);
        string topic = $"{this.ip.Replace(".", "_")}/{type}_{pinyin}/";
        dict["state"] = $"{topic}state";
        dict["attributes"] = $"{topic}attributes";
        dict["command"] = $"{topic}command";
        dict["brightness"] = $"{topic}brightness";
        dict["brightness_command"] = $"{topic}brightness_command";
        dict["object_id"] = pinyin;
        return dict;
    }

    async public void Connect(Action<dynamic> connected, Action<dynamic> disconnected)
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
        mqttClient.UseConnectedHandler((action) =>
        {
            // 订阅
            foreach (string topic in subscribeList.Keys)
            {
                mqttClient.SubscribeAsync(topic);
            }
            // 连接成功
            connected(action);
        });
        mqttClient.UseDisconnectedHandler((action) =>
        {
            // 连接中断了啊
            disconnected(action);
        });
        mqttClient.UseApplicationMessageReceivedHandler(e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = e.ApplicationMessage.Payload == null ? "" : System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            if (subscribeList.Keys.Contains(topic))
            {
                foreach (var action in subscribeList[topic])
                {
                    action(payload);
                }
            }
        });
        this.mqttClient = mqttClient;
        await mqttClient.ConnectAsync(options.Build(), CancellationToken.None);
    }

    // 配置
    public void Config(string component, string object_id, Dictionary<string, object> dict)
    {
        string unique_id = $"{PinYin(this.device.name)}-{object_id}";
        // 实体唯一ID
        dict.Add("unique_id", unique_id);
        // 设备信息
        dict.Add("device", this.device);
        string payload = JsonConvert.SerializeObject(dict);
        this.Publish($"homeassistant/{component}/{unique_id}/config", payload);
    }

    public Dictionary<string, string> ConfigSensor(string name, string unit_of_measurement, string device_class = "")
    {
        Dictionary<string, string> topic = GetTopic(name, "sensor");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        dict.Add("state_topic", topic["state"]);
        dict.Add("json_attributes_topic", topic["attributes"]);
        dict.Add("unit_of_measurement", unit_of_measurement);
        if (!string.IsNullOrEmpty(device_class))
        {
            dict.Add("device_class", device_class);
        }
        Config("sensor", topic["object_id"], dict);
        return topic;
    }

    public Dictionary<string, string> ConfigLight(string name)
    {
        Dictionary<string, string> topic = GetTopic(name, "light");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        dict.Add("state_topic", topic["state"]);
        dict.Add("json_attributes_topic", topic["attributes"]);
        dict.Add("brightness_state_topic", topic["brightness"]);
        dict.Add("brightness_command_topic", topic["brightness_command"]);
        dict.Add("command_topic", topic["command"]);
        Config("light", topic["object_id"], dict);
        return topic;
    }


    public Dictionary<string, string> ConfigSwitch(string name, string icon)
    {
        Dictionary<string, string> topic = GetTopic(name, "switch");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        if (string.IsNullOrEmpty(icon))
        {
            dict.Add("icon", icon);
        }
        dict.Add("state_topic", topic["state"]);
        dict.Add("json_attributes_topic", topic["attributes"]);
        dict.Add("command_topic", topic["command"]);
        Config("switch", topic["object_id"], dict);
        return topic;
    }


    public Dictionary<string, string> ConfigNumber(string name, int min, int max)
    {
        Dictionary<string, string> topic = GetTopic(name, "number");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        dict.Add("state_topic", topic["state"]);
        dict.Add("command_topic", topic["command"]);
        dict.Add("min", min);
        dict.Add("max", max);
        Config("number", topic["object_id"], dict);
        return topic;
    }

    public Dictionary<string, string> ConfigCamera(string name)
    {
        Dictionary<string, string> topic = GetTopic(name, "camera");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        dict.Add("topic", topic["state"]);
        Config("camera", topic["object_id"], dict);
        return topic;
    }

    public Dictionary<string, string> ConfigLock(string name)
    {
        Dictionary<string, string> topic = GetTopic(name, "lock");
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("name", name);
        dict.Add("state_topic", topic["state"]);
        dict.Add("command_topic", topic["command"]);
        dict.Add("payload_lock", "LOCK");
        dict.Add("payload_unlock", "UNLOCK");
        dict.Add("state_locked", "LOCK");
        dict.Add("state_unlocked", "UNLOCK");
        Config("lock", topic["object_id"], dict);
        return topic;
    }

    public void Subscribe(string topic, Action<string> action)
    {
        if (!subscribeList.ContainsKey(topic))
        {
            subscribeList.Add(topic, new List<Action<string>>());
        }
        subscribeList[topic].Add(action);

        if (mqttClient != null && mqttClient.IsConnected)
        {
            mqttClient.SubscribeAsync(topic);
        }
    }


    // 发布
    public void Publish(string topic, string payload)
    {
        mqttClient.PublishAsync(topic, payload);
    }

    public void PublishJson(string topic, Dictionary<string, string> payload)
    {
        mqttClient.PublishAsync(topic, JsonConvert.SerializeObject(payload));
    }

    public string GetIP()
    {
        string ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Select(p => p.GetIPProperties())
            .SelectMany(p => p.UnicastAddresses)
            .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
            .FirstOrDefault()?.Address.ToString();
        return ip;
    }

    public static string GetIPAddress()
    {
        string ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Select(p => p.GetIPProperties())
            .SelectMany(p => p.UnicastAddresses)
            .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(p.Address))
            .FirstOrDefault()?.Address.ToString();
        return ip;
    }

    public bool TcpClientCheck(string ip, int port)
    {
        IPAddress ipa = IPAddress.Parse(ip);
        IPEndPoint point = new IPEndPoint(ipa, port);
        TcpClient tcp = null;

        try
        {
            tcp = new TcpClient();
            tcp.Connect(point);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
        finally
        {
            if (tcp != null)
            {
                tcp.Close();
            }
        }
    }
}