/* =====================================================================
   Corteza ERP · Esquema de referencia (SQL Server 2022 / Azure SQL)
   ---------------------------------------------------------------------
   ⚠️ NO es la fuente de verdad. El esquema real lo generan las
   migraciones de EF Core (api/src/Corteza.Infrastructure). Este archivo
   sirve para leer el modelo, revisarlo en clase o en SSMS.
   Para ver el SQL exacto que aplicará EF:
     dotnet ef migrations script -p src/Corteza.Infrastructure -s src/Corteza.Api
   Convenciones: tablas en plural PascalCase, dinero decimal(14,2),
   fechas con hora en UTC (datetime2), enums como texto + CHECK,
   borrado lógico con IsActive.
   ===================================================================== */

-- CREATE DATABASE Corteza COLLATE Modern_Spanish_CI_AI;  -- sin distinguir mayúsculas ni tildes
-- GO
-- USE Corteza;

/* ---------- Secuencias para códigos de negocio ---------- */
CREATE SEQUENCE dbo.CustomerCodeSeq  AS int START WITH 1;
CREATE SEQUENCE dbo.OrderCodeSeq     AS int START WITH 1;
CREATE SEQUENCE dbo.SalesInvoiceSeq  AS int START WITH 1;
CREATE SEQUENCE dbo.PurchaseOrderSeq AS int START WITH 1000;
CREATE SEQUENCE dbo.PayslipCodeSeq   AS int START WITH 1;

/* ========== SEGURIDAD ========== */
CREATE TABLE Roles (
  Id    int          NOT NULL PRIMARY KEY,
  Code  nvarchar(30) NOT NULL CONSTRAINT UX_Roles_Code UNIQUE,  -- ADMIN, CRM, SALES, INVENTORY, SUPPLIERS, PURCHASES, HR
  Name  nvarchar(60) NOT NULL
);

/* ========== TALENTO HUMANO ========== */
CREATE TABLE Employees (
  Id                 int IDENTITY PRIMARY KEY,
  FirstNames         nvarchar(80)  NOT NULL,
  LastNames          nvarchar(80)  NOT NULL,
  DocumentType       nvarchar(20)  NOT NULL CONSTRAINT CK_Employees_DocumentType CHECK (DocumentType IN ('CC','CE','PP','TI')),
  DocumentNumber     nvarchar(20)  NOT NULL CONSTRAINT UX_Employees_DocumentNumber UNIQUE,
  BirthDate          date          NOT NULL,
  Phone              nvarchar(25)  NOT NULL,
  Email              nvarchar(120) NOT NULL,
  Address            nvarchar(200) NOT NULL,
  EmergencyContact   nvarchar(200) NOT NULL,
  Position           nvarchar(60)  NOT NULL,
  Department         nvarchar(60)  NOT NULL,
  WorkSite           nvarchar(80)  NOT NULL,
  ContractType       nvarchar(20)  NOT NULL CONSTRAINT CK_Employees_ContractType CHECK (ContractType IN ('Indefinite','FixedTerm','Services','Apprenticeship')),
  HireDate           date          NOT NULL,
  MonthlySalary      decimal(14,2) NOT NULL CONSTRAINT CK_Employees_MonthlySalary CHECK (MonthlySalary > 0),
  Shift              nvarchar(20)  NOT NULL CONSTRAINT CK_Employees_Shift CHECK (Shift IN ('Morning','Afternoon','Night')),
  Eps                nvarchar(60)  NOT NULL,
  PensionFund        nvarchar(60)  NOT NULL,
  Arl                nvarchar(80)  NOT NULL,
  CompensationFund   nvarchar(60)  NOT NULL,
  BankName           nvarchar(60)  NOT NULL,
  BankAccountType    nvarchar(20)  NOT NULL CONSTRAINT CK_Employees_BankAccountType CHECK (BankAccountType IN ('Savings','Checking')),
  BankAccountNumber  nvarchar(30)  NOT NULL,   -- sensible: la API solo devuelve ****1234
  PayFrequency       nvarchar(20)  NOT NULL CONSTRAINT CK_Employees_PayFrequency CHECK (PayFrequency IN ('Biweekly','Monthly')),
  IsActive           bit           NOT NULL DEFAULT 1,
  CreatedAt          datetime2(3)  NOT NULL,
  UpdatedAt          datetime2(3)  NULL
);

