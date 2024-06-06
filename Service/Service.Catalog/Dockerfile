FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service/Service.Catalog/Service.Catalog.csproj", "Service.Catalog/"]
RUN dotnet restore "Service/Service.Catalog/Service.Catalog.csproj"
COPY . .
WORKDIR "/src/Service.Catalog"
RUN dotnet build "Service.Catalog.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Catalog.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Catalog.dll"]
