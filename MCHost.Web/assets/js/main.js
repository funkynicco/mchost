var instanceList = new InstanceList();

function askShutdown(instanceId) {
    askShutdownDialog(instanceId, function () {
        Service.send('stp', { instanceId: instanceId });
    })

    return false;
}

function askTerminate(instanceId) {
    askTerminateDialog(instanceId, function () {
        Service.send('trm', { instanceId: instanceId });
    })

    return false;
}

function bindInterface() {
    $('#btn-new-instance').click(function () {
        startNewInstanceDialog(function (data) {
            Service.send('new', { packageName: data });
        })

        return false;
    })

    $('#cmd').keydown(function (evt) {
        if (evt.keyCode == 13) {
            var cmd = $(this).val();
            if (cmd.length > 0) {
                $(this).val('');
                var instanceId = instanceList.getSelected();
                if (instanceId) {
                    Service.send('cmd', {
                        instanceId: instanceId,
                        command: cmd
                    });
                }
            }
            return false;
        }

        return true;
    })
}

$(function () {
    instanceList.update();

    Service.on('NewInstance', function (data) {
        instanceList.add(data.instanceId, data.packageName, data.status, data.address);
    })

    Service.on('InstanceStatus', function (data) {
        instanceList.updateInstance(data.instanceId, {
            status: data.status
        })
    })

    Service.on('InstanceLog', function (data) {
        instanceList.addLog(data.instanceId, data.text);
    })

    Service.on('InstanceList', function (data) {
        instanceList.clear();
        for (var i = 0; i < data.instances.length; ++i) {
            var instance = data.instances[i];
            instanceList.add(instance.instanceId, instance.packageName, instance.status, instance.address, instance.lastLog);
        }
    })

    Service.on('ServiceError', function (data) {
        console.log('[Service Error] ' + data.message);
    })

    Service.initiate(function (result, event) {
        $('#loader').fadeOut(400, function () {
            if (result) {
                $('#main-instance-manager').fadeIn(400);
                bindInterface();
            } else {
                $(this).html('<div class="alert alert-danger">Failed to connect to service. <a href="/" class="alert-link">Reload the page</a> to try again.</div>')
                    .fadeIn(400);
            }
        })
    }, function (evtName, event) {
        // lost connection callback - evtName is either 'error' or 'closed'
        $('#lost-connection-div').fadeIn();
    })
})