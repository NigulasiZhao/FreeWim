# ---------------- base ----------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:9940

# 时区
RUN ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
 && echo 'Asia/Shanghai' >/etc/timezone

# apt 国内源
RUN sed -i 's|deb.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources \
 && sed -i 's|security.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources

# speedtest
RUN apt-get update && \
    apt-get install -y curl gnupg && \
    curl -s https://packagecloud.io/install/repositories/ookla/speedtest-cli/script.deb.sh | bash && \
    apt-get install -y speedtest && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# ---------------- build ----------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY NuGet.config ./
COPY ["FreeWim/FreeWim.csproj", "FreeWim/"]

RUN dotnet restore "FreeWim/FreeWim.csproj" --disable-parallel

COPY . .
WORKDIR /src/FreeWim
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

# ---------------- publish ----------------
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---------------- final ----------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FreeWim.dll"]
