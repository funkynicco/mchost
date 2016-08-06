function showDialog(data) {
    HttpFetch.get('/assets/dialogs/' + data.name + '.html', function (html) {
        var element = $('#' + data.name);
        if (element.length == 0) {
            $('body').append(html);
            element = $('#' + data.name);
            if (data.init)
                data.init(data, element);
        }

        if (data.configure)
            data.configure(data, element);

        element.modal('show');
    })
}

function startNewInstanceDialog(cb) {
    showDialog({
        name: 'start-new-instance-dlg',
        init: function (data, element) { // called only the first time the dialog is appended to the body tag
            element.find('#btnConfirmNewInstance').click(function () {
                element.modal('hide');
                if (cb) {
                    cb(element.find('#selPackageName').val());
                }
                return false;
            })

            $.get('/api/packages', function (result) {
                var html = '';
                for (var i = 0; i < result.packages.length; ++i) {
                    html += '<option>' + result.packages[i].name + '</option>';
                }
                element.find('#selPackageName').html(html);
            })
        },
        configure: function (data, element) { // called every time just before the dialog is shown
        }
    })
}

function askShutdownDialog(instanceId, cb) {
    showDialog({
        name: 'ask-shutdown-dlg',
        init: function (data, element) {
            element.find('#btnConfirmShutdown').click(function () {
                element.modal('hide');
                if (cb) {
                    cb();
                }
                return false;
            })
        },
        configure: function (data, element) {
            element.find('.instanceId').html(instanceId);
        }
    })
}

function askTerminateDialog(instanceId, cb) {
    showDialog({
        name: 'ask-terminate-dlg',
        init: function (data, element) {
            element.find('#btnConfirmTerminate').click(function () {
                element.modal('hide');
                if (cb) {
                    cb();
                }
                return false;
            })
        },
        configure: function (data, element) {
            element.find('.instanceId').html(instanceId);
        }
    })
}