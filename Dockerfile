FROM node:24-alpine AS client
WORKDIR /build/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
COPY content/ /build/content/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /build
COPY global.json Directory.*.props ./
COPY src/Risk.Sim/*.csproj src/Risk.Sim/
COPY src/Risk.Server/*.csproj src/Risk.Server/
RUN dotnet restore src/Risk.Server
COPY src/ src/
COPY content/ content/
RUN dotnet publish src/Risk.Server -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /out/ ./
COPY --from=client /build/client/dist/ wwwroot/
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Risk.Server.dll"]