CREATE TABLE Users (
  Id            int IDENTITY PRIMARY KEY,
  Username      nvarchar(60)  NOT NULL CONSTRAINT UX_Users_Username UNIQUE,
  Email         nvarchar(120) NULL,
  PasswordHash  nvarchar(500) NOT NULL,       -- PBKDF2 (PasswordHasher de ASP.NET Core)
  RoleId        int           NOT NULL REFERENCES Roles(Id),
  EmployeeId    int           NULL REFERENCES Employees(Id),
  IsActive      bit           NOT NULL DEFAULT 1,
  CreatedAt     datetime2(3)  NOT NULL,
  LastLoginAt   datetime2(3)  NULL
);
CREATE UNIQUE INDEX UX_Users_Email      ON Users(Email)      WHERE Email IS NOT NULL;
CREATE UNIQUE INDEX UX_Users_EmployeeId ON Users(EmployeeId) WHERE EmployeeId IS NOT NULL;

CREATE TABLE PayrollPeriods (
  Id           int IDENTITY PRIMARY KEY,
  StartDate    date NOT NULL,
  EndDate      date NOT NULL,
  PaymentDate  date NOT NULL,
  Status       nvarchar(20) NOT NULL CONSTRAINT CK_PayrollPeriods_Status CHECK (Status IN ('Draft','Approved','Paid')),
  CONSTRAINT CK_PayrollPeriods_Dates CHECK (EndDate >= StartDate),
  CONSTRAINT UX_PayrollPeriods_Range UNIQUE (StartDate, EndDate)
);

CREATE TABLE Payslips (
  Id               int IDENTITY PRIMARY KEY,
  Code             nvarchar(20) NOT NULL CONSTRAINT UX_Payslips_Code UNIQUE
                   DEFAULT CONCAT('NOM-', FORMAT(NEXT VALUE FOR dbo.PayslipCodeSeq, '000000')),
  PeriodId         int NOT NULL REFERENCES PayrollPeriods(Id),
  EmployeeId       int NOT NULL REFERENCES Employees(Id),
  DaysWorked       smallint NOT NULL CONSTRAINT CK_Payslips_DaysWorked CHECK (DaysWorked BETWEEN 0 AND 31),
  TotalEarnings    decimal(14,2) NOT NULL,
  TotalDeductions  decimal(14,2) NOT NULL,
  NetPay           AS (TotalEarnings - TotalDeductions) PERSISTED,
  Status           nvarchar(20) NOT NULL CONSTRAINT CK_Payslips_Status CHECK (Status IN ('Draft','Approved','Paid')),
  PaidAt           datetime2(3) NULL,
  CONSTRAINT UX_Payslips_PeriodEmployee UNIQUE (PeriodId, EmployeeId)
);

CREATE TABLE PayslipLines (
  Id           int IDENTITY PRIMARY KEY,
  PayslipId    int NOT NULL REFERENCES Payslips(Id) ON DELETE CASCADE,
  Type         nvarchar(20)  NOT NULL CONSTRAINT CK_PayslipLines_Type CHECK (Type IN ('Earning','Deduction')),
  Concept      nvarchar(80)  NOT NULL,       -- Salario básico, Auxilio de transporte, Salud 4 %…
  Description  nvarchar(200) NULL,
  Amount       decimal(14,2) NOT NULL CONSTRAINT CK_PayslipLines_Amount CHECK (Amount >= 0)
);

/* ========== CRM ========== */
CREATE TABLE Customers (
  Id              int IDENTITY PRIMARY KEY,
  Code            nvarchar(12)  NOT NULL CONSTRAINT UX_Customers_Code UNIQUE
                  DEFAULT CONCAT('CLI-', FORMAT(NEXT VALUE FOR dbo.CustomerCodeSeq, '000')),
  DocumentNumber  nvarchar(20)  NOT NULL CONSTRAINT UX_Customers_DocumentNumber UNIQUE, -- solo dígitos, no se edita
  FirstNames      nvarchar(80)  NOT NULL,
  LastNames       nvarchar(80)  NOT NULL,
  Phone           nvarchar(20)  NOT NULL,
  Email           nvarchar(120) NULL,
  Birthday        date          NULL,
  IsActive        bit           NOT NULL DEFAULT 1,
  CreatedAt       datetime2(3)  NOT NULL,
  UpdatedAt       datetime2(3)  NULL
);
CREATE INDEX IX_Customers_Names ON Customers(FirstNames, LastNames);

