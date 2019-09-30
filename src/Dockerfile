FROM alpine:latest

COPY extract-bundler.sh /extract-bundler/extract-bundler.sh
COPY extract-bundler.js /extract-bundler/extract-bundler.js
COPY node_modules  /extract-bundler/node_modules

RUN chmod +x /extract-bundler/extract-bundler.sh

CMD ["/extract-bundler/extract-bundler.sh"]
