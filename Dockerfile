FROM mcr.microsoft.com/dotnet/core/sdk:2.2 as builder

RUN mkdir -p /app

WORKDIR /app

COPY gnet-dump.csproj . 
RUN dotnet restore ./gnet-dump.csproj

COPY . .

RUN dotnet publish -c release -o published

FROM mcr.microsoft.com/dotnet/core/runtime:2.2.5-alpine3.9

COPY --from=builder /app/published .

ENTRYPOINT [ "dotnet", "gnet-dump.dll" ]
CMD [ "--", "-h" ]