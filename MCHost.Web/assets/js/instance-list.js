const MAX_INSTANCES = 5;

function escapeHtml(str) {
    var result = '';

    for (var i = 0; i < str.length; ++i) {
        switch (str[i]) {
            case '<':
                result += '&lt;';
                break;
            case '>':
                result += '&gt;';
                break;
            case '&':
                result += '&amp;';
                break;
            case '"':
                result += '&quot;';
                break;
            default:
                result += str[i];
        }
    }

    return result;
}

function appendToPre(elem, str) {
    var html = elem.html();
    if (html.length > 0)
        html += '\r\n';
    html += escapeHtml(str);

    elem.html(html);
    elem.scrollTop(elem.prop("scrollHeight"));
}

function Instance(id, packageName, status, address) {
    this.id = id;
    this.packageName = packageName;
    this.status = status;
    this.address = address;
    this.isSelected = false;
    this.log = [];
}

function InstanceList() {
    this.instances = [];
    this.instanceLookup = [];
    this.isFirstUpdate = true;
    this.templateHtml = '';
    this.noInstanceHtml = '';

    this.clear = function () {
        this.instances = [];
        this.instanceLookup = [];
        this.update();
        this.switchLog(null);
    }

    this.add = function (instanceId, packageName, status, address, lastLogMessages) {
        if (this.instances[instanceId]) {
            console.log('InstanceList.add - Instance ' + instanceId + ' already exists');
            return;
        }

        var instance = new Instance(instanceId, packageName, status, address);

        this.instances[instanceId] = instance;
        this.instanceLookup.push(instanceId);

        if (lastLogMessages) {
            for (var i = 0; i < lastLogMessages.length; ++i) {
                instance.log.push(lastLogMessages[i]);
            }
        }

        this.update();

        if (this.instanceLookup.length == 1) {
            this.select(instanceId);
        }
    }

    this.select = function (instanceId) {
        var result_instance = null;

        for (var i = 0; i < this.instanceLookup.length; ++i) {
            var instance = this.instances[this.instanceLookup[i]];

            instance.isSelected = instance.id == instanceId;
            if (instance.isSelected) {
                found = true;
                result_instance = instance;
            }
        }

        if (!result_instance) {
            console.log('Could not find instance ' + instanceId + ' in select');
            return;
        }

        this.update();
        this.switchLog(result_instance);
    }

    this.switchLog = function (instance) {
        var html = '';

        if (instance) { // allows this to be null to clear the console (used by InstanceList.clear() method)
            for (var i = 0; i < instance.log.length; ++i) {
                if (i > 0)
                    html = html + '\r\n';

                html = html + escapeHtml(instance.log[i]);
            }
        }

        var elem = $('#console-log');
        elem.html(html);
        elem.scrollTop(elem.prop("scrollHeight"));
    }

    this.addLog = function (instanceId, text) {
        var instance = this.instances[instanceId];
        if (!instance) {
            console.log('Instance ' + instanceId + ' was not found');
            return;
        }

        instance.log.push(text);
        if (instance.isSelected) {
            appendToPre($('#console-log'), text);
        }
    }

    this.remove = function (instanceId) {
        for (var i = 0; i < this.instanceLookup.length; ++i) {
            if (this.instanceLookup[i] == instanceId) {
                this.instanceLookup.splice(i, 1);
                delete this.instances[instanceId];
                this.switchLog(null);
                this.update();
                return;
            }
        }
    }

    this.updateInstance = function (instanceId, data) {
        var instance = this.instances[instanceId];
        if (!instance) {
            console.log('Instance ' + instanceId + ' was not found');
            return;
        }

        if (data.packageName)
            instance.packageName = data.packageName;

        if (data.status) {
            instance.status = data.status;

            if (data.status == 4 || // stopped
                data.status == 5) { // exception
                this.remove(instanceId);
                return;
            }
        }

        if (data.address)
            instance.address = data.address;

        this.update();
    }

    this.getHtmlForInstance = function (instance) {
        var html = this.templateHtml;

        var statusHtml = buildStatusTag(instance.status);

        html = replaceTemplate(html, {
            'instance-id': instance.id,
            'package-name': instance.packageName,
            'status': statusHtml,
            'address': instance.address
        });

        return html;
    }

    this.getSelected = function () {
        for (var i = 0; i < this.instanceLookup.length; ++i) {
            var instance = this.instances[this.instanceLookup[i]];

            if (instance.isSelected) {
                return instance.id;
            }
        }

        return null;
    }

    this.update = function () {

        if (this.isFirstUpdate) {
            var elem = $('#instance-list-template');
            elem.removeAttr('id'); // remove the unique html id attribute before we grab the template html below
            this.templateHtml = elem[0].outerHTML;
            elem.remove();

            elem = $('#no-instance-html');
            this.noInstanceHtml = elem[0].outerHTML;
            elem.remove();
            this.isFirstUpdate = false;
        }

        var html = '';

        var activeId = '';

        for (var i = 0; i < this.instanceLookup.length; ++i) {
            var instance = this.instances[this.instanceLookup[i]];

            html += this.getHtmlForInstance(instance);
            if (instance.isSelected) {
                activeId = instance.id;
            }
        }

        if (html.length == 0) {
            html = this.noInstanceHtml;
        }

        $('#btn-new-instance').css('display', this.instanceLookup.length >= MAX_INSTANCES ? 'none' : 'inline-block');

        $('#instance-list').html(html);

        if (activeId.length > 0) {
            $('li[data-id="' + activeId + '"]').addClass('active');
        }

        var instanceList = this; // save "this"

        $('#instance-list li').off('click'); // remove any current click handlers
        $('#instance-list li').on('click', function () {
            instanceList.select($(this).attr('data-id'));
        })

        $('#instance-list li .dropdown a').off('click');
        $('#instance-list li .dropdown a').on('click', function (evt) {
            $(this).parent().find('.dropdown-menu').dropdown('toggle');
            return false;
        })
    }
}