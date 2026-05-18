FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Api.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
RUN apk add --no-cache curl
COPY --from=build /app .
RUN adduser --disabled-password --uid 10001 appuser \
	&& mkdir -p /app/Data /app/Logs \
	&& chown -R appuser:appuser /app
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "GroceryStore.Chatbot.Api.dll"]
