FROM node:lts-alpine

LABEL maintainer "agentschap Informatie Vlaanderen <informatie.vlaanderen@vlaanderen.be>"

COPY extract-bundler.sh /extract-bundler/extract-bundler.sh
COPY *.js /extract-bundler/
COPY node_modules  /extract-bundler/node_modules

RUN dos2unix /extract-bundler/extract-bundler.sh && chmod +x /extract-bundler/extract-bundler.sh

CMD ["/extract-bundler/extract-bundler.sh"]
