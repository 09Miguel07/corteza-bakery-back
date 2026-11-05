namespace Corteza.Domain.Enums;

// Los enums se guardan como texto en la base de datos (ver ConfigureConventions en el DbContext)
// y viajan como texto en el JSON de la API. Cambiar el nombre de un miembro exige una migración.

public enum DocumentType { CC, CE, PP, TI }

public enum ContractType { Indefinite, FixedTerm, Services, Apprenticeship }

public enum WorkShift { Morning, Afternoon, Night }

public enum BankAccountType { Savings, Checking }

public enum PayFrequency { Biweekly, Monthly }

public enum PayrollStatus { Draft, Approved, Paid }

public enum PayslipLineType { Earning, Deduction }

public enum ProductCategory { Bakery, Pastry, Drink, Dessert }

public enum ServiceType { DineIn, Takeaway }

public enum OrderStatus { Received, Preparing, Ready, Delivered, Cancelled }

public enum PaymentMethod { Cash, Card, Transfer }

public enum SalesInvoiceStatus { Paid, Voided }

public enum MovementType { In, Out, Adjustment }

/// <summary>Estado de existencias (pantalla 03.1): suficiente, por acabarse o agotado.</summary>
public enum StockStatus { Ok, Low, Out }

/// <summary>Pendiente → Aprobada → Recibida. Solo una pendiente se cancela.</summary>
public enum PurchaseOrderStatus { Pending, Approved, Received, Cancelled }

public enum PurchaseInvoiceStatus { Pending, Paid }

public enum RfqStatus { Open, Closed, Cancelled }

public enum QuotationStatus { Received, Selected, Rejected }

/// <summary>Estado de la mesa (pantalla 02.4). No se guarda: se deriva del pedido abierto.</summary>
public enum TableState { Free, Occupied, AwaitingPayment }
