# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and publish
COPY . ./
RUN dotnet publish -c Release -o out

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./

# Render uses PORT env variable
ENV ASPNETCORE_URLS=http://+:$PORT

EXPOSE 8080
ENTRYPOINT ["dotnet", "NewsPortal.API.dll"]
