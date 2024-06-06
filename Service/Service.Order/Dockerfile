FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service/Service.Order/Service.Order.csproj", "Service.Order/"]
RUN dotnet restore "Service/Service.Order/Service.Order.csproj"
COPY . .
WORKDIR "/src/Service.Order"
RUN dotnet build "Service.Order.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Order.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Order.dll"]
