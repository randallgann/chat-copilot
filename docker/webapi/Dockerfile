# 1) Base runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# 2) Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
# generate dev-certs for https
RUN dotnet dev-certs https

# Copy csproj files, then restore
COPY webapi/CopilotChatWebApi.csproj webapi/
COPY webapi/nuget.config webapi/
COPY shared/CopilotChatShared.csproj shared/
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Create a custom NuGet.config that enables preview packages
RUN echo '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping><config><add key="globalPackagesFolder" value="/src/nuget-packages" /><add key="includePrerelease" value="true" /></config><packageRestore><add key="enabled" value="True" /><add key="automatic" value="True" /></packageRestore></configuration>' > /src/NuGet.config

RUN dotnet restore webapi/CopilotChatWebApi.csproj --configfile "/src/NuGet.config"

# Copy the rest of your source code
COPY webapi/ webapi/
COPY shared/ shared/

WORKDIR /src/webapi
RUN dotnet build CopilotChatWebApi.csproj -c $configuration -o /app/build

# 3) Publish stage
FROM build AS publish
ARG configuration=Release
RUN dotnet publish CopilotChatWebApi.csproj -c $configuration -o /app/publish /p:UseAppHost=false

# 4) Final runtime image
FROM base AS final
ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /root/.dotnet/corefx/cryptography/x509stores/my/* /root/.dotnet/corefx/cryptography/x509stores/my/
ENTRYPOINT ["dotnet", "CopilotChatWebApi.dll"]