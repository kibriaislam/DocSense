FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RagApi.sln ./
COPY RagApi/RagApi.csproj RagApi/
COPY RagApi.Application/RagApi.Application.csproj RagApi.Application/
COPY RagApi.Domain/RagApi.Domain.csproj RagApi.Domain/
COPY RagApi.Infrastructure/RagApi.Infrastructure.csproj RagApi.Infrastructure/

RUN dotnet restore RagApi.sln

COPY . .
RUN dotnet publish RagApi/RagApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RagApi.dll"]
