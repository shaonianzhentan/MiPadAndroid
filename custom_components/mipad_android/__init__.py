import socket, threading, json, requests, urllib, logging, time
from homeassistant.components.http import HomeAssistantView
from homeassistant.helpers import template
from homeassistant.helpers.network import get_url
from .shaonianzhentan import get_mac_address_key, DeviceServer, HassGateView

_LOGGER = logging.getLogger(__name__)

from .const import VERSION, DOMAIN, DOMAIN_API

def setup(hass, config):
    # 读取配置
    cfg = config[DOMAIN]
    # MQTT配置
    mqtt_host = cfg.get('mqtt')
    _list = cfg.get('list', [])
    # 安装
    for item in _list:
        host = item['host']
        url = item.get('url', '').replace('TIMESTAMP', str(int(time.time())))
        hass.data[f"{DOMAIN}{host}"] = DeviceServer(hass, host, url, mqtt_host)

    # 设置数据
    def setting_data(call):
        data = call.data
        key = f"{DOMAIN}{data.get('ip', '')}"
        if key in hass.data:
            dev = hass.data[key]
            brightness = data.get('brightness', '')
            system_volume = data.get('system_volume', '')
            tts = data.get('tts', '')
            lock = data.get('lock', '')

            if tts != '':
                dev.tts(tts)

            if system_volume != '':
                dev.system_volume(system_volume)

            if lock != '':
                dev.lockscreen(lock)

            if brightness != '':
                dev.brightness(brightness)

    # 订阅服务
    hass.services.async_register(DOMAIN, 'setting', setting_data)
    # 注册事件网关
    hass.http.register_view(HassGateView)
    # 显示插件信息
    _LOGGER.info('''
-------------------------------------------------------------------

    小米平板【作者QQ：635147515】
    
    版本：''' + VERSION + '''

    API：''' + get_url(hass).strip('/') + DOMAIN_API + '''
    
-------------------------------------------------------------------''')
    
    # 接收信息
    def udp_socket_recv_client():
        udp_socket = socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
        udp_socket.bind(("", 9234))
        while True:
            try:
                recv_data, recv_addr = udp_socket.recvfrom(1024)
                host = recv_addr[0]
                data = json.loads(recv_data.decode('utf-8'))
                print(data)
                # 设置启动页面
                ip = data.get('ip', '')
                if ip != '':
                    dev = hass.data[f"{DOMAIN}{ip}"]
                    dev.connect_mqtt()
            except Exception as ex:
                print(ex)

    # 监听广播
    socket_recv_thread = threading.Thread(target=udp_socket_recv_client,args=())
    socket_recv_thread.start()
    return True