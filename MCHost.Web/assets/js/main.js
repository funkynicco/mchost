function addConsoleLog(str) {
    var element = $('#console-log');

    var html = element.html();
    if (html.length > 0)
        html += '\r\n';
    html += str;

    element.html(html);
    element.scrollTop(element.prop("scrollHeight"));
}

function bindInterface() {
    $('#btn-new-instance').click(function () {
        startNewInstanceDialog(function (data) {
            Service.send('new', { packageName: data });
        })

        return false;
    })
}

$(function () {

    var instanceList = new InstanceList();
    instanceList.add('08d371eae4ca52ad', 'Hoodoo', 2);
    instanceList.add('18d17ca67d8186f2', 'Infinity', 2);
    instanceList.add('6428c8317d1768ad', 'Infinity', 2);
    instanceList.update();

    setTimeout(function () {
        instanceList.instances[1].isSelected = true;
        instanceList.update();
    },2000)

    Service.on('NewInstance', function (data) {
        console.log('New instance of id ' + data.instanceId + ' with package name: ' + data.packageName);
        addInstanceToList({
            instanceId: data.instanceId,
            packageName: data.packageName,
            status: 0
        });
    })

    Service.on('InstanceStatus', function (data) {
        console.log('Instance status of ' + data.instanceId + ' => ' + data.status);
    })

    Service.on('InstanceLog', function (data) {
        console.log('[' + data.instanceId + '] ' + data.text);
    })

    Service.on('InstanceConfiguration', function (data) {
        console.log('Instance config from ' + data.instanceId);
    })

    Service.on('InstanceList', function (data) {
        for (var i = 0; i < data.instances.length; ++i) {
            console.log(data.instances[i]);
        }
    })

    Service.on('ServiceError', function (data) {
        console.log('[Service Error] ' + data.message);
    })

    Service.on('Test', function (data) {
        console.log(data);
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
    })
})