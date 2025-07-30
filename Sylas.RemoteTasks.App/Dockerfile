# FROM mcr.microsoft.com/dotnet/sdk:9.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
MAINTAINER iduo admin@iduo.cc

RUN ln -s /lib/x86_64-linux-gnu/libdl-2.24.so /lib/x86_64-linux-gnu/libdl.so

RUN mv /etc/apt/sources.list /etc/apt/sources.list.bak; echo "deb https://mirrors.bfsu.edu.cn/debian/ bookworm main contrib non-free non-free-firmware" > /etc/apt/sources.list; echo "deb https://mirrors.bfsu.edu.cn/debian/ bookworm-updates main contrib non-free non-free-firmware" >> /etc/apt/sources.list; echo "deb https://mirrors.bfsu.edu.cn/debian/ bookworm-backports main contrib non-free non-free-firmware" >> /etc/apt/sources.list; echo "deb https://mirrors.bfsu.edu.cn/debian-security bookworm-security main contrib non-free non-free-firmware" /etc/apt/sources.list

RUN apt-get update
RUN apt-get install vim -y
# RUN dotnet dev-certs --trust

ENV TZ "Asia/Shanghai"

# ENV ASPNETCORE_URLS="http://+:5105;https://+:7166"
ENV ASPNETCORE_URLS="http://+:80;https://+:443"

WORKDIR /app
EXPOSE 80
COPY ./ .

ENTRYPOINT ["dotnet", "Sylas.RemoteTasks.App.dll"] # ENTRYPOINT 可以在docker run运行容器的时候继续追加命令, 如 --urls=http... --environment=Development