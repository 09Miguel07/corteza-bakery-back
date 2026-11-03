# language: es
Característica: Aprobación de órdenes de compra
  Como jefe de compras de La Espiga
  Quiero aprobar órdenes solo si el proveedor tiene presupuesto
  Para no comprometer más dinero del asignado

  Antecedentes:
    Dado que el insumo "Harina de trigo" tiene 40 bultos en bodega
    Y que el proveedor "ACME" con NIT "900123456" tiene presupuesto disponible de 10000000 pesos

  Escenario: Aprobar orden dentro del presupuesto
    Dado que existe una orden de 100 bultos de "Harina de trigo" a 50000 pesos para "ACME"
    Cuando el jefe de compras aprueba la orden
    Entonces la orden cambia a "APROBADA"
    Y el presupuesto de "ACME" se reduce a 5000000 pesos

  Escenario: No aprobar una orden que excede el presupuesto
    Dado que existe una orden de 300 bultos de "Harina de trigo" a 50000 pesos para "ACME"
    Cuando el jefe de compras aprueba la orden
    Entonces el sistema responde "Presupuesto insuficiente: la orden vale $15.000.000 y el proveedor tiene $10.000.000 disponibles."
    Y la orden sigue en "PENDIENTE"
    Y el presupuesto de "ACME" sigue en 10000000 pesos

  Escenario: Una orden aprobada no se vuelve a aprobar
    Dado que existe una orden de 100 bultos de "Harina de trigo" a 50000 pesos para "ACME"
    Y que la orden ya fue aprobada
    Cuando el jefe de compras aprueba la orden
    Entonces el sistema responde que la orden ya está aprobada
    Y el presupuesto de "ACME" sigue en 5000000 pesos

  Escenario: Cancelar una orden pendiente indicando el motivo
    Dado que existe una orden de 100 bultos de "Harina de trigo" a 50000 pesos para "ACME"
    Cuando cancelo la orden por "El proveedor no tiene existencias"
    Entonces la orden cambia a "CANCELADA"

  Escenario: Recibir la mercancía suma al inventario
    Dado que existe una orden de 25 bultos de "Harina de trigo" a 50000 pesos para "ACME"
    Y que la orden ya fue aprobada
    Cuando registro la recepción de la orden
    Entonces la orden cambia a "RECIBIDA"
    Y el stock final de "Harina de trigo" es 65
