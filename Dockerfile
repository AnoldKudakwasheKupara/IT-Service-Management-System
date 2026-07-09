# Multi-stage build for the IT Service Management System (ASP.NET Core, .NET 10)
# Build:  docker build -t itsm .
# Run:    docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." itsm
#
# NOTE: optional OCR (Tesseract) and PDF-to-image (PDFium) features need extra
# native packages in the runtime image (e.g. apt-get install -y libleptonica-dev
# libtesseract-dev + a tessdata volume). The core app runs without them
# (PlainText OCR is the default; QuestPDF is fully managed).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) — copy the manifests only.
COPY ["global.json", "./"]
COPY ["IT Service Management System/IT Service Management System.csproj", "IT Service Management System/"]
RUN dotnet restore "IT Service Management System/IT Service Management System.csproj"

# Copy the rest and publish.
COPY . .
RUN dotnet publish "IT Service Management System/IT Service Management System.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Non-root runtime user (the aspnet image ships an 'app' user).
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "IT Service Management System.dll"]
