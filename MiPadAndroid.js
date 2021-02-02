class MiPadAndroid extends HTMLElement {

    /*
     * 触发事件
     * type: 事件名称
     * data: 事件参数
     */
    fire(type, data) {
        const event = new Event(type, {
            bubbles: true,
            cancelable: false,
            composed: true
        });
        event.detail = data;
        this.dispatchEvent(event);
    }

    /*
     * 调用服务
     * service: 服务名称(例：light.toggle)
     * service_data：服务数据(例：{ entity_id: "light.xiao_mi_deng_pao" } )
     */
    callService(service_name, service_data = {}) {
        let arr = service_name.split('.')
        let domain = arr[0]
        let service = arr[1]
        this._hass.callService(domain, service, service_data)
    }

    mqttPublish(payload) {
        this.callService('mqtt.publish', { topic: this.stateObj.attributes['设置主题'], payload })
    }

    // 通知
    toast(message) {
        this.fire("hass-notification", { message })
    }

    /*
     * 接收HA核心对象
     */
    set hass(hass) {
        this._hass = hass
        if (!this.isCreated) {
            this.created(hass)
        }
    }

    get stateObj() {
        return this._stateObj
    }

    // 接收当前状态对象
    set stateObj(value) {
        this._stateObj = value
        // console.log(value)
        if (this.isCreated) this.updated()
    }

    // 创建界面
    created(hass) {
        /* ***************** 基础代码 ***************** */
        const shadow = this.attachShadow({ mode: 'open' });
        // 创建面板
        const ha_card = document.createElement('div');
        ha_card.className = 'custom-card-panel'
        ha_card.innerHTML = `
           <div class="flex">
              <span>屏幕亮度</span>
              <paper-slider
                id="brightness"
                value="1"
                max="255"
                pin
                markers
                style="flex: 1"
              >
              </paper-slider>
            </div>
            <div class="flex">
              <span>音乐音量</span>
              <paper-slider
                id="musicVolume"
                value="1"
                max="15"
                pin
                markers
                style="flex: 1"
              >
              </paper-slider>
            </div>
            <div class="flex">
              <span>闹钟音量</span>
              <paper-slider
                id="alarmVolume"
                value="1"
                max="15"
                pin
                markers
                style="flex: 1"
              >
              </paper-slider>
            </div>
            <div class="flex">
              <span>系统音量</span>
              <paper-slider
                id="systemVolume"
                value="1"
                max="15"
                pin
                markers
                style="flex: 1"
              >
              </paper-slider>
            </div>
            <paper-input
              id="ttsInput"
              always-float-label
              label="文本转语音"
            ></paper-input>
            <ha-attributes id="attrs"></ha-attributes>
        `
        shadow.appendChild(ha_card)
        // 创建样式
        const style = document.createElement('style')
        style.textContent = `
        .flex {
            display: flex;
            align-items: center;
        }
        .flex span {
            color: #857c79;
            font-size: 14px;
        }
        `
        shadow.appendChild(style);
        // 保存核心DOM对象
        this.shadow = shadow
        this.$ = this.shadow.querySelector.bind(this.shadow)
        // 创建成功
        this.isCreated = true
        /* ***************** 附加代码 ***************** */
        let { $ } = this
        const _this = this
        $("#brightness").addEventListener("change", function () {
            // console.log(this.value);
            _this.mqttPublish(`brightness: ${this.value}`)
        });
        $("#musicVolume").addEventListener("change", function () {
            // console.log(this.value);
            _this.mqttPublish(`music_volume: ${this.value}`)
        });
        $("#alarmVolume").addEventListener("change", function () {
            // console.log(this.value);
            _this.mqttPublish(`alarm_volume: ${this.value}`)
        });
        $("#systemVolume").addEventListener("change", function () {
            // console.log(this.value);
            _this.mqttPublish(`system_volume: ${this.value}`)
        });
        $("#ttsInput").addEventListener("change", function (event) {
            // console.log(this.value);
            _this.mqttPublish(`tts: ${this.value}`)
            this.value = "";
        });
    }

    // 更新界面数据
    updated(hass) {
        let { $, _stateObj } = this
        $('#attrs').stateObj = _stateObj
        $("#brightness").value = _stateObj.attributes['屏幕亮度'] || 0
        $("#musicVolume").value = _stateObj.attributes['音乐音量'] || 0
        $("#systemVolume").value = _stateObj.attributes['系统音量'] || 0
        $("#alarmVolume").value = _stateObj.attributes['闹钟音量'] || 0
    }
}
// 定义DOM对象元素
customElements.define('mipad-android', MiPadAndroid);