'use strict'

const INFO = 'Info';
const ERROR = 'Error';
const logLevels = [ INFO, ERROR ];

const logInfo = (message, stack) => {
    log(INFO, message, stack);
}

const logError = (message, stack) => {
    log(ERROR, message, stack);
}

const log = (logLevel, message = '', stack) => {
    if (logLevels.indexOf(logLevel) === -1)
        logLevel = 'Debug';

    const datadogMessage = {
        '@t': new Date().toISOString(),
        '@l': logLevel,
        '@m': message,
        '@x': stack
    };
    console.log(JSON.stringify(datadogMessage));
}

module.exports = {
    logInfo,
    logError,
};
