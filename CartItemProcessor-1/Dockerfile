# #See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
# WORKDIR /app

# # should be a comma-delimited list
# ENV CLUSTER__IP ""
# ENV CLUSTER__PORT "9446"

# EXPOSE 9446

# FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
# WORKDIR /src
# COPY ["CartItemProcessor-1/CartItemProcessor-1.csproj", "CartItemProcessor-1/"]
# COPY ["Akka.Cluster.Infra/Akka.Cluster.Infra.csproj", "Akka.Cluster.Infra/"]
# RUN dotnet restore "CartItemProcessor-1/CartItemProcessor-1.csproj"
# COPY . .
# WORKDIR "/src/CartItemProcessor-1"
# RUN dotnet build "CartItemProcessor-1.csproj" -c Release -o /app/build

# FROM build AS publish
# RUN dotnet publish "CartItemProcessor-1.csproj" -c Release -o /app/publish /p:UseAppHost=false

# FROM base AS final
# WORKDIR /app
# COPY --from=publish /app/publish .

# # Install Petabridge.Cmd client
# RUN dotnet tool install --global pbm 
# # copy .NET Core global tool
# COPY --from=base /root/.dotnet /root/.dotnet/

# # Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
# ENV PATH="${PATH}:/root/.dotnet/tools"

# ENTRYPOINT ["dotnet", "CartItemProcessor-1.dll"]


FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
WORKDIR /app

# should be a comma-delimited list
ENV CLUSTER__IP ""
ENV CLUSTER__PORT "9446"

#Akka.Remote inbound listening endpoint
EXPOSE 80
EXPOSE 9446 

COPY ./bin/Release/net6.0/publish/ /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS app
WORKDIR /app

COPY --from=base /app /app

# copy .NET Core global tool
COPY --from=base /root/.dotnet /root/.dotnet/

# Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
ENV PATH="${PATH}:/root/.dotnet/tools"

CMD ["dotnet", "CartItemProcessor-1.dll"]
