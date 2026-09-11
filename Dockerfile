FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY WorkoutTracker.Api/WorkoutTracker.Api.csproj WorkoutTracker.Api/
COPY WorkoutTracker.Shared/WorkoutTracker.Shared.csproj WorkoutTracker.Shared/
RUN dotnet restore WorkoutTracker.Api/WorkoutTracker.Api.csproj
COPY WorkoutTracker.Api/ WorkoutTracker.Api/
COPY WorkoutTracker.Shared/ WorkoutTracker.Shared/
RUN dotnet publish WorkoutTracker.Api/WorkoutTracker.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Cloud Run injects PORT; ASP.NET Core needs to bind to it explicitly.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "WorkoutTracker.Api.dll"]
