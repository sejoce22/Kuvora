# Kuvora

[![CI](https://github.com/sejoce22/Kuvora/actions/workflows/ci.yml/badge.svg)](https://github.com/sejoce22/Kuvora/actions/workflows/ci.yml)

Software de gestión para restaurantes pensado como SaaS multi-local: punto de venta, mesas, comandas, pantalla de cocina en tiempo real, cobros, inventario y reportes.

> Proyecto en desarrollo. Fases 0 a 3 completas y backend de la Fase 4 (pedidos y comandas) terminado; el frontend de pedidos está en construcción.

## Qué funciona hoy

- **Autenticación y roles:** registro de restaurante, login con JWT de 15 minutos y refresh token en cookie HttpOnly (con rotación y detección de reutilización). Roles: Admin, Mesero, Cocina y Cajero.
- **Multi-tenant real:** cada restaurante solo ve sus datos (filtro global por `TenantId`), con pruebas de aislamiento contra PostgreSQL.
- **Catálogo y mesas:** categorías, productos y mesas por zona, con desactivación en vez de borrado y reglas de negocio.
- **Pedidos (API):** un pedido abierto por mesa, ítems por comensal, envío a cocina por rondas (seguro ante doble clic), cancelaciones con motivo y permisos por rol, número de pedido consecutivo por restaurante y control de concurrencia.
- **Frontend en Angular:** login, sesión restaurada al recargar, navegación por rol, usuarios, catálogo y mesas.

## Stack

- **Backend:** .NET 10 Web API, EF Core (code-first) con Npgsql, Serilog, ProblemDetails, OpenAPI
- **Base de datos:** PostgreSQL (nombres en snake_case)
- **Frontend:** Angular 22 con componentes standalone, signals, sin zone.js y TypeScript estricto
- **Calidad:** xUnit v3 (pruebas de dominio y de integración contra PostgreSQL real), Vitest en Angular, GitHub Actions

## Arquitectura

Monolito modular en capas, con la dependencia dirigida hacia el dominio:

    Api → Infrastructure → Application → Domain

| Capa | Responsabilidad |
|---|---|
| Domain | Entidades y reglas de negocio, sin dependencias externas |
| Application | Casos de uso, DTOs e interfaces (por ejemplo `IAppDbContext`) |
| Infrastructure | EF Core, PostgreSQL, migraciones, seguridad |
| Api | Controladores, autorización, manejo de errores, logging |

## Decisiones de diseño

- Multi-tenant en una sola base, con `TenantId` en cada tabla y filtros globales de EF Core
- Claves primarias `Guid` v7 (ordenables en el tiempo, generables sin consultar la base)
- Fechas siempre en UTC (`DateTimeOffset`)
- Dinero como entero (`long`) en pesos chilenos
- Errores uniformes con `ProblemDetails` (RFC 9457); los mensajes de negocio llegan al cliente, los técnicos no
- Refresh token en cookie HttpOnly y access token solo en memoria
- Reglas críticas protegidas también por la base de datos: índice único parcial (un pedido abierto por mesa), contador atómico para el número de pedido y concurrencia optimista con `xmin`
- El ítem del pedido guarda una copia del nombre y del precio al momento de la venta

## Cómo ejecutarlo en local

Requisitos: .NET 10 SDK, PostgreSQL y Node.js (para el frontend).

1. Crea la base de datos y un usuario para la aplicación.
2. Guarda la cadena de conexión y la clave de firma del JWT en los user secrets:

       cd backend
       dotnet user-secrets init --project src/Kuvora.Api
       dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=kuvora_dev;Username=<usuario>;Password=<clave>" --project src/Kuvora.Api
       dotnet user-secrets set "Jwt:SigningKey" "<clave-aleatoria-de-al-menos-32-caracteres>" --project src/Kuvora.Api

3. Aplica las migraciones y ejecuta la API (perfil https, puerto 7052):

       dotnet ef database update --project src/Kuvora.Infrastructure --startup-project src/Kuvora.Api
       dotnet run --project src/Kuvora.Api --launch-profile https

4. En otra terminal, ejecuta el frontend (usa un proxy `/api` hacia la API):

       cd frontend
       npm install
       ng serve

   Abre `http://localhost:4200`.

5. Pruebas del backend. Las de integración necesitan una base vacía aparte y la variable de entorno `KUVORA_TEST_DB` con su cadena de conexión:

       cd backend
       dotnet test

   Pruebas del frontend: `ng test` (dentro de `frontend`).

## Hoja de ruta

- [x] Fase 0: entorno
- [x] Fase 1: base de la API (capas, EF Core, errores, logs, pruebas, CI)
- [x] Fase 2: autenticación y multi-tenant
- [x] Fase 3: catálogo y mesas
- [ ] Fase 4: pedidos y comandas (API lista; falta el frontend y la comanda imprimible)
- [ ] Fase 4b: alérgenos, cambiar y unir mesas, paleta de comandos, asistente de configuración
- [ ] Fase 5: cocina en tiempo real
- [ ] Fase 6: cobros y caja
- [ ] Fase 7: inventario e insumos
- [ ] Fase 8: reportes y panel
- [ ] Fase 9: pulido y portafolio
