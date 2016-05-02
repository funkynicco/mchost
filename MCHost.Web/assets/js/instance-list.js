function Instance(id, packageName, status) {
    this.id = id;
    this.packageName = packageName;
    this.status = status;
    this.isSelected = false;
}

function InstanceList() {
    this.instances = [];
    this.instanceLookup = [];
    this.isFirstUpdate = true;
    this.templateHtml = '';

    this.add = function (instanceId, packageName, status) {
        if (this.instances[instanceId]) {
            console.log('InstanceList.add - Instance ' + instanceId + ' already exists');
            return;
        }

        this.instances[instanceId] = new Instance(instanceId, packageName, status);
        this.instanceLookup.push(instanceId);
    }

    this.getHtmlForInstance = function (instance) {
        var html = this.templateHtml;

        var statusHtml = buildStatusTag(instance.status);

        html = replaceTemplate(html, {
            'instance-id': instance.id,
            'package-name': instance.packageName,
            'status': statusHtml
        });

        return html;
    }

    this.update = function () {

        if (this.isFirstUpdate) {
            this.templateHtml = $('#instance-list-template')[0].outerHTML;
            $('#instance-list-template').remove();
            this.isFirstUpdate = false;
        }

        var html = '';

        var activeId = '';

        for (var i = 0; i < this.instances.length; ++i) {
            var instance = this.instances[i];

            html += this.getHtmlForInstance(instance);
            if (instance.isSelected) {
                activeId = instance.id;
            }
        }

        $('#instance-list').html(html);

        if (activeId.length > 0) {
            $('li[data-id="' + activeId + '"]').addClass('active');
        }
    }
}