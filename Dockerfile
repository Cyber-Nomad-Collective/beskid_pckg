FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Toggle verbose diagnosis during image build
ARG VERBOSE_BUILD=false

COPY Directory.Build.props src/
COPY src/ src/

# Optional diagnostics to confirm SDK and project content inside the image
RUN if [ "$VERBOSE_BUILD" = "true" ]; then \
      echo "--- dotnet --info ---" && dotnet --info && \
      echo "--- Listing /src/src/Server/Features ---" && ls -la src/Server/Features || true && \
      echo "--- Tree under /src/src/Server/Features (depth 3) ---" && find src/Server/Features -maxdepth 3 -type f -print | sort || true && \
      echo "--- Server.csproj (first 200 lines) ---" && sed -n '1,200p' src/Server/Server.csproj; \
    fi

RUN dotnet restore src/Server/Server.csproj

# When VERBOSE_BUILD=true, also print the Compile item list from MSBuild
RUN if [ "$VERBOSE_BUILD" = "true" ]; then \
      dotnet publish src/Server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false /p:LogCompileItems=true /p:TreatWarningsAsErrors=false --no-restore -v normal; \
    else \
      dotnet publish src/Server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false /p:TreatWarningsAsErrors=false --no-restore -v minimal; \
    fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV HTTP_PORTS=8082 \
    Logging__LogLevel__Default=Information
EXPOSE 8082

COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/packages

ENTRYPOINT ["dotnet", "Server.dll"]