/* ========== FACTURACIÓN Y VENTAS ========== */
CREATE TABLE Products (
  Id           int IDENTITY PRIMARY KEY,
  Name         nvarchar(100) NOT NULL CONSTRAINT UX_Products_Name UNIQUE,
  Description  nvarchar(200) NULL,
  Category     nvarchar(20)  NOT NULL CONSTRAINT CK_Products_Category CHECK (Category IN ('Bakery','Pastry','Drink','Dessert')),
  Price        decimal(14,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
  IsActive     bit NOT NULL DEFAULT 1
);

CREATE TABLE DiningTables (
  Id        int IDENTITY PRIMARY KEY,
  Number    smallint NOT NULL CONSTRAINT UX_DiningTables_Number UNIQUE,
  Capacity  smallint NOT NULL CONSTRAINT CK_DiningTables_Capacity CHECK (Capacity > 0),
  IsActive  bit NOT NULL DEFAULT 1
  -- Estado (Libre / Ocupada / Por cobrar): se deriva del pedido abierto, no se guarda.
);

CREATE TABLE Orders (
  Id               int IDENTITY PRIMARY KEY,
  Code             nvarchar(12) NOT NULL CONSTRAINT UX_Orders_Code UNIQUE
                   DEFAULT CONCAT('P-', FORMAT(NEXT VALUE FOR dbo.OrderCodeSeq, '0000')),
  CustomerId       int NULL REFERENCES Customers(Id),        -- NULL = cliente ocasional
  ServiceType      nvarchar(20) NOT NULL CONSTRAINT CK_Orders_ServiceType CHECK (ServiceType IN ('DineIn','Takeaway')),
  DiningTableId    int NULL REFERENCES DiningTables(Id),
  Status           nvarchar(20) NOT NULL CONSTRAINT CK_Orders_Status CHECK (Status IN ('Received','Preparing','Ready','Delivered','Cancelled')),
  ServedByUserId   int NOT NULL REFERENCES Users(Id),
  Total            decimal(14,2) NOT NULL,
  CreatedAt        datetime2(3) NOT NULL,
  PreparingAt      datetime2(3) NULL,
  ReadyAt          datetime2(3) NULL,
  DeliveredAt      datetime2(3) NULL,
  BillRequestedAt  datetime2(3) NULL,                       -- mesa "Por cobrar"
  ClosedAt         datetime2(3) NULL,                       -- se cierra al cobrar o cancelar
  CONSTRAINT CK_Orders_Table CHECK ((ServiceType = 'DineIn' AND DiningTableId IS NOT NULL)
                                 OR (ServiceType = 'Takeaway' AND DiningTableId IS NULL))
);
CREATE INDEX IX_Orders_CreatedAt ON Orders(CreatedAt);
-- Una mesa solo tiene un pedido abierto a la vez:
CREATE UNIQUE INDEX UX_Orders_OpenTable ON Orders(DiningTableId) WHERE DiningTableId IS NOT NULL AND ClosedAt IS NULL;

CREATE TABLE OrderItems (
  Id         int IDENTITY PRIMARY KEY,
  OrderId    int NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
  ProductId  int NOT NULL REFERENCES Products(Id),
  Quantity   smallint NOT NULL CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0),
  UnitPrice  decimal(14,2) NOT NULL,                        -- copia del precio al vender
  LineTotal  AS (Quantity * UnitPrice) PERSISTED,
  CONSTRAINT UX_OrderItems_OrderProduct UNIQUE (OrderId, ProductId)
);

