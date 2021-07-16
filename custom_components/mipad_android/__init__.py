import socket, threading, json, requests, urllib, logging, time
from homeassistant.helpers import template

_LOGGER = logging.getLogger(__name__)


VERSION = '1.1'
DOMAIN = 'mipad_android'
ROOT_PATH = f'/{DOMAIN}-local'
API_URL = None
HASS = None

def setup(hass, config):
    # 注册静态目录
    if hass is not None:
        global HASS
        HASS = hass
        hass.http.register_static_path(ROOT_PATH, hass.config.path(f'custom_components/{DOMAIN}/local'), False)
        hass.components.frontend.add_extra_js_url(hass, ROOT_PATH + '/MiPadAndroid.js?ver=' + VERSION)
        # 订阅服务
        hass.services.async_register(DOMAIN, 'load', load_data)
        hass.services.async_register(DOMAIN, 'setting', setting_data)
    # 读取配置
    cfg = config[DOMAIN]
    host = cfg.get('host', '')
    web_url = cfg.get('web_url').replace('TIMESTAMP', str(int(time.time())))
    mqtt_host = cfg.get('mqtt_host')
    set_api_url(host)
    # 显示插件信息
    _LOGGER.info('''
-------------------------------------------------------------------

    小米平板【作者QQ：635147515】
    
    版本：''' + VERSION + '''    
    
-------------------------------------------------------------------''')
    # 监听广播
    socket_recv_thread = threading.Thread(target=udp_socket_recv_client,args=(mqtt_host, web_url))
    socket_recv_thread.start()
    return True

# 接收信息
def udp_socket_recv_client(mqtt_host, web_url):    
    udp_socket = socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
    udp_socket.bind(("", 9234))
    while True:
        recv_data, recv_addr = udp_socket.recvfrom(1024)
        host = recv_addr[0]
        data = json.loads(recv_data.decode('utf-8'))
        print(data)
        ip = data['ip']
        set_api_url(ip)
        # 设置启动页面
        set_value('mqtt', mqtt_host)
        set_web_url(web_url)
      
# 加载URL
def load_data(call):
    data = call.data
    url = data.get('url', '')
    ip = data.get('ip', '')
    if url != '':
        set_web_url(url)
    if ip != '':
        set_api_url(ip)

# 设置API地址
def set_api_url(ip):
    if ip != '':
        global API_URL
        API_URL = f'http://{ip}:8124/'

# 设置页面
def set_web_url(web_url):
    set_value('url', urllib.parse.quote(web_url))

# 设置数据
def setting_data(call):
    data = call.data
    brightness = data.get('brightness', '')
    system_volume = data.get('system_volume', '')
    tts = data.get('tts', '')

    if tts != '':
        set_value('tts', urllib.parse.quote(template_message(tts)))

    if system_volume != '':
        set_value('system_volume', system_volume)

    if brightness != '':
        set_value('brightness', brightness)

# 设置值
def set_value(key, value):
    if API_URL is not None:
        res = requests.get(API_URL + 'set?key=' + key + '&value=' + value)
        print(res.json())

# 解析模板
def template_message(_message):        
    tpl = template.Template(_message, HASS)
    _message = tpl.async_render(None)
    return _message
'''
setup(None, {
    'web_url': 'http://192.168.1.119/local/TileBoard/index.html',
    'mqtt_host': '192.168.1.119'
})
'''