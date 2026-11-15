# Contexto de build: raiz do repositorio (docker build -f src/Conciliador.Api/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia so os .csproj primeiro para aproveitar o cache da restauracao
COPY Directory.Build.props ./
COPY src/Conciliador.Domain/Conciliador.Domain.csproj src/Conciliador.Domain/
COPY src/Conciliador.Application/Conciliador.Application.csproj src/Conciliador.Application/
COPY src/Conciliador.Infrastructure/Conciliador.Infrastructure.csproj src/Conciliador.Infrastructure/
COPY src/Conciliador.Api/Conciliador.Api.csproj src/Conciliador.Api/
RUN dotnet restore src/Conciliador.Api/Conciliador.Api.csproj

COPY src/ src/
RUN dotnet publish src/Conciliador.Api/Conciliador.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Usuario sem privilegios que ja vem na imagem oficial
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Conciliador.Api.dll"]
