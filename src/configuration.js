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

const loadConfigurationAddress = () => {
    return {
        ...loadEnvironmentConfig().address,
        archiveFormat: 'zip',
    };
}

const loadConfigurationAddressLinks = () => {
    return {
        ...loadEnvironmentConfig().addresslinks,
        archiveFormat: 'zip',
    };
}

const loadEnvironmentConfig = () => {
    const {
        EXTRACTDOWNLOADURLS = '',
        STREETNAMEEXTRACTDOWNLOADURLS = '',
        ADDRESSEXTRACTDOWNLOADURLS = '',
        ADDRESSLINKSEXTRACTDOWNLOADURLS = '',
        APIVERSIONURL,
        S3_BUCKET,
        S3_DESTINATIONPATH = '',
        BUNDLENAME,
        STREETNAMEBUNDLENAME,
        ADDRESSBUNDLENAME,
        ADDRESSLINKSBUNDLENAME
    } = process.env;

    const extractDownloadUrls = EXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    const streetNameExtractDownloadUrls = STREETNAMEEXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    const addressExtractDownloadUrls = ADDRESSEXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);


    const addresslinksExtractDownloadUrls = ADDRESSLINKSEXTRACTDOWNLOADURLS
        .split(',')
        .map(url => url.trim())
        .filter(url => url);

    if (extractDownloadUrls.length === 0)
        throwConfigurationException('EXTRACTDOWNLOADURLS');
    if (streetNameExtractDownloadUrls.length === 0)
        throwConfigurationException('STREETNAMEEXTRACTDOWNLOADURLS');
    if (addressExtractDownloadUrls.length === 0)
        throwConfigurationException('ADDRESSEXTRACTDOWNLOADURLS');
    if (addresslinksExtractDownloadUrls.length === 0)
        throwConfigurationException('ADDRESSLINKSEXTRACTDOWNLOADURLS');
    if (!APIVERSIONURL)
        throwConfigurationException("APIVERSIONURL");
    if (!S3_BUCKET)
        throwConfigurationException('S3_BUCKET');
    if (!BUNDLENAME)
        throwConfigurationException('BUNDLENAME');
    if (!STREETNAMEBUNDLENAME)
        throwConfigurationException('STREETNAMEBUNDLENAME');
    if (!ADDRESSBUNDLENAME)
        throwConfigurationException('ADDRESSBUNDLENAME');
    if (!ADDRESSLINKSBUNDLENAME)
        throwConfigurationException('ADDRESSLINKSBUNDLENAME');

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
        },
        address: {
            extractDownloadUrls: addressExtractDownloadUrls,
            apiVersionUrl: APIVERSIONURL,
            s3Config: {
                bucket: S3_BUCKET,
                destinationPath: S3_DESTINATIONPATH,
                keyNameTemplate: ADDRESSBUNDLENAME
            }
        },
        addresslinks: {
            extractDownloadUrls: addresslinksExtractDownloadUrls,
            apiVersionUrl: APIVERSIONURL,
            s3Config: {
                bucket: S3_BUCKET,
                destinationPath: S3_DESTINATIONPATH,
                keyNameTemplate: ADDRESSLINKSBUNDLENAME
            }
        }
    };
}

const throwConfigurationException = variableName => {
    throw `Environment variable ${variableName} not set!`
    + '\n\nEnvironment variables:'
    + '\n EXTRACTDOWNLOADURLS : string containing a comma separated list of download urls'
    + '\n STREETNAMEEXTRACTDOWNLOADURLS : string containing a comma separated list of download urls for streetname'
    + '\n ADDRESSEXTRACTDOWNLOADURLS : string containing a comma separated list of download urls for address'
    + '\n ADDRESSLINKSEXTRACTDOWNLOADURLS : string containing a comma separated list of download urls for addresslinks'
    + '\n APIVERSIONURL : current api version url'
    + '\n S3_BUCKET : S3 bucket name'
    + '\n S3_DESTINATIONPATH : optional, prefix for the uploaded bundle'
    + '\n BUNDLENAME : name of bundle supporting the following placeholders:'
    + '\n STREETNAMEBUNDLENAME : name of streetname bundle supporting the following placeholders:'
    + '\n ADDRESSBUNDLENAME : name of address bundle supporting the following placeholders:'
    + '\n ADDRESSLINKSBUNDLENAME : name of addresslinks bundle supporting the following placeholders:'
    + '\n - [VERSION] : API version'
    + '\n - [DATE] : date formated as yyyyMMdd';
}

module.exports.load = loadConfiguration;
module.exports.loadStreetName = loadConfigurationStreetName;
module.exports.loadAddress = loadConfigurationAddress;
module.exports.loadAddressLinks = loadConfigurationAddressLinks;
