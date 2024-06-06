FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service.Payment/Service.Payment.csproj", "Service.Payment/"]
RUN dotnet restore "Service.Payment/Service.Payment.csproj"
COPY . .
WORKDIR "/src/Service.Payment"
RUN dotnet build "Service.Payment.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Payment.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Payment.dll"]
