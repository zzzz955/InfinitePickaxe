FROM node:20-alpine

WORKDIR /app

# Install dependencies (production only)
COPY package*.json ./
RUN npm install --omit=dev

# Copy source
COPY src ./src

EXPOSE 10000

CMD ["node", "src/app.js"]
