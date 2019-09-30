FROM node:lts-alpine

LABEL maintainer "agentschap Informatie Vlaanderen <informatie.vlaanderen@vlaanderen.be>"

COPY extract-bundler.sh /extract-bundler/extract-bundler.sh
COPY extract-bundler.js /extract-bundler/extract-bundler.js
COPY node_modules  /extract-bundler/node_modules

RUN chmod +x /extract-bundler/extract-bundler.sh

CMD ["/extract-bundler/extract-bundler.sh"]
