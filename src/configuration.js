'use strict'

const loadConfiguration = () => {
    return {
        ...loadEnvironmentConfig(),
        archiveFormat: 'zip',
    };
}

const loadEnvironmentConfig = () => {
    const {
        EXTRACTDOWNLOADURLS = '',
        APIVERSIONURL,
        S3_BUCKET,
        S3_DESTINATIONPATH = '',
        BUNDLENAME
    } = process.env;

    const extractDownloadUrls = EXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    if (extractDownloadUrls.length === 0)
        throwConfigurationException('EXTRACTDOWNLOADURLS');
    if (!APIVERSIONURL)
        throwConfigurationException("APIVERSIONURL");
    if (!S3_BUCKET)
        throwConfigurationException('S3_BUCKET');
    if (!BUNDLENAME)
        throwConfigurationException('BUNDLENAME');

    return {
        extractDownloadUrls,
        apiVersionUrl: APIVERSIONURL,
        s3Config: {
            bucket: S3_BUCKET,
            destinationPath: S3_DESTINATIONPATH,
            keyNameTemplate: BUNDLENAME
        }
    };
}

const throwConfigurationException = variableName => {
    throw `Environment variable ${variableName} not set!`
    + '\n\nEnvironment variables:'
    + '\n EXTRACTDOWNLOADURLS : string containing a comma separated list of download urls'
    + '\n APIVERSIONURL : current api version url'
    + '\n S3_BUCKET : S3 bucket name'
    + '\n S3_DESTINATIONPATH : optional, prefix for the uploaded bundle'
    + '\n BUNDLENAME : name of bundle supporting the following placeholders:'
    + '\n - [VERSION] : API version'
    + '\n - [DATE] : date formated as yyyyMMdd';
}

module.exports.load = loadConfiguration;
