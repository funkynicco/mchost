HttpFetch = {

    enableCache: true,
    cache: [],

    get: function (url, cb) {

        if (HttpFetch.enableCache) {
            var cacheItem = HttpFetch.cache[url];

            if (cacheItem) {
                cb(cacheItem);
                return;
            }
        }

        $.get(url, function (data) {
            HttpFetch.cache[url] = data;
            cb(data);
        })
    }
};

function getStatusClass(status) {
    switch (status) {
        case 0:
        case 2:
            return 'c-green';
        case 1:
        case 3:
            return 'c-yellow';
        case 4:
        case 5:
            return 'c-red';
    }

    return null;
}

function getStatusString(status) {
    switch (status) {
        case 0: return 'Idle';
        case 1: return 'Starting';
        case 2: return 'Running';
        case 3: return 'Stopping';
        case 4: return 'Stopped';
        case 5: return 'Error';
    }

    return 'Unknown';
}

function buildStatusTag(status) {
    var cssClass = getStatusClass(status);
    if (cssClass)
        return '<span class="' + cssClass + '">' + getStatusString(status) + '</span>';
    else
        return '<span>' + getStatusString(status) + '</span>';
}

function replaceTemplate(template, dictionary) {
    var result = '';

    for (var i = 0; i < template.length;) {
        if (template[i] == '{') {
            ++i;
            var end = template.indexOf('}', i);
            if (end == -1) {
                console.log('replaceTemplate - Invalid template data: ' + template);
                return result;
            }

            var key = template.substr(i, end - i);

            var value = dictionary[key];
            if (!value) {
                console.log('replaceTemplate - Could not find key ' + key + ' in dictionary');
                return result;
            }

            result += value;

            i = end + 1;
            continue;
        }

        result += template[i++];
    }

    return result;
}