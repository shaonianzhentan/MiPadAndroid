class MiPadAndroidStateCard extends HTMLElement {

    /*
     * �����¼�
     * type: �¼�����
     * data: �¼�����
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
     * ���÷���
     * service: ��������(����light.toggle)
     * service_data����������(����{ entity_id: "light.xiao_mi_deng_pao" } )
     */
    callService(service_name, service_data = {}) {
        let arr = service_name.split('.')
        let domain = arr[0]
        let service = arr[1]
        this._hass.callService(domain, service, service_data)
    }

    // ֪ͨ
    toast(message) {
        this.fire("hass-notification", { message })
    }

    /*
     * ����HA���Ķ���
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

    // ���յ�ǰ״̬����
    set stateObj(value) {
        this._stateObj = value
        // console.log(value)
        if (this.isCreated) this.updated()
    }

    // ��������
    created(hass) {
        /* ***************** �������� ***************** */
        const shadow = this.attachShadow({ mode: 'open' });
        // �������
        const ha_card = document.createElement('div');
        ha_card.className = 'custom-card-panel'
        ha_card.innerHTML = `
            <div>
                ��Ļ���ȣ�<paper-slider  min="1" max="255"></paper-slider>
            </div>
            <ul id="attrs"></ul>
        `
        shadow.appendChild(ha_card)
        // ������ʽ
        const style = document.createElement('style')
        style.textContent = `
            .custom-card-panel{}
        `
        shadow.appendChild(style);
        // �������DOM����
        this.shadow = shadow
        this.$ = this.shadow.querySelector.bind(this.shadow)
        // �����ɹ�
        this.isCreated = true
        /* ***************** ���Ӵ��� ***************** */
        let { $ } = this
       
    }

    // ���½�������
    updated(hass) {
        let { $, _stateObj } = this
        $('#attrs').innerHTML = ''
        Object.keys(_stateObj.attributes).forEach(key => {
            let li = document.createElement('li')
            li.innerHTML = `${key}: ${_stateObj.attributes[key]}`
            $('#attrs').appendChild(li)
        })
    }
}
// ����DOM����Ԫ��
customElements.define('MiPadAndroid-StateCard', MiPadAndroidStateCard);
