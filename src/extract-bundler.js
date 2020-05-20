'use strict';

const AWS = require('aws-sdk');
const axios = require('axios');
const unzip = require('unzip-stream');
const archiver = require('archiver');
const { logInfo, logError } = require('./datadog-logging');

const config = require('./configuration').load();
const s3 = new AWS.S3();

const bundle = async ({ extractDownloadUrls, archiveFormat, uploadOptions = {} }) => {
  return new Promise(async (resolve, reject) => {
    try {
      let downloadsBundle = archiver(archiveFormat);
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
      reject(error);
    }
  });
}

const populateBundle = async (downloadsBundle, extractDownloadUrls = []) => {
  for (var i = 0; i < extractDownloadUrls.length; i++) {
    let url = extractDownloadUrls[i];
    try {
      logInfo('Start streaming extract content', { url })
      let response = await axios({
        method: 'get',
        url: url,
        responseType: 'stream'
      });
      await appendDownload(downloadsBundle, { downloadNumber: i + 1, ...response });
      logInfo('Extract added to bundle', { url })
    } catch (error) {
      throw {
        message: `Failed adding extract to bundle: ${url}`,
        exceptionDetail: error
      };
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
          let name = entry.path;
          logInfo('Streaming file to bundle', { name });
          archive.append(entry, { name });
        });
  });
}

bundle(config)
  .then(result => { logInfo('Finished uploading extract bundle', result); })
  .catch(error => { logError('Failed to upload extract bundle', { bundleConfiguration: config, exception: error }); });
