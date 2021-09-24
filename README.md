# MiPadAndroid

控制辣鸡小米平板1接入HomeAssistant

---

## 配置信息

HA配置
```yaml
mipad_android:
  mqtt: 192.168.1.119
  list:
    - host: 192.168.1.104
      url: http://192.168.1.119/local/TileBoard/index.html?r=TIMESTAMP
    - host: 192.168.1.105
      url: http://192.168.1.119/local/TileBoard/index.html?r=TIMESTAMP
```

## 模板示例

判断当前设置是否打开
```js
{%- set filterDomain = ["light", "switch", "media_player"] -%}
{%- set filterState = ["on", "playing"] -%}
{% set ns = namespace(found=false) %}
{%- for state in states -%}
  {%- if state.state in filterState and state.domain in filterDomain -%}
       {%- set ns.found = true -%} {{ state.name }},
  {%- endif %}
{%- endfor %}
{%- if ns.found -%}
  还是打开的哦
{%- else %}
  当前所有设备已经关闭
{%- endif %}
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
- TTS文本转语音

## 发布方法

- 将`Debug`改为`Release`
- 右键项目选择`存档`
- `分发` - 临时 - 密钥为 `123456`


### 关联项目

- https://github.com/resoai/TileBoard


## 如果这个项目对你有帮助，请我喝杯<del><small>咖啡</small></del><b>奶茶</b>吧😘
|支付宝|微信|
|---|---|
<img src="https://ha.jiluxinqing.com/img/alipay.png" align="left" height="160" width="160" alt="支付宝" title="支付宝">  |  <img src="https://ha.jiluxinqing.com/img/wechat.png" align="left" height="160" width="160" alt="微信支付" title="微信">