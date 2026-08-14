# Billing Application | .NET Console System 💳📄

![C#](https://img.shields.io/badge/C%23-.NET_8-purple?style=for-the-badge&logo=csharp)
![Dapper](https://img.shields.io/badge/Dapper-Micro--ORM-blue?style=for-the-badge)
![SQL Server](https://img.shields.io/badge/SQL_Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver)
![Tests](https://img.shields.io/badge/Tests-58%20passing-brightgreen?style=for-the-badge)
![Coverage](https://img.shields.io/badge/Coverage-100%25-brightgreen?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)
![CI](https://img.shields.io/github/actions/workflow/status/Lantieridev/billingApplication/ci.yml?branch=billing-console&style=for-the-badge&label=CI)

A console-based billing management system: create invoices, manage a product catalog with stock tracking, and manage customers. Built to demonstrate the Repository pattern and a clean, layered architecture in C#.

## 🚀 Features
- **Invoices**: create a full invoice (customer + payment method + line items), with per-line stock validation.
- **Products**: catalog listing with unit price and stock.
- **Customers**: listing with contact info.

## 🏗️ Architecture

```
Domain/     — POCO entities (Customer, Product, Invoice, InvoiceDetail, PaymentMethod)
Data/
  Interfaces/  — IRepository<T> + per-entity repository contracts
  Repository/  — Dapper-based repository implementations
  DapperContext.cs — connection factory, reads the connection string from appsettings.json
Services/
  Interfaces/  — IInvoiceService
  InvoiceService.cs — invoice creation logic (total calculation, stock deduction)
Program.cs  — console menu (composition root: wires DI, drives the loop)
```

Repositories are injected via `Microsoft.Extensions.DependencyInjection` — no ORM tracking, no magic; every query is explicit Dapper SQL.

## ⚙️ Setup

1. **Database**: run `billingApplicationsql.sql` against your SQL Server instance to create the `BillingSystem` schema.
2. **Connection string**: `appsettings.json` defaults to a local `(localdb)\MSSQLLocalDB` instance — point `ConnectionStrings:DefaultConnection` at your own server if you're not using LocalDB.
3. **Run**:
   ```powershell
   dotnet restore
   dotnet run --project billingApplication.csproj
   ```

## ✅ Testing

```powershell
dotnet test
```

58 tests: repository integration tests against a real, disposable SQL Server instance via [Testcontainers](https://testcontainers.com/) (requires Docker running locally), plus unit tests for `InvoiceService`'s business logic (total calculation, stock deduction) and the console UI's input-handling loop. **100% line/branch/method coverage**, enforced on every run via `coverlet.msbuild` — a coverage regression fails the build, same as CI.

## 📜 Academic Context
University project focused on the Repository pattern and separation of concerns (Data / Domain / Services) in C#.

---
*Developed by [Martin Lantieri](https://github.com/Lantieridev) - 2026*
