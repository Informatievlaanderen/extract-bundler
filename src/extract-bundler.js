'use strict';

const AWS = require('aws-sdk');
const axios = require('axios');
const unzip = require('unzip-stream');
const archiver = require('archiver');
const { logInfo, logError } = require('./datadog-logging');
const { generateS3Key } = require('./s3-key-generator');

const config = require('./configuration').load();
const streetNameConfig = require('./configuration').loadStreetName();
const addressConfig = require('./configuration').loadAddress();

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
      await new Promise(r => setTimeout(r, 5000)); //give some time before finalizing download (flush)
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
    Key: key.key,
    ContentDisposition: `attachment; filename=${key.fileName}`
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

bundle(addressConfig)
  .then(result => {
    logInfo('Finished uploading address extract bundle', result);
    bundle(streetNameConfig)
      .then(result => {
        logInfo('Finished uploading streetname extract bundle', result);
        bundle(config)
          .then(result => { logInfo('Finished uploading extract bundle', result); })
          .catch(error => { logError('Failed to upload extract bundle', { bundleConfiguration: config, exception: error }); });
      })
      .catch(error => { logError('Failed to upload streetname extract bundle', { bundleConfiguration: streetNameConfig, exception: error }); });
  })
  .catch(error => { logError('Failed to upload address extract bundle', { bundleConfiguration: addressConfig, exception: error }); });
