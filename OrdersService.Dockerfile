FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

COPY wait-for-it.sh .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["OrdersService/OrdersService.csproj", "OrdersService/"]

RUN dotnet restore "OrdersService/OrdersService.csproj"

COPY . .

WORKDIR "/src/OrdersService"

RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS publish
WORKDIR /app
COPY --from=build /out .
COPY --from=build /src/wait-for-it.sh ./wait-for-it.sh
ENTRYPOINT ["./wait-for-it.sh", "kafka:29092", "--timeout=15", "--", "dotnet", "OrdersService.dll"]