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
      logError('Extract download failed.', error, { url });
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

bundle(config)
  .then(result => { logInfo('Uploaded extract bundle', result); })
  .catch(error => { logError('Failed to upload bundle', error, config); });