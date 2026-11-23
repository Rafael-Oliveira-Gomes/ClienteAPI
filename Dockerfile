FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GumpStudio.Api/GumpStudio.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Arquivos privados e chaves de Data Protection ficam em volume, fora da imagem.
RUN mkdir -p /data/storage /data/keys && chown -R $APP_UID /data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__RootPath=/data/storage \
    DataProtection__KeysPath=/data/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "GumpStudio.Api.dll"]
