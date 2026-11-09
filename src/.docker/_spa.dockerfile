### BUILD ###
FROM node:24.22.2-alpine AS build-env
WORKDIR /app

RUN npm install --global npm@12.0.2 --no-audit --no-fund

COPY ./LandStack-Spa/package.json ./LandStack-Spa/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY ./LandStack-Spa/angular.json ./LandStack-Spa/tsconfig*.json ./
COPY ./LandStack-Spa/src/ ./src/

RUN npm run build -- --output-path=./dist/out --configuration production

### DEPLOY ###
FROM nginx:alpine
RUN rm -rf /usr/share/nginx/html/*
COPY --from=build-env /app/dist/out/browser /usr/share/nginx/html
COPY ./.docker/config/nginx.conf /etc/nginx/conf.d/default.conf
