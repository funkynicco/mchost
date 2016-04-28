function escapeProtocolData(json) {
    var result = '';

    for (var i = 0; i < json.length; ++i) {
        switch (json[i]) {
            case '%':
                result += '%25';
                break;
            case '|':
                result += '%7C';
                break;
            default:
                result += json[i];
                break;
        }
    }

    return result;
}

var Service = {
    socket: null,
    previousBuffer: '',
    subscriptionMethods: [],
    packetLookupTable: [],

    getWebSocketUrl: function() {
        var baseUrl = location.origin;
        if (baseUrl.indexOf("http://") == 0) {
            baseUrl = 'ws' + baseUrl.substr(4);
        } else if (baseUrl.indexOf('https://') == 0) {
            baseUrl = 'wss' + baseUrl.substr(5);
        }  else {
            console.log('Failed to retrieve WebSocket URL from origin: ' + location.origin);
            baseUrl = null;
        }

        return baseUrl;
    },

    initiate: function () {

        Service.packetLookupTable = [];
        Service.packetLookupTable['new'] = 'NewInstance';
        Service.packetLookupTable['is'] = 'InstanceStatus';
        Service.packetLookupTable['il'] = 'InstanceLog';
        Service.packetLookupTable['cfg'] = 'InstanceConfiguration';
        Service.packetLookupTable['lst'] = 'InstanceList';
        Service.packetLookupTable['err'] = 'ServiceError';
        Service.packetLookupTable['test'] = 'Test';

        var url = Service.getWebSocketUrl() + '/service';
        console.log('WebSocket connecting to \'' + url + '\' ...');
        Service.socket = new WebSocket(url);

        Service.socket.onopen = function () {
        }

        Service.socket.onclose = function (event) {
            console.log('onclose');
            console.log(event);
        }

        Service.socket.onerror = function (event) {
            console.log('onerror');
            console.log(event);
        }

        Service.socket.onmessage = function (event) {
            //console.log('(WebSocket) ' + event.data);

            var data = Service.previousBuffer + event.data;
            Service.previousBuffer = '';
            var pos = 0;

            while ((pos = data.indexOf('|')) != -1) {
                var header = data.substr(0, pos);
                data = data.substr(pos + 1);

                if ((pos = header.indexOf(' ')) != -1) {
                    Service.processPacket(
                        header.substr(0, pos).toLowerCase(),
                        unescape(header.substr(pos + 1)));
                } else {
                    Service.processPacket(header.toLowerCase(), '');
                }
            }

            if (data.length > 0)
                Service.previousBuffer = data.length;
        }
    },

    on: function (name, func) {
        var methods = Service.subscriptionMethods[name];
        if (!methods) {
            methods = [];
            Service.subscriptionMethods[name] = methods;
        }

        methods.push(func);
    },

    processPacket: function (header, content) {
        console.log('Packet \'' + header + '\' => ' + content);

        var jsonData = JSON.parse(content);

        var methodName = Service.packetLookupTable[header];
        if (methodName) {
            var methods = Service.subscriptionMethods[methodName];
            if (methods) {
                for (var i = 0; i < methods.length; ++i) {
                    methods[i](jsonData);
                }
            }
        } else {
            console.log('Header not found in method lookup: ' + header);
        }
    },

    send: function (header, obj) {
        Service.socket.send(header + ' ' + escapeProtocolData(JSON.stringify(obj)) + '|');
    },

    sendRaw: function (header, data) {
        Service.socket.send(header + ' ' + escapeProtocolData(data) + '|');
    }
}