FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci --legacy-peer-deps
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
# Template en vez de config estática: nginx:alpine ya trae envsubst nativo para
# /etc/nginx/templates/*.template -> /etc/nginx/conf.d/*.conf al arrancar, sustituyendo
# PORT y API_URL. Necesario porque en Cloud Run no existe el nombre "api" de Docker Compose
# (cada servicio tiene su propia URL) y el puerto lo asigna Cloud Run en runtime via $PORT.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
ENV PORT=80
ENV API_URL=http://api:80
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
