# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY CTSHIPDashboard.csproj ./
RUN dotnet restore CTSHIPDashboard.csproj --arch "$TARGETARCH" -p:ContainerPublish=true

COPY . ./
RUN dotnet publish CTSHIPDashboard.csproj \
    --configuration Release \
    --arch "$TARGETARCH" \
    --no-self-contained \
    --no-restore \
    -p:ContainerPublish=true \
    -p:DebugType=None \
    --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CTSHIPDashboard.dll"]
