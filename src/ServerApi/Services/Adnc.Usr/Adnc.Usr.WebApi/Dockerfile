#adnc.usr.webapi
#使用asp.net 5作为基础镜像，起一个别名为base
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
#设置容器的工作目录为/app
WORKDIR /app
#COPY 文件
COPY . /app
#设置ASPNETCORE_ENVIRONMENT
ENV ASPNETCORE_ENVIRONMENT production
#配置本服务在skywalking名称
ENV SKYWALKING__SERVICENAME=adnc-usr-webapi
#skywalking接管的意思，除了adnc_health不需要配置，其他4个必须配置
ENV ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=SkyAPM.Agent.AspNetCore
#设置时区为东八区
ENV TZ Asia/Shanghai
#启动服务
ENTRYPOINT ["dotnet", "Adnc.Usr.WebApi.dll"]