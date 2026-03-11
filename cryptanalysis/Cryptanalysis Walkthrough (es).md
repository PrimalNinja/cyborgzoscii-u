# Protocolo UNSIGNAL: Análisis de Inteligencia Paso a Paso

**Autor:** Julian Cassin  
**Fecha:** 2026-03-11

1. La Intercepción
   - Archivo crudo obtenido: sin cabeceras, sin bytes mágicos, sin estructura
   - Tamaño de archivo: variable (debido a prefijo/sufijo aleatorio)
   - Metadatos: tiempo, origen, destino — no llevan a ninguna parte, no se pueden correlacionar con contenido o intención

2. Análisis Estadístico (ent)
   - Entropía: 7.99+ bits/byte (máxima)
   - Compresión: 0% (entropía perfecta)
   - Chi cuadrado: pasa como aleatorio
   - Correlación serial: cercana a cero
   - Resultado: indistinguible del ruido aleatorio verdadero

3. Confusión del Sistema de Coordenadas
   - Direcciones de cabecera: posiciones absolutas dentro de los primeros 64KB de la ROM
   - Direcciones de datos: relativas al desplazamiento de ROM derivado de las propias indirecciones H1/H2
   - Dos sistemas de coordenadas diferentes en el mismo archivo
   - El atacante no puede interpretar las direcciones de datos sin primero:
       a) Reconocer H1/H2 como especiales (parecen datos normales)
       b) Decodificar H1/H2 para obtener el valor de desplazamiento
       c) Aplicar el desplazamiento para reinterpretar todas las direcciones siguientes
   - Probabilidad de adivinar correctamente sin ROM: cero
   - Incluso con ROM, debe saber qué direcciones son de cabecera vs. datos
   - La alineación cabecera/datos ocurre solo 1/65536 de las veces por azar

4. Análisis de Tráfico
   - Prefijo/sufijo aleatorio oculta los verdaderos límites del mensaje
   - H3/H4 también son indirecciones en sí mismos
   - Los desplazamientos de inicio de ROM variables cambian la interpretación por sesión
   - Sin patrones fijos en tamaños de paquetes o tiempos
   - No se puede determinar si el archivo contiene datos o está vacío

5. Ingeniería Inversa
   - Codificador obtenido: tabla de búsqueda de una línea (pública)
   - Algoritmo: trivial, la seguridad está en la ROM (clave)
   - Saber cómo funciona no proporciona ventaja

6. Intentos de Texto Plano Conocido
   - Mismo texto plano codificado dos veces → salidas diferentes
   - Múltiples opciones de dirección por carácter (selección aleatoria)
   - Sin patrones repetibles para explotar

7. Recuperación de Clave
   - Fuerza bruta: ITS, imposible por definición
   - Canal lateral: búsqueda simple, sin matemáticas complejas que filtrar
   - La ROM debe obtenerse por medios físicos/legales

8. El Problema de la Verificación
   - Cualquier ROM decodifica algo
   - ROM incorrecta → basura (pero basura que parece real)
   - Sin sumas de verificación, sin MAC, sin indicador de éxito
   - No se puede verificar qué decodificación es "correcta"

9. Escala Combinatoria (ejemplo de "Lo que el viento se llevó")
   - Codificación de una sola novela: >10^5,500,000 representaciones posibles
   - 5 direcciones no repetidas: 1 billón de combinaciones
   - Seguimiento hasta agotar la memoria: imposible
   - Sin colisiones, nunca

10. Negación Perfecta
    - Cada decodificación es internamente consistente
    - Cualquier salida puede ser descartada como coincidencia aleatoria
    - La decodificación "correcta" no está definida sin contexto externo

11. Comportamiento de Compresión
    - Los archivos codificados ZOSCII/UNSIGNAL no se comprimen (~0% de ratio)
    - La salida ya está cerca de la entropía máxima
    - Para reducir tamaño: comprimir la entrada primero, luego codificar
    - El resultado codificado permanece incompresible independientemente de la entrada

12. Autenticación y Detección de Manipulación es Interna
    - MAC si es necesario: colóquelo DENTRO de la carga útil codificada
    - Sumas de verificación, firmas, datos de verificación: todos VAN EN el mensaje
    - Se aplican las mismas reglas de codificación — se vuelven indistinguibles del ruido aleatorio
    - El atacante no puede distinguir los datos de autenticación del contenido del mensaje
    - No existen marcadores de validación externos

13. Conclusión
    - Las herramientas estadísticas devuelven: ruido aleatorio, nada aquí
    - Análisis de tráfico derrotado por ocultación de límites
    - La recuperación de clave requiere ROM, no matemáticas
    - Verificación imposible incluso con un candidato de ROM
    - Autenticación oculta dentro de la carga útil, indistinguible del mensaje
    - Compresión solo posible antes de la codificación, no después
    - El sistema logra un cierre epistémico: el atacante no puede saber si ha ganado
