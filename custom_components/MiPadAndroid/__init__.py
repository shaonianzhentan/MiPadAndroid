import socket, threading, json, requests, urllib

VERSION = '1.0'
DOMAIN = 'MiPadAndroid'
ROOT_PATH = f'/{DOMAIN}-local'
API_URL = None

def setup(hass, config):
    # 注册静态目录
    if hass is not None:
        hass.http.register_static_path(ROOT_PATH, hass.config.path('custom_components/MiPadAndroid/local'), False)
        hass.components.frontend.add_extra_js_url(hass, ROOT_PATH + '/MiPadAndroid.js?ver=' + VERSION)
        # 订阅服务
        hass.services.async_register(DOMAIN, 'load', load_data)
    # 读取配置
    web_url = config.get('web_url')
    mqtt_host = config.get('mqtt_host')
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
        api = data['api']
        global API_URL
        API_URL = api
        # 设置启动页面
        res = requests.get(api + 'set?key=mqtt&value=' + mqtt_host)
        print(res.json())
        set_web_url(web_url)
      
# 加载URL
def load_data(call):
    data = call.data
    url = data.get('url', '')
    if url != '':
        set_web_url(url)

# 设置页面
def set_web_url(web_url):
    if API_URL is not None:
        res = requests.get(API_URL + 'set?key=url&value=' + urllib.parse.quote(web_url))
        print(res.json())

'''
setup(None, {
    'web_url': 'http://192.168.1.119/local/TileBoard/index.html',
    'mqtt_host': '192.168.1.119'
})
'''