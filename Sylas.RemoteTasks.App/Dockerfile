# FROM mcr.microsoft.com/dotnet/sdk:7.0
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
MAINTAINER iduo admin@iduo.cc

RUN ln -s /lib/x86_64-linux-gnu/libdl-2.24.so /lib/x86_64-linux-gnu/libdl.so

RUN echo  'deb http://mirrors.ustc.edu.cn/debian buster main contrib non-free \ndeb http://mirrors.ustc.edu.cn/debian buster-backports main contrib non-free \ndeb http://mirrors.ustc.edu.cn/debian buster-proposed-updates main contrib non-free \ndeb http://mirrors.ustc.edu.cn/debian-security buster/updates main contrib non-free' > /etc/apt/sources.list

# RUN mv /etc/apt/sources.list /etc/apt/sources.list.bak && \
#    echo "deb http://mirrors.aliyun.com/debian/ buster main non-free contrib" >>/etc/apt/sources.list && \
#    echo "deb-src http://mirrors.aliyun.com/debian/ buster main non-free contrib" >>/etc/apt/sources.list && \
#    echo "deb http://mirrors.aliyun.com/debian-security buster/updates main" >>/etc/apt/sources.list && \
#    echo "deb-src http://mirrors.aliyun.com/debian-security buster/updates main" >>/etc/apt/sources.list && \
#    echo "deb http://mirrors.aliyun.com/debian/ buster-updates main non-free contrib" >>/etc/apt/sources.list && \
#    echo "deb-src http://mirrors.aliyun.com/debian/ buster-updates main non-free contrib" >>/etc/apt/sources.list && \
#    echo "deb http://mirrors.aliyun.com/debian/ buster-backports main non-free contrib" >>/etc/apt/sources.list && \
#    echo "deb-src http://mirrors.aliyun.com/debian/ buster-backports main non-free contrib" >>/etc/apt/sources.list

RUN apt-get update
RUN apt-get install vim -y
# RUN dotnet dev-certs --trust

ENV TZ "Asia/Shanghai"

ENV ASPNETCORE_URLS="http://+:80;https://+:443" # ENV ASPNETCORE_URLS="http://+:80;https://+:7166"

WORKDIR /app
EXPOSE 80
COPY ./ .

ENTRYPOINT ["dotnet", "Sylas.RemoteTasks.App.dll"] # ENTRYPOINT 可以在docker run运行容器的时候继续追加命令, 如 --urls=http... --environment=Development