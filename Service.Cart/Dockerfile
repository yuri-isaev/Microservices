FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Service.Cart/Service.Cart.csproj", "Service.Cart/"]
RUN dotnet restore "Service.Cart/Service.Cart.csproj"
COPY . .
WORKDIR "/src/Service.Cart"
RUN dotnet build "Service.Cart.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Service.Cart.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Service.Cart.dll"]
