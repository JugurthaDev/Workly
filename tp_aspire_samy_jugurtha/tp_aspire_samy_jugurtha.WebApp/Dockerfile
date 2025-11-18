# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore
COPY tp_aspire_samy_jugurtha/tp_aspire_samy_jugurtha.sln ./
COPY tp_aspire_samy_jugurtha/tp_aspire_samy_jugurtha.WebApp/*.csproj ./tp_aspire_samy_jugurtha.WebApp/
COPY tp_aspire_samy_jugurtha/tp_aspire_samy_jugurtha.ServiceDefaults/*.csproj ./tp_aspire_samy_jugurtha.ServiceDefaults/

RUN dotnet restore ./tp_aspire_samy_jugurtha.WebApp/tp_aspire_samy_jugurtha.WebApp.csproj

# Copy the rest of the source
COPY tp_aspire_samy_jugurtha ./

RUN dotnet publish ./tp_aspire_samy_jugurtha.WebApp/tp_aspire_samy_jugurtha.WebApp.csproj -c Release -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "tp_aspire_samy_jugurtha.WebApp.dll"]
