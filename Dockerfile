# =============================
# Build stage
# =============================
FROM dev-1.dev.local:5000/dotnet-sdk:8.0 AS build
WORKDIR /src


# Copy only the csproj first (for faster restores)
COPY IfsahApp/IfsahApp.csproj IfsahApp/
RUN dotnet restore IfsahApp/IfsahApp.csproj

# Copy everything else
COPY . .

# Build and publish the app
WORKDIR /src/IfsahApp
RUN dotnet publish -c Release -o /app/publish

# =============================
# Runtime stage
# =============================
FROM dev-1.dev.local:5000/dotnet-aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose port
EXPOSE 5000

ENTRYPOINT ["dotnet", "IfsahApp.dll"]
