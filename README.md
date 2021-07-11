# MiPadAndroid

控制辣鸡小米平板1接入HomeAssistant

---

## 配置信息

HA配置
```yaml
mipad_android:
  host: 192.168.1.104
  web_url: http://192.168.1.119/local/TileBoard/index.html?r=TIMESTAMP
  mqtt_host: 192.168.1.119
```

设置功能
```yaml
service: mqtt.publish
data:
  topic: android/13e6af99/set
  payload: 'tts: 这是一条TTS文本转语音消息'
  payload: 'brightness: 255'
  payload: 'music_volume: 15'
  payload: 'alarm_volume: 7'
  payload: 'system_volume: 7'
```

因为辣鸡小米平板1是使用的安卓4.4系统，然后不能使用内置webview打开HomeAssistant页面，
也不能使用HomeAssistant的APP，所以这个辣鸡APP就产生了

## 使用环境

- 小米平板1
- Android 4.4
- 权限我全都要

## 功能介绍
- 显示手机相关信息
- 控制音量和屏幕亮度
- 语音识别

## 发布方法

- 将`Debug`改为`Release`
- 右键项目选择`存档`
- `分发` - 临时 - 密钥为 `123456`


### 关联项目

- https://github.com/resoai/TileBoard
