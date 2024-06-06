FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service/Service.Notification/Service.Notification.csproj", "Service.Notification/"]
RUN dotnet restore "Service/Service.Notification/Service.Notification.csproj"
COPY . .
WORKDIR "/src/Service.Notification"
RUN dotnet build "Service.Notification.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Notification.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Notification.dll"]
