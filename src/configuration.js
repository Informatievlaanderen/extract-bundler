'use strict'

const loadConfiguration = () => {
    return {
        ...loadEnvironmentConfig().full,
        archiveFormat: 'zip',
    };
}

const loadConfigurationStreetName = () => {
    return {
        ...loadEnvironmentConfig().streetName,
        archiveFormat: 'zip',
    };
}

const loadEnvironmentConfig = () => {
    const {
        EXTRACTDOWNLOADURLS = '',
        STREETNAMEEXTRACTDOWNLOADURLS = '',
        APIVERSIONURL,
        S3_BUCKET,
        S3_DESTINATIONPATH = '',
        BUNDLENAME,
        STREETNAMEBUNDLENAME
    } = process.env;

    const extractDownloadUrls = EXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    const streetNameExtractDownloadUrls = STREETNAMEEXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    if (extractDownloadUrls.length === 0)
        throwConfigurationException('EXTRACTDOWNLOADURLS');
    if (streetNameExtractDownloadUrls.length === 0)
        throwConfigurationException('STREETNAMEEXTRACTDOWNLOADURLS');
    if (!APIVERSIONURL)
        throwConfigurationException("APIVERSIONURL");
    if (!S3_BUCKET)
        throwConfigurationException('S3_BUCKET');
    if (!BUNDLENAME)
        throwConfigurationException('BUNDLENAME');
    if (!STREETNAMEBUNDLENAME)
        throwConfigurationException('STREETNAMEBUNDLENAME');

    return {
        full: {
            extractDownloadUrls,
            apiVersionUrl: APIVERSIONURL,
            s3Config: {
                bucket: S3_BUCKET,
                destinationPath: S3_DESTINATIONPATH,
                keyNameTemplate: BUNDLENAME
            }
        },
        streetName: {
            extractDownloadUrls: streetNameExtractDownloadUrls,
            apiVersionUrl: APIVERSIONURL,
            s3Config: {
                bucket: S3_BUCKET,
                destinationPath: S3_DESTINATIONPATH,
                keyNameTemplate: STREETNAMEBUNDLENAME
            }
        }
    };
}

const throwConfigurationException = variableName => {
    throw `Environment variable ${variableName} not set!`
    + '\n\nEnvironment variables:'
    + '\n EXTRACTDOWNLOADURLS : string containing a comma separated list of download urls'
    + '\n STREETNAMEEXTRACTDOWNLOADURLS : string containing a comma separated list of download urls for streetname'
    + '\n APIVERSIONURL : current api version url'
    + '\n S3_BUCKET : S3 bucket name'
    + '\n S3_DESTINATIONPATH : optional, prefix for the uploaded bundle'
    + '\n BUNDLENAME : name of bundle supporting the following placeholders:'
    + '\n STREETNAMEBUNDLENAME : name of streetname bundle supporting the following placeholders:'
    + '\n - [VERSION] : API version'
    + '\n - [DATE] : date formated as yyyyMMdd';
}

module.exports.load = loadConfiguration;
module.exports.loadStreetName = loadConfigurationStreetName;
