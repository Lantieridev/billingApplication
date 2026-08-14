USE [master]
GO

IF DB_ID(N'BillingSystem') IS NULL
BEGIN
    CREATE DATABASE [BillingSystem];
END
GO

USE [BillingSystem]
GO

IF OBJECT_ID(N'dbo.DetallesFactura', N'U') IS NOT NULL DROP TABLE dbo.DetallesFactura;
IF OBJECT_ID(N'dbo.Facturas', N'U') IS NOT NULL DROP TABLE dbo.Facturas;
IF OBJECT_ID(N'dbo.Articulos', N'U') IS NOT NULL DROP TABLE dbo.Articulos;
IF OBJECT_ID(N'dbo.Clientes', N'U') IS NOT NULL DROP TABLE dbo.Clientes;
IF OBJECT_ID(N'dbo.FormasPago', N'U') IS NOT NULL DROP TABLE dbo.FormasPago;
GO

CREATE TABLE dbo.Clientes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(150)   NOT NULL,
    Direccion   NVARCHAR(250)   NULL,
    Telefono    NVARCHAR(50)    NULL,
    Email       NVARCHAR(150)   NULL,
    Activo      BIT             NOT NULL DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Clientes_Email ON dbo.Clientes(Email) WHERE Email IS NOT NULL;
GO

CREATE TABLE dbo.Articulos (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Codigo          NVARCHAR(50)    NOT NULL,
    Nombre          NVARCHAR(150)   NOT NULL,
    Descripcion     NVARCHAR(500)   NULL,
    PrecioUnitario  DECIMAL(18,2)   NOT NULL CHECK (PrecioUnitario >= 0),
    Stock           INT             NOT NULL CHECK (Stock >= 0),
    Activo          BIT             NOT NULL DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Articulos_Codigo ON dbo.Articulos(Codigo);
GO

CREATE TABLE dbo.FormasPago (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Nombre  NVARCHAR(100)   NOT NULL,
    Activo  BIT             NOT NULL DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_FormasPago_Nombre ON dbo.FormasPago(Nombre);
GO

CREATE TABLE dbo.Facturas (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    NumeroFactura   NVARCHAR(50)    NOT NULL,
    Fecha           DATETIME2       NOT NULL DEFAULT (SYSDATETIME()),
    ClienteId       INT             NOT NULL REFERENCES dbo.Clientes(Id),
    FormaPagoId     INT             NOT NULL REFERENCES dbo.FormasPago(Id),
    Subtotal        DECIMAL(18,2)   NOT NULL CHECK (Subtotal >= 0),
    Total           DECIMAL(18,2)   NOT NULL CHECK (Total >= 0)
);
GO

CREATE UNIQUE INDEX UX_Facturas_NumeroFactura ON dbo.Facturas(NumeroFactura);
GO

CREATE TABLE dbo.DetallesFactura (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FacturaId       INT             NOT NULL REFERENCES dbo.Facturas(Id),
    ArticuloId      INT             NOT NULL REFERENCES dbo.Articulos(Id),
    Cantidad        INT             NOT NULL CHECK (Cantidad > 0),
    PrecioUnidad    DECIMAL(18,2)   NOT NULL CHECK (PrecioUnidad >= 0),
    Subtotal        DECIMAL(18,2)   NOT NULL CHECK (Subtotal >= 0)
);
GO

CREATE INDEX IX_DetallesFactura_FacturaId ON dbo.DetallesFactura(FacturaId);
GO
