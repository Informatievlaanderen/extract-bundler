'use strict';

const AWS = require('aws-sdk');
const axios = require('axios');
const unzip = require('unzip-stream');
const archiver = require('archiver');
const { logInfo, logError } = require('./datadog-logging');
const { generateS3Key } = require('./s3-key-generator');

const config = require('./configuration').load();
const s3 = new AWS.S3();

const bundle = async ({ extractDownloadUrls, apiVersionUrl, archiveFormat, s3Config }) => {
  return new Promise(async (resolve, reject) => {
    try {
      const uploadOptions = await createUploadOptions({ s3Config, apiVersionUrl, archiveFormat });
      const downloadsBundle = archiver(archiveFormat);
      const params = {
        ...uploadOptions,
        Body: downloadsBundle
      };

      logInfo('Uploading extract bundle', uploadOptions)
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

      await populateBundle(downloadsBundle, extractDownloadUrls);
      downloadsBundle.finalize();
    }
    catch (error) {
      console.error(error);
      reject(error);
    }
  });
}

const createUploadOptions = async ({ s3Config, apiVersionUrl, archiveFormat }) => {
  const key = await generateS3Key({
    destinationPath: s3Config.destinationPath, 
    keyNameTemplate: s3Config.keyNameTemplate, 
    versionUrl: apiVersionUrl,
    archiveFormat
  });

  return {
    Bucket: s3Config.bucket,
    Key: key
  };
}

const populateBundle = async (downloadsBundle, extractDownloadUrls = []) => {
  for (const url of extractDownloadUrls) {
    try {
      logInfo('Start streaming extract content', { url })
      
      const response = await axios({
        method: 'get',
        url: url,
        responseType: 'stream'
      });
      
      await appendDownload(downloadsBundle, response);
      
      logInfo('Extract added to bundle', { url })
    } catch (error) {
      throw {
        message: `Failed adding extract to bundle: ${url}`,
        exceptionDetail: error
      };
    }
  }
}

const appendDownload = async (archive, { data, headers }) => {
  return new Promise((resolve, reject) => {
    let downloadName = headers['content-disposition'].match(/filename=(.+)\.zip;/i)[1];
    downloadName = downloadName.replace(/-\d{1,4}-\d{1,2}-\d{1,4}$/, '');
    
    data
      .on('end', () => { resolve(); })
      .on('error', error => { reject(error); })
      .pipe(unzip.Parse())
        .on('entry', async entry => {
          const name = entry.path;
          logInfo('Streaming file to bundle', { name });
          archive.append(entry, { name });
        });
  });
}

bundle(config)
  .then(result => { logInfo('Finished uploading extract bundle', result); })
  .catch(error => { logError('Failed to upload extract bundle', { bundleConfiguration: config, exception: error }); });
