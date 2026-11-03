# language: es
Característica: Gestión de proveedores
  Como coordinadora de proveedores de La Espiga
  Quiero registrar proveedores con NIT y correo válidos
  Para mantener actualizada la base de aliados comerciales

  Escenario: Registrar un proveedor válido
    Dado que no existe un proveedor con NIT "900123456"
    Cuando registro el proveedor con NIT "900123456", nombre "ACME S.A.S." y correo "contacto@acme.com"
    Entonces el sistema responde "Proveedor registrado"
    Y el proveedor "ACME S.A.S." queda en estado "ACTIVO"

  Escenario: Rechazar proveedor con NIT duplicado
    Dado que ya existe un proveedor con NIT "900123456"
    Cuando intento registrar otro proveedor "Harinera Copia" con el NIT "900.123.456"
    Entonces el sistema responde "Ya existe un proveedor con el NIT 900123456."
    Y solo hay 1 proveedor con NIT "900123456"

  Esquema del escenario: Validar formatos de correo
    Cuando registro el proveedor con NIT "901234567", nombre "Empaques Kraft" y correo "<correo>"
    Entonces el sistema responde "<resultado>"

    Ejemplos:
      | correo                    | resultado                  |
      | pedidos@empaqueskraft.com | Proveedor registrado       |
      | sin-arroba.com            | Formato de correo inválido |
      | usuario@                  | Formato de correo inválido |

  Escenario: Desactivar un proveedor sin órdenes de compra activas
    Dado que existe un proveedor "ACME" sin órdenes de compra activas
    Cuando solicito desactivar el proveedor "ACME"
    Entonces el sistema responde "Proveedor desactivado"
    Y el proveedor "ACME" queda en estado "INACTIVO"

  Escenario: No desactivar un proveedor con órdenes de compra activas
    Dado que existe un proveedor "ACME" con órdenes de compra activas
    Cuando solicito desactivar el proveedor "ACME"
    Entonces el sistema responde "No se puede desactivar: el proveedor tiene órdenes activas."
    Y el proveedor "ACME" queda en estado "ACTIVO"
