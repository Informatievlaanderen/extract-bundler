'use strict'

const { logInfo, logError } = require('./datadog-logging');
const axios = require('axios');

const generateKey = async ({ destinationPath, keyNameTemplate, archiveFormat, versionUrl }) => {
    if (destinationPath.length > 0) {
        destinationPath = `${destinationPath}/`.replace(/\/\//g, '/');
    }
    
    const keyName = await resolve({
        keyNameTemplate,
        parameters: [
            { name: 'VERSION', resolveValue: async () => await getApiVersion(versionUrl) },
            { name: 'DATE', resolveValue: getDateString }
        ]
    });

    return `${destinationPath}${keyName}.${archiveFormat}`;
}

const resolve = async ( { keyNameTemplate = '', parameters = [] }) => {
    var keyName = keyNameTemplate;
    
    for (const { name, resolveValue } of parameters) {
        const parameterRegex = new RegExp(`\\[${name}\\]`, 'gi');
    
        if (keyName.match(parameterRegex)){
            const parameterValue = await resolveValue();
            keyName = keyName.replace(parameterRegex, parameterValue);
        }    
    }

    return keyName;
}

const getApiVersion = async url => {
    const unknownVersion = 'x.x';
    const { status = 'unknown', data = {} } = await axios.get(url);
    const { version = unknownVersion } = data;

    if (version === unknownVersion) {
        logError(`Unable to retrieve API version, set '${unknownVersion}'` , { requestUrl: url, status, response: data });
    }
  
    return version;
}

const getDateString = async () => {
    const date = new Date();
    const month = leftPad(date.getMonth() + 1, 2);
    const day = leftPad(date.getDate(), 2);

    return `${date.getFullYear()}${month}${day}`;
}

const leftPad = (value, length) => {
    const padded = value.toString();
    return padded.length < length ? leftPad('0' + padded, length) : padded;
}

module.exports.generateS3Key = generateKey;
