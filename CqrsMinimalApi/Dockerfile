# Production image for CqrsMinimalApi demonstrating Wolverine 6 static code generation.
#
# The goal (GH-2900): pre-generate Wolverine's code at build time, then publish in Release WITHOUT
# the WolverineFx.RuntimeCompilation package, so the production image carries NO Roslyn. At runtime
# the app uses TypeLoadMode.Static (configured in Program.cs via CritterStackDefaults.Production).
#
# Build from this project's directory:
#     docker build -t cqrs-minimal-api -f Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

# 1) Pre-generate Wolverine's HTTP/handler code into Internal/Generated.
#    Runs in Debug so the *Debug-only* WolverineFx.RuntimeCompilation package (Roslyn) is present.
#    `codegen write` boots the host in metadata-only mode, so NO database is required here.
RUN dotnet run -c Debug --project CqrsMinimalApi.csproj -- codegen write

# 2) Publish in Release. WolverineFx.RuntimeCompilation is referenced only in Debug (see the
#    .csproj), so the published output ships WITHOUT Roslyn — the app runs the pre-generated code.
RUN dotnet publish CqrsMinimalApi.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish .

# Production environment activates CritterStackDefaults.Production -> TypeLoadMode.Static
# (+ AssertAllPreGeneratedTypesExist), so a missing pre-generated type fails fast at startup.
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CqrsMinimalApi.dll"]
