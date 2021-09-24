import uuid, requests, urllib
from homeassistant.helpers import template
from homeassistant.components.http import HomeAssistantView
from homeassistant.helpers.network import get_url
from .const import VERSION, DOMAIN, DOMAIN_API

# 获取本机MAC地址
def get_mac_address_key(): 
    mac=uuid.UUID(int = uuid.getnode()).hex[-12:] 
    return "".join([mac[e:e+2] for e in range(0,11,2)])

# 解析模板
def template_message(_message):        
    tpl = template.Template(_message, HASS)
    _message = tpl.async_render(None)
    return _message

class DeviceServer:

    def __init__(self, hass, host, web_url, mqtt_host):
        self.hass = hass
        self.api_url = f"http://{host}:8124"
        self.web_url = web_url
        self.mqtt_host = mqtt_host
        self.ha_api = get_url(hass).strip('/') + DOMAIN_API

    def set_value(key, value):
        res = requests.get(self.api_url + '/set?key=' + key + '&value=' + str(value))
        print(res.json())

    # 连接MQTT
    def connect_mqtt(self):
        self.set_value('mqtt', self.mqtt_host)
        self.set_value('ha_api', self.ha_api)
        self.home_url(self.web_url)

    # 文本转语音
    def tts(self, text):
        self.set_value('tts', urllib.parse.quote(template_message(text)))

    # 设置屏幕亮度
    def brightness(self, value):
        self.set_value('brightness', value)

    # 锁定屏幕
    def lockscreen(self, value):
        self.set_value('float', value)

    # 打开语音识别
    def speech_recognition(self):
        self.set_value('speech_recognition', 1)

    # 打开主链接
    def home_url(self, web_url):
        self.set_value('url', urllib.parse.quote(web_url))

    # 设置系统音量
    def system_volume(self, value):
        self.set_value('system_volume', value)

class HassGateView(HomeAssistantView):

    url = DOMAIN_API
    name = DOMAIN
    requires_auth = False
    
    async def get(self, request):
        # 这里进行重定向
        hass = request.app["hass"]
        if 'text' in request.query:
            text = request.query['text']
            # 触发事件
            hass.async_create_task(hass.services.async_call('conversation', 'process', {'text': text}))
            return self.json({'code': '0', 'msg': text})
        else:
            return self.json({'code': '401', 'msg': '参数不正确'})