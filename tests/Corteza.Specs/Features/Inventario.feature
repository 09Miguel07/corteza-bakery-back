# language: es
Característica: Registro de insumos en inventario
  Como auxiliar de bodega de La Espiga
  Quiero registrar entradas y salidas de insumos
  Para saber siempre cuánto hay de cada uno

  Esquema del escenario: Retiro de inventario
    Dado que el insumo "<insumo>" tiene <stock> unidades en bodega
    Cuando se retiran <retiro> unidades de "<insumo>"
    Entonces el stock final de "<insumo>" es <final>
    Y el sistema responde "<mensaje>"

    Ejemplos:
      | insumo          | stock | retiro | final | mensaje            |
      | Azúcar refinada | 10    | 3      | 7     | Retiro exitoso     |
      | Levadura fresca | 5     | 5      | 0     | Retiro exitoso     |
      | Leche entera    | 2     | 5      | 2     | Stock insuficiente |

  Escenario: Registrar una entrada suma al stock
    Dado que el insumo "Harina de trigo" tiene 40 bultos en bodega
    Cuando registro una entrada de 25 bultos de "Harina de trigo"
    Entonces el sistema responde "Entrada registrada"
    Y el stock final de "Harina de trigo" es 65

  Escenario: El stock inicial no puede ser negativo
    Cuando registro el insumo "Mantequilla pura" con stock inicial -5
    Entonces el sistema responde "El stock inicial no puede ser negativo."
