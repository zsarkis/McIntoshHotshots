FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY McIntoshHotshots/McIntoshHotshots.csproj ./McIntoshHotshots/
WORKDIR /src/McIntoshHotshots
COPY McIntoshHotshots/. ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "McIntoshHotshots.dll"]
