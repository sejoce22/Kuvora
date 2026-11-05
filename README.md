# Kuvora

[![CI](https://github.com/sejoce22/Kuvora/actions/workflows/ci.yml/badge.svg)](https://github.com/sejoce22/Kuvora/actions/workflows/ci.yml)

Software de gestión para restaurantes pensado como SaaS multi-local: punto de venta, mesas, comandas, pantalla de cocina en tiempo real, cobros y reportes.

> Proyecto en desarrollo. Actualmente en la Fase 1 del MVP (base de la API).

## Stack

- **Backend:** .NET 10 Web API, EF Core (code-first) con Npgsql, Serilog, ProblemDetails, OpenAPI
- **Base de datos:** PostgreSQL 18 (nombres en snake_case)
- **Frontend (próximamente):** Angular con componentes standalone, signals y TypeScript estricto
- **Calidad:** xUnit v3, GitHub Actions

## Arquitectura

Monolito modular en capas, con la dependencia dirigida hacia el dominio:

    Api → Infrastructure → Application → Domain

| Capa | Responsabilidad |
|---|---|
| Domain | Entidades y reglas de negocio, sin dependencias externas |
| Application | Casos de uso, DTOs e interfaces (por ejemplo `IAppDbContext`) |
| Infrastructure | EF Core, PostgreSQL, migraciones |
| Api | Controladores, manejo de errores, logging |

## Decisiones de diseño

- Multi-tenant en una sola base, con `TenantId` en cada tabla y filtros globales de EF Core
- Claves primarias `Guid` v7 (ordenables en el tiempo, generables sin consultar la base)
- Fechas siempre en UTC (`DateTimeOffset`)
- Dinero como entero (`long`) en pesos chilenos
- Errores uniformes con `ProblemDetails` (RFC 9457)

## Cómo ejecutarlo en local

Requisitos: .NET 10 SDK y PostgreSQL 18.

1. Crea la base de datos y un usuario para la aplicación.
2. Guarda la cadena de conexión en los user secrets:

       cd backend
       dotnet user-secrets init --project src/Kuvora.Api
       dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=kuvora_dev;Username=<usuario>;Password=<clave>" --project src/Kuvora.Api

3. Aplica las migraciones y ejecuta la API:

       dotnet ef database update --project src/Kuvora.Infrastructure --startup-project src/Kuvora.Api
       dotnet run --project src/Kuvora.Api

4. Ejecuta las pruebas:

       dotnet test

## Hoja de ruta

- [x] Fase 0: entorno
- [x] Fase 1: base de la API (capas, EF Core, errores, logs, pruebas, CI)
- [ ] Fase 2: autenticación y multi-tenant
- [ ] Fase 3: catálogo y mesas
- [ ] Fase 4: pedidos y comandas
- [ ] Fase 5: cocina en tiempo real
- [ ] Fase 6: cobros y panel de ventas
- [ ] Fase 7: pulido y portafolio