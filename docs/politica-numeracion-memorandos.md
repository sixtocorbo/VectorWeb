# Política recomendada de numeración de memorandos

## Recomendación principal

Aplicar un esquema **mixto por oficina y tipo de documento**:

1. **Modo administrado por rango (interno)**: para oficinas donde el sistema controla la numeración.
2. **Modo numeración externa (libre/validada)**: para oficinas que reciben número de una entidad externa (por ejemplo, juzgados).

Este enfoque evita forzar una sola regla para todos los escenarios operativos.

---

## ¿Qué hacer cuando se agota el rango (ejemplo: 1 al 100)?

Para oficinas en **modo administrado por rango**, la práctica más segura es:

- **Bloquear la asignación automática al agotarse el rango**.
- **Mostrar mensaje claro**: “Rango agotado. Debe registrar un nuevo rango para continuar”.
- **No permitir que el usuario escriba números manuales** en ese modo, para evitar duplicados o saltos no auditables.

### ¿Por qué?

- Protege la trazabilidad legal/administrativa.
- Evita numeraciones duplicadas o incoherentes.
- Mantiene consistencia entre documentos del mismo circuito.

---

## Oficinas con numeración externa

Para oficinas en **modo numeración externa** (sin rangos administrados por el sistema):

- Permitir ingreso manual del número.
- Validar formato mínimo (no vacío, longitud máxima, caracteres permitidos).
- Validar unicidad bajo la regla de negocio que corresponda (ejemplo: por oficina + año + tipo).
- Guardar auditoría de quién ingresó el número y cuándo.

---

## Regla de decisión sugerida (operativa)

- Si la oficina/tipo está en **modo administrado por rango**:
  - El sistema consume el siguiente número.
  - Si no hay rango activo o está agotado, **se bloquea** y se solicita reposición.
- Si la oficina/tipo está en **modo numeración externa**:
  - Se habilita captura manual con validaciones.

---

## Buenas prácticas adicionales

- Alertar preventivamente cuando quede poco cupo (ejemplo: últimos 10 números).
- Permitir configurar rango por año para evitar cruces interanuales.
- Registrar historial de aperturas/cierres de rangos y cambios de modo.
- Definir un permiso excepcional de contingencia (supervisor) con auditoría obligatoria.

---

## Conclusión

Para tu caso, lo más sólido es **no dejar “libre para cualquiera” cuando el sistema sí administra rangos**. En esos casos conviene **bloquear al agotarse y exigir reposición**. En paralelo, habilita un **modo externo** para oficinas como juzgados, donde la numeración no la administra tu sistema.