CREATE TABLE SalesInvoices (
  Id              int IDENTITY PRIMARY KEY,
  Number          nvarchar(12) NOT NULL CONSTRAINT UX_SalesInvoices_Number UNIQUE
                  DEFAULT CONCAT('FV-', FORMAT(NEXT VALUE FOR dbo.SalesInvoiceSeq, '0000')),
  OrderId         int NOT NULL CONSTRAINT UX_SalesInvoices_OrderId UNIQUE REFERENCES Orders(Id),
  CustomerId      int NULL REFERENCES Customers(Id),
  Total           decimal(14,2) NOT NULL,
  PaymentMethod   nvarchar(20) NOT NULL CONSTRAINT CK_SalesInvoices_PaymentMethod CHECK (PaymentMethod IN ('Cash','Card','Transfer')),
  AmountReceived  decimal(14,2) NULL,
  ChangeGiven     decimal(14,2) NULL,
  Status          nvarchar(20) NOT NULL CONSTRAINT CK_SalesInvoices_Status CHECK (Status IN ('Paid','Voided')),
  VoidReason      nvarchar(200) NULL,
  IssuedByUserId  int NOT NULL REFERENCES Users(Id),
  IssuedAt        datetime2(3) NOT NULL,
  VoidedAt        datetime2(3) NULL,
  CONSTRAINT CK_SalesInvoices_Cash CHECK (PaymentMethod <> 'Cash' OR AmountReceived >= Total)
);
CREATE INDEX IX_SalesInvoices_IssuedAt ON SalesInvoices(IssuedAt);

/* ========== PROVEEDORES ========== */
CREATE TABLE SupplyCategories (
  Id    int IDENTITY PRIMARY KEY,
  Name  nvarchar(40) NOT NULL CONSTRAINT UX_SupplyCategories_Name UNIQUE  -- Harinas, Lácteos, Huevos…
);

CREATE TABLE Suppliers (
  Id           int IDENTITY PRIMARY KEY,
  Name         nvarchar(120) NOT NULL,
  TaxId        nvarchar(20)  NOT NULL CONSTRAINT UX_Suppliers_TaxId UNIQUE,  -- NIT sin puntos: 900123456-7
  ContactName  nvarchar(80)  NOT NULL,
  Phone        nvarchar(20)  NOT NULL,
  Email        nvarchar(120) NOT NULL,
  IsActive     bit NOT NULL DEFAULT 1,
  CreatedAt    datetime2(3) NOT NULL,
  UpdatedAt    datetime2(3) NULL
);

CREATE TABLE SupplierSupplyCategories (
  SupplierId        int NOT NULL REFERENCES Suppliers(Id) ON DELETE CASCADE,
  SupplyCategoryId  int NOT NULL REFERENCES SupplyCategories(Id) ON DELETE CASCADE,
  PRIMARY KEY (SupplierId, SupplyCategoryId)
);

/* ========== INVENTARIOS ========== */
CREATE TABLE Supplies (
  Id              int IDENTITY PRIMARY KEY,
  Name            nvarchar(100) NOT NULL CONSTRAINT UX_Supplies_Name UNIQUE,
  CategoryId      int NOT NULL REFERENCES SupplyCategories(Id),
  PurchaseUnit    nvarchar(60) NOT NULL,                    -- "Bulto de 50 kg"
  UnitLabel       nvarchar(20) NOT NULL,                    -- "bultos"
  UnitSizeKg      decimal(10,3) NULL,                       -- 50 → "1.250 kg en total"
  CurrentStock    decimal(12,2) NOT NULL CONSTRAINT CK_Supplies_CurrentStock CHECK (CurrentStock >= 0),
  MinStock        decimal(12,2) NOT NULL CONSTRAINT CK_Supplies_MinStock CHECK (MinStock >= 0),
  MainSupplierId  int NULL REFERENCES Suppliers(Id),
  IsActive        bit NOT NULL DEFAULT 1,
  CreatedAt       datetime2(3) NOT NULL,
  UpdatedAt       datetime2(3) NULL
  -- "Por acabarse": CurrentStock < MinStock. "Agotado": CurrentStock = 0. No se guardan.
);

