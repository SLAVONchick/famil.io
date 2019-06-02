FROM mcr.microsoft.com/dotnet/core/runtime:2.2-alpine
RUN mkdir /wwwroot/
ADD deploy/Client /wwwroot/Client
ADD deploy/Server /wwwroot/Server
WORKDIR /wwwroot/Server/
RUN cd /wwwroot/Server/
CMD ["dotnet" ,"Server.dll"]
