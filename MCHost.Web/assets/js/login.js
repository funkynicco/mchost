var updateBanTimeInterval = 0;
var banLift = 0;

function pad(n, width, z) {
    z = z || '0';
    n = n + '';
    return n.length >= width ? n : new Array(width - n.length + 1).join(z) + n;
}

function getTimeLeft(sec) {
    var hours = parseInt(sec / 3600);
    var minutes = parseInt((sec / 60) % 60);
    var seconds = parseInt(sec % 60);

    return pad(hours, 2) + ':' + pad(minutes, 2) + ':' + pad(seconds, 2);
}

function updateBanTime() {

    var tick = new Date().getTime() / 1000;
    if (tick < banLift) {
        var remainingSeconds = parseInt(banLift - (new Date().getTime() / 1000));

        $('.sign-alert').html('You can try again in ' + getTimeLeft(remainingSeconds)).fadeIn(200);
    } else {
        clearInterval(updateBanTimeInterval);
        $('.sign-alert').fadeOut();
        updateRemainingAttempts(1);
    }
}

function setBanTime(seconds) {
    banLift = (new Date().getTime() / 1000) + seconds;
    updateBanTimeInterval = setInterval(updateBanTime, 250);
}

function updateRemainingAttempts(attempts) {
    $('#attempts').html(attempts + ' attempt' + (attempts == 1 ? '' : 's') + ' remaining').fadeIn();

    if (attempts > 0) {
        $('#attempts').css('color', '#333');
        $('#btnSignIn').removeAttr('disabled');
    } else {
        $('#attempts').css('color', '#f00000');
        $('#btnSignIn').attr('disabled', 'disabled');
    }
}

$(function () {

    $('#btnSignIn').attr('disabled', 'disabled');

    $('#btnSignIn').click(function () {

        var email = $('#inputEmail').val();
        var password = $('#inputPassword').val();

        $('.sign-alert').fadeOut(200, function () {
            $.ajax({
                url: '/api/login',
                method: 'POST',
                data: 'email=' + escape(email) + '&password=' + escape(password)
            }).done(function (data) {
                console.log(data);
                if (data.result) {
                    document.location.href = '/';
                } else {
                    updateRemainingAttempts(data.attempts);
                    if (data.errors) {
                        var html = '';
                        for (var i = 0; i < data.errors.length; ++i) {
                            if (i > 0)
                                html += '<br />';
                            html += data.errors[i];
                        }
                        $('.sign-alert').html(html).fadeIn();
                    } else if (data.message) {
                        $('.sign-alert').html(data.message).fadeIn();
                    } else {
                        $('.sign-alert').html('Email or password is wrong.').fadeIn();
                    }

                    if (data.banTime) {
                        setTimeout(function () {
                            $('.sign-alert').fadeOut(function () {
                                setBanTime(data.banTime);
                            })
                        }, 2000);
                    }
                }
            }).fail(function (jqXHR, textStatus, str) {
                console.log('[' + textStatus + '] ' + str);
            })
        })

        return false;
    })

    /*
<b>Username or password incorrect!</b><br />
Repeated attempt to login will lock you out for a set of time.
    */

    setTimeout(function () {
        $.get('/api/login', function (data) {
            console.log(data);
            updateRemainingAttempts(data.attempts);
            if (data.banTime) {
                setBanTime(data.banTime);
            }
        })
    }, 1000);
})