/* ========== COMPRAS ========== */
CREATE TABLE PurchaseOrders (
  Id                  int IDENTITY PRIMARY KEY,
  Code                nvarchar(12) NOT NULL CONSTRAINT UX_PurchaseOrders_Code UNIQUE
                      DEFAULT CONCAT('OC-', FORMAT(NEXT VALUE FOR dbo.PurchaseOrderSeq, '0000')),
  SupplierId          int NOT NULL REFERENCES Suppliers(Id),
  Status              nvarchar(20) NOT NULL CONSTRAINT CK_PurchaseOrders_Status
                      CHECK (Status IN ('Draft','Requested','Confirmed','Received','Cancelled')),
  ExpectedDeliveryAt  datetime2(3) NOT NULL,
  Total               decimal(14,2) NOT NULL,
  Notes               nvarchar(200) NULL,
  CancelReason        nvarchar(200) NULL,
  CreatedByUserId     int NOT NULL REFERENCES Users(Id),
  RequestedAt         datetime2(3) NOT NULL,
  ConfirmedAt         datetime2(3) NULL,
  ReceivedAt          datetime2(3) NULL,
  CancelledAt         datetime2(3) NULL
);
CREATE INDEX IX_PurchaseOrders_Supplier_Status ON PurchaseOrders(SupplierId, Status);

CREATE TABLE PurchaseOrderItems (
  Id               int IDENTITY PRIMARY KEY,
  PurchaseOrderId  int NOT NULL REFERENCES PurchaseOrders(Id) ON DELETE CASCADE,
  SupplyId         int NOT NULL REFERENCES Supplies(Id),
  Quantity         decimal(12,2) NOT NULL CONSTRAINT CK_PurchaseOrderItems_Quantity CHECK (Quantity > 0),
  UnitPrice        decimal(14,2) NOT NULL CONSTRAINT CK_PurchaseOrderItems_UnitPrice CHECK (UnitPrice >= 0),
  LineTotal        AS (Quantity * UnitPrice) PERSISTED,
  CONSTRAINT UX_PurchaseOrderItems_OrderSupply UNIQUE (PurchaseOrderId, SupplyId)
);

CREATE TABLE InventoryMovements (
  Id               bigint IDENTITY PRIMARY KEY,
  SupplyId         int NOT NULL REFERENCES Supplies(Id),
  Type             nvarchar(20) NOT NULL CONSTRAINT CK_InventoryMovements_Type CHECK (Type IN ('In','Out','Adjustment')),
  Quantity         decimal(12,2) NOT NULL CONSTRAINT CK_InventoryMovements_Quantity CHECK (Quantity > 0),
  StockAfter       decimal(12,2) NOT NULL,
  SupplierId       int NULL REFERENCES Suppliers(Id),
  PurchaseOrderId  int NULL REFERENCES PurchaseOrders(Id),
  MovementDate     date NOT NULL,
  Location         nvarchar(60)  NULL,                      -- "Pasillo A - Tarima 04"
  ExpirationDate   date NULL,
  Notes            nvarchar(200) NULL,
  CreatedByUserId  int NOT NULL REFERENCES Users(Id),
  CreatedAt        datetime2(3) NOT NULL
);
CREATE INDEX IX_InventoryMovements_Supply_CreatedAt ON InventoryMovements(SupplyId, CreatedAt);

CREATE TABLE PurchaseInvoices (
  Id                     int IDENTITY PRIMARY KEY,
  SupplierInvoiceNumber  nvarchar(30) NOT NULL,             -- FC-9921 (lo da el proveedor)
  SupplierId             int NOT NULL REFERENCES Suppliers(Id),
  PurchaseOrderId        int NULL REFERENCES PurchaseOrders(Id),
  Description            nvarchar(200) NULL,
  IssueDate              date NOT NULL,
  DueDate                date NOT NULL,
  Total                  decimal(14,2) NOT NULL CONSTRAINT CK_PurchaseInvoices_Total CHECK (Total > 0),
  Status                 nvarchar(20) NOT NULL CONSTRAINT CK_PurchaseInvoices_Status CHECK (Status IN ('Pending','Paid')),
  PaidAt                 datetime2(3) NULL,
  CONSTRAINT CK_PurchaseInvoices_Dates CHECK (DueDate >= IssueDate),
  CONSTRAINT UX_PurchaseInvoices_SupplierNumber UNIQUE (SupplierId, SupplierInvoiceNumber)
  -- "VENCIDA" = Status 'Pending' AND DueDate < hoy. No se guarda.
);
