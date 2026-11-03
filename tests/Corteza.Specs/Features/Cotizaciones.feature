# language: es
Característica: Solicitud de cotizaciones
  Como analista de compras de La Espiga
  Quiero pedir precios a varios proveedores y comparar sus ofertas
  Para comprar los insumos al mejor precio

  Antecedentes:
    Dado que el insumo "Harina de trigo" tiene 40 bultos en bodega
    Y que el proveedor "Molinos del Valle" con NIT "900123456-7" está activo
    Y que existe una solicitud abierta por 25 bultos de "Harina de trigo"

  Esquema del escenario: Validar monto de cotización
    Cuando "Molinos del Valle" cotiza la solicitud a <precio> pesos por bulto
    Entonces el sistema responde "<resultado>"

    Ejemplos:
      | precio  | resultado             |
      | 2500000 | Cotización registrada |
      | 0       | Monto inválido        |
      | -1000   | Monto inválido        |

  Escenario: Registrar la cotización de un proveedor
    Cuando "Molinos del Valle" cotiza la solicitud a 50000 pesos por bulto
    Entonces la cotización de "Molinos del Valle" queda en estado "RECIBIDA"
    Y su total es 1250000 pesos

  Escenario: Un proveedor no cotiza dos veces la misma solicitud
    Dado que "Molinos del Valle" ya cotizó la solicitud a 50000 pesos por bulto
    Cuando "Molinos del Valle" cotiza la solicitud a 48000 pesos por bulto
    Entonces el sistema responde "El proveedor Molinos del Valle ya registró una cotización para esta solicitud."
    Y la solicitud tiene 1 cotización

  Escenario: Elegir la mejor oferta genera la orden de compra
    Dado que el proveedor "Harinera San Antonio" con NIT "901234567-1" está activo
    Y que "Molinos del Valle" ya cotizó la solicitud a 52000 pesos por bulto
    Y que "Harinera San Antonio" ya cotizó la solicitud a 50000 pesos por bulto
    Cuando elijo la cotización de "Harinera San Antonio"
    Entonces la solicitud queda en estado "CERRADA"
    Y se crea una orden de compra "PENDIENTE" para "Harinera San Antonio" por 1250000 pesos
