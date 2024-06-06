FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service.Identity/Service.Identity.csproj", "Service.Identity/"]
RUN dotnet restore "Service.Identity/Service.Identity.csproj"
COPY . .
WORKDIR "/src/Service.Identity"
RUN dotnet build "Service.Identity.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Identity.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Identity.dll"]
