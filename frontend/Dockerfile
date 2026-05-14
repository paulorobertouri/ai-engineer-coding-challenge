FROM node:22-alpine3.22 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:stable-alpine3.21
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
RUN addgroup -S appgroup \
	&& adduser -S appuser -G appgroup -u 10001 \
	&& chown -R appuser:appgroup /usr/share/nginx/html /var/cache/nginx /var/run /var/log/nginx \
	&& chmod +x /docker-entrypoint.d/40-runtime-config.sh
USER appuser
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
