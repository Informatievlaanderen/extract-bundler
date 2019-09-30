'use strict';

const AWS = require('aws-sdk');
const axios = require('axios');
const unzip = require('unzip-stream');
const archiver = require('archiver');

const s3 = new AWS.S3();
const archiveFormat = 'zip';

const bundle = () => {
  const config = loadEnvironmentConfig();

  pushExtractsToS3(config)
    .then(result => { console.log('Uploaded extract bundle', result); })
    .catch(error => {
      console.log('Failed to upload bundle', { error, extractDownloadUrls, uploadOptions });
      throw error;
    });
};

const pushExtractsToS3 = async ({ extractDownloadUrls = [], uploadOptions = {} }) => {
  return new Promise(async (resolve, reject) => {
    try {
      let downloadsBundle = archiver(archiveFormat);
      const params = {
        ...uploadOptions,
        Body: downloadsBundle
      };

      s3.upload(params, (error, data) => {
        if (error) {
          reject(error);
        } else {
          resolve({
            download: data.Location,
            fileSize: downloadsBundle.pointer(),
          });
        }
      })

      await appendDownloads(downloadsBundle, extractDownloadUrls);
      downloadsBundle.finalize();
    }
    catch (error) {
      reject(error);
    }
  });
}

const appendDownloads = async (downloadsBundle, extractDownloadUrls = []) => {
  for (var i = 0; i < extractDownloadUrls.length; i++) {
    let url = extractDownloadUrls[i];
    try {
      let response = await axios({
        method: 'get',
        url: url,
        responseType: 'stream'
      });
      await appendDownload(downloadsBundle, { downloadNumber: i + 1, ...response });
    } catch (error) {
      console.log('Extract download failed.', { url, error });
    }
  }
}

const appendDownload = async (archive, { downloadNumber, data, headers }) => {
  return new Promise((resolve, reject) => {
    let downloadName = headers['content-disposition'].match(/filename=(.+)\.zip;/i)[1];
    downloadName = downloadName.replace(/-\d{1,4}-\d{1,2}-\d{1,4}$/, '');
    data
      .on('end', () => { resolve(); })
      .on('error', error => { reject(error); })
      .pipe(unzip.Parse())
        .on('entry', async entry => {
          let { path } = entry;
          archive.append(entry, { name: downloadNumber + '-' + downloadName + '/' + path });
        });
  });
}

const loadEnvironmentConfig = () => {
  const {
    EXTRACTDOWNLOADURLS = '',
    S3_BUCKET,
    S3_DESTINATIONPATH = '',
    BUNDLENAME
  } = process.env;

  const extractDownloadUrls = EXTRACTDOWNLOADURLS
    .split(',')
    .map(url => url.trim())
    .filter(url => url);

  if (extractDownloadUrls.length === 0)
    throwConfigurationException('EXTRACTDOWNLOADURLS')
  if (!S3_BUCKET)
    throwConfigurationException('S3_BUCKET');
  if (!BUNDLENAME)
    throwConfigurationException('BUNDLENAME');

  return {
    extractDownloadUrls,
    uploadOptions: {
      Bucket: S3_BUCKET,
      Key: `${S3_DESTINATIONPATH}${BUNDLENAME}-${getDateString()}.${archiveFormat}`
    }
  }
}

const throwConfigurationException = variableName => {
  throw `Environment variable ${variableName} not set!`
  + '\n\nEnvironment variables:'
  + '\n EXTRACTDOWNLOADURLS : string containing a comma separated list of download urls'
  + '\n S3_BUCKET : S3 bucket name'
  + '\n S3_DESTINATIONPATH : optional, prefix for the uploaded bundle'
  + '\n BUNDLENAME : name of bundle that will be suffixed with the date';
}

const getDateString = () => {
  const pad = (value, length) => {
    let padded = value.toString();
    while (padded.length < length)
      padded = '0' + padded;
    return padded;
  }

  const date = new Date();
  const month = pad(date.getMonth() + 1, 2);
  const day = pad(date.getDate(), 2);

  return `${date.getFullYear()}-${month}-${day}`;
}

bundle();
