# language: es
Característica: Cobro de pedidos en mesa
  Como cajero de La Espiga
  quiero cobrar el pedido de una mesa
  para emitir la factura, devolver el cambio correcto y liberar la mesa

  Antecedentes:
    Dado que el producto "Pan francés" vale 1500 pesos
    Y que el producto "Café con leche" vale 3800 pesos
    Y que la mesa 4 está libre
    Y que la mesa 4 pidió 4 "Pan francés" y 2 "Café con leche"

  Escenario: Cobro en efectivo con cambio
    Cuando cobro el pedido en efectivo con 20000 pesos
    Entonces el sistema responde "Factura emitida"
    Y el cambio es 6400 pesos
    Y la mesa 4 queda "LIBRE"

  Escenario: El efectivo no alcanza
    Cuando cobro el pedido en efectivo con 10000 pesos
    Entonces el sistema responde "Efectivo insuficiente"
    Y la mesa 4 queda "OCUPADA"

  Escenario: Una mesa ocupada no recibe otro pedido
    Cuando intento abrir otro pedido en la mesa 4
    Entonces el sistema responde "La mesa 4 ya tiene un pedido abierto."
