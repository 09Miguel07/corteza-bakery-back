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

public enum PurchaseOrderStatus { Draft, Requested, Confirmed, Received, Cancelled }

public enum PurchaseInvoiceStatus { Pending, Paid }
