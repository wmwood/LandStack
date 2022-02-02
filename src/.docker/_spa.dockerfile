### BUILD ###
FROM node:alpine as build-env
WORKDIR /app

ENV NODE_OPTIONS=--openssl-legacy-provider

COPY ./LandStack-Spa/package.json ./LandStack-Spa/package-lock.json ./
RUN npm install

COPY ./LandStack-Spa/ ./

RUN npm run build -- --output-path=./dist/out --configuration production 

### DEPLOY ###
FROM nginx:alpine
RUN rm -rf /usr/share/nginx/html/*
COPY --from=build-env /app/dist/out /usr/share/nginx/html
COPY ./.docker/config/nginx.conf /etc/nginx/conf.d/default.conf
