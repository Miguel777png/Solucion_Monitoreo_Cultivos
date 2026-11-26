# Sistema Web e IoT de Monitoreo y Riego Adaptativo para Invernaderos de Orquídeas

Plataforma web desarrollada en **ASP.NET Core MVC (.NET 8)** conectada a nodos microcontroladores **ESP32** (simulados en **Wokwi**) y persistencia relacional en **MySQL 8.0** (local o en la nube con **Azure Database for MySQL**). El sistema captura humedad y temperatura cada **2,5 segundos**, evalúa los umbrales botánicos de cada especie de orquídea y controla automáticamente una válvula de riego mediante servomotor, climatización (ventilación y calefacción) y confirmación de comandos con persistencia en memoria no volátil (NVS).

---

## Funcionamiento General

El proyecto utiliza una arquitectura **MVC**. Los ESP32 envían la humedad y temperatura a la API de telemetría, el servidor evalúa los rangos de la planta asignada y responde qué actuadores deben activarse.

* La humedad bajo el mínimo activa el riego de emergencia hasta recuperar el rango permitido.
* La temperatura sobre el máximo activa la ventilación.
* La temperatura bajo el mínimo activa la calefacción.
* El valor óptimo indica dónde comienza el rango óptimo. Si no se personaliza, se calcula con el punto medio entre el mínimo y el máximo.
* El calendario omite el riego cuando la humedad ya se encuentra en el rango óptimo.
* Los administradores pueden gestionar usuarios y sensores. Los operadores pueden utilizar las funciones normales de monitoreo y configuración permitidas.

### Estructura Principal

```text
Web_Solucion_Monitoreo_Invernadero/
├── Controllers/     Controladores MVC y API de telemetría
├── Models/          Entidades de la base de datos y DTO
├── Database/        Contexto y configuración de Entity Framework
├── Services/        Estado en memoria, auditoría y riego programado
├── Views/           Vistas Razor organizadas por función
├── wwwroot/         CSS, JavaScript y librerías del frontend
├── Firmware/        Código del ESP32 para Wokwi
└── sqlscript/       Script de creación de la base de datos
```

### Archivos Importantes

* [Solución de Visual Studio](Web_Solucion_Monitoreo_Invernadero.slnx)
* [Proyecto ASP.NET Core](Web_Solucion_Monitoreo_Invernadero/Web_Solucion_Monitoreo_Invernadero.csproj)
* [Configuración principal de la aplicación](Web_Solucion_Monitoreo_Invernadero/Program.cs)
* [Configuración de conexión](Web_Solucion_Monitoreo_Invernadero/appsettings.json)
* [Script completo de la base de datos](Web_Solucion_Monitoreo_Invernadero/sqlscript/monitoreo_orquideas.sql)
* [Firmware del ESP32](Web_Solucion_Monitoreo_Invernadero/Firmware/ESP32_Invernadero_Wokwi/ESP32_Invernadero_Wokwi.ino)
* [Circuito de Wokwi](Web_Solucion_Monitoreo_Invernadero/Firmware/ESP32_Invernadero_Wokwi/diagram.json)
* [Librerías utilizadas por Wokwi](Web_Solucion_Monitoreo_Invernadero/Firmware/libraries.txt)

---

## 1. Requisitos Previos

Antes de iniciar la instalación en un equipo local, asegúrate de contar con:

* **.NET 8.0 SDK** (o superior).
* **Visual Studio 2022 / 2026** (con la carga de trabajo *ASP.NET y desarrollo web* y soporte para *Túneles de desarrollo*).
* **Git** instalado en el sistema.
* **Motor MySQL 8.0+** (solo si ejecutarás la base de datos de forma local, por ejemplo mediante **Laragon**, **XAMPP** o **MySQL Community Server**) y un cliente SQL (**MySQL Workbench** o **HeidiSQL**).
* Cuenta gratuita en **[Wokwi](https://wokwi.com/)** para ejecutar la simulación del microcontrolador ESP32.

---

## 2. Clonación e Instalación del Proyecto

Puedes ejecutar estos comandos **directamente desde la Terminal integrada de Visual Studio** (en el menú superior **Ver** $\rightarrow$ **Terminal**, o con el atajo `Ctrl + '` / `Ctrl + Ñ`), o bien desde PowerShell, CMD o Git Bash:

1. Clona el repositorio en tu equipo e ingresa a la carpeta del proyecto:
   ```powershell
   git clone https://github.com/Miguel777png/Solucion_Monitoreo_Cultivos.git
   cd Solucion_Monitoreo_Cultivos
   ```

2. Restaura los paquetes NuGet necesarios:
   ```powershell
   dotnet restore .\Web_Solucion_Monitoreo_Invernadero\Web_Solucion_Monitoreo_Invernadero.csproj
   ```

3. Compila el proyecto para comprobar que la instalación sea correcta:
   ```powershell
   dotnet build .\Web_Solucion_Monitoreo_Invernadero\Web_Solucion_Monitoreo_Invernadero.csproj
   ```

4. Ejecuta la aplicación desde la raíz del repositorio:
   ```powershell
   dotnet run --project .\Web_Solucion_Monitoreo_Invernadero
   ```

Actualmente el repositorio no posee un proyecto de pruebas automatizadas. Cuando se agregue uno, se podrá ejecutar con `dotnet test`.

---

## 3. Configuración de la Base de Datos

Puedes ejecutar el sistema conectándote a una **base de datos MySQL local** en tu computador o directamente a la **base de datos en la nube (Azure)**.

### Opción A: Crear y Ejecutar la Base de Datos en Local (Paso a Paso)

1. **Inicia tu servidor MySQL local:**
   * Si usas **Laragon**, abre la aplicación y haz clic en **Iniciar Todo** (verifica que el servicio `MySQL` quede activo en el puerto `3306`).
   * Si usas **MySQL Server**, asegúrate de que el servicio `MySQL80` esté en ejecución.

2. **Conéctate desde tu gestor SQL (MySQL Workbench o HeidiSQL):**
   * **Host / IP:** `127.0.0.1` (o `localhost`)
   * **Puerto:** `3306`
   * **Usuario:** `root`
   * **Contraseña:** *(vacía por defecto en Laragon, o la clave que definiste al instalar MySQL)*

3. **Ejecuta el script de creación de base de datos:**
   * Abre [`Web_Solucion_Monitoreo_Invernadero/sqlscript/monitoreo_orquideas.sql`](Web_Solucion_Monitoreo_Invernadero/sqlscript/monitoreo_orquideas.sql) en tu gestor SQL.
   * Este script comienza con `DROP DATABASE IF EXISTS`. Por este motivo elimina la base anterior y debe utilizarse solamente en una instalación nueva o en una base local que se pueda volver a crear.
   * No ejecutes el script completo sobre una base con información que necesites conservar.
   * Presiona **Ejecutar** (`F9` en HeidiSQL o el ícono del rayo en MySQL Workbench).

4. **Verifica la estructura:**
   * Refresca el panel izquierdo y confirma que exista la base de datos **`monitoreo_orquideas`** con todas sus tablas (`usuarios`, `plantas`, `invernaderos`, `sensores`, `calendario_riego`, `registro_historico`, `bitacora_riego_alertas`, `comandos_riego` y `auditoria_cambios`).

### Opción B: Usar la Base de Datos en la Nube (Azure Database for MySQL)

Si utilizarás la instancia desplegada en Microsoft Azure (`orquideas-iot3.mysql.database.azure.com`), no necesitas ejecutar el script localmente; solo verifica en el portal de Azure (**Redes**) que tu dirección IP pública tenga permiso de acceso en el firewall del servidor.

---

## 4. Configuración de [`appsettings.json`](Web_Solucion_Monitoreo_Invernadero/appsettings.json) (Local vs. Nube)

La aplicación necesita una cadena llamada **`DefaultConnection`**. Los siguientes ejemplos muestran su formato para trabajar en local o en Azure.

> **Importante:** no guardes contraseñas reales en Git. Para trabajar en tu equipo utiliza User Secrets o una variable de entorno. Si una contraseña fue publicada anteriormente, debe cambiarse en el servidor de base de datos.

Puedes guardar la conexión de desarrollo mediante User Secrets:

```powershell
dotnet user-secrets init --project .\Web_Solucion_Monitoreo_Invernadero
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "TU_CADENA_DE_CONEXION" --project .\Web_Solucion_Monitoreo_Invernadero
```

### Caso 1: Conectarse a tu Base de Datos Local (Laragon / MySQL Local)

En local no se exige certificado SSL y el usuario por defecto es `root`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=monitoreo_orquideas;Uid=root;Pwd=;SslMode=None;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```
*(Si tu instalación local de MySQL tiene contraseña en `root`, escríbela en `Pwd=tu_clave;`).*

### Caso 2: Conectarse a la Base de Datos en la Nube (Azure MySQL)

Para Azure Database for MySQL debes apuntar al servidor en Azure, indicar el usuario administrador y exigir cifrado TLS mediante `SslMode=Required;`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=orquideas-iot3.mysql.database.azure.com;Port=3306;Database=monitoreo_orquideas;Uid=administrador;Pwd=TU_CONTRASEÑA_AZURE;SslMode=Required;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Resumen de qué cambia en `appsettings.json`

| Parámetro     | Configuración Local (Laragon/MySQL)           | Configuración Nube (Azure MySQL)                  |
| :------------ | :-------------------------------------------- | :------------------------------------------------ |
| **`Server`**  | `localhost` (o `127.0.0.1`)                   | `orquideas-iot3.mysql.database.azure.com`         |
| **`Uid`**     | `root`                                        | `administrador` (usuario admin de Azure)          |
| **`Pwd`**     | *(vacío)* o tu clave local                    | Contraseña configurada en el servidor Azure       |
| **`SslMode`** | `None` *(evita errores de certificado local)* | `Required` *(obligatorio por seguridad en Azure)* |

---

## 5. Ejecución y Creación del Dev Tunnel en Visual Studio

### ¿Por qué es necesario un Dev Tunnel?
Los microcontroladores ESP32 simulados en **Wokwi** se ejecutan en internet y **no pueden conectarse a `localhost:5034`** directamente. **Microsoft Dev Tunnels** genera una URL HTTPS pública que redirige tanto el envío de telemetría como la confirmación de comandos hacia tu servidor local.

### Pasos para crear el túnel y levantar el servidor:

1. Abre la solución [`Web_Solucion_Monitoreo_Invernadero.slnx`](Web_Solucion_Monitoreo_Invernadero.slnx) en **Visual Studio**.
2. En la barra superior, haz clic en la flecha desplegable junto al botón verde de ejecución (**`http`**).
3. Selecciona **Túneles de desarrollo** $\rightarrow$ **Crear un túnel...**.
4. Inicia sesión con tu cuenta Microsoft / institucional y configura los parámetros:
   * **Nombre:** `tunel-orquideas`
   * **Tipo de túnel:** **Persistente** *(para conservar la misma URL al reiniciar)*
   * **Acceso:** **Público** *(indispensable para que el ESP32 pueda enviar peticiones `POST` sin iniciar sesión)*
5. Presiona **`F5`** (o clic en el botón **http**) para ejecutar el proyecto. Visual Studio abrirá el navegador y mostrará la URL pública generada (ejemplo: `https://TU-TUNEL-5034.brs.devtunnels.ms`). **Copia esta URL base**, ya que deberás colocarla en **dos variables** dentro del código del ESP32 en Wokwi.

El túnel debe ser público para que Wokwi pueda enviar telemetría. Utilízalo solamente durante las pruebas, ya que el endpoint del ESP32 acepta lecturas sin iniciar sesión.

---

## 6. Instalación y Configuración del Nodo ESP32 en Wokwi

Sigue estos pasos para montar el microcontrolador, el sensor DHT22, el servomotor de la válvula y los LEDs de climatización en el simulador:

### Paso 1: Crear el proyecto en Wokwi
1. Ingresa a [wokwi.com](https://wokwi.com/) e inicia sesión.
2. En el menú principal, haz clic en **New Project** $\rightarrow$ selecciona la placa **ESP32**.

### Paso 2: Instalar las librerías necesarias (`libraries.txt`)
1. En el editor de Wokwi, haz clic en la pestaña **`Library Manager`** y utiliza como referencia el archivo [`Firmware/libraries.txt`](Web_Solucion_Monitoreo_Invernadero/Firmware/libraries.txt).
2. Agrega las siguientes librerías externas (las librerías `WiFi.h`, `HTTPClient.h`, `WiFiClientSecure.h` y `Preferences.h` ya vienen integradas en el núcleo del ESP32):

```text
# Wokwi Library List
# See https://docs.wokwi.com/guides/libraries

DHT sensor library
ArduinoJson
ESP32Servo
```

### Paso 3: Cargar el circuito y conexiones (`diagram.json`)
1. Haz clic en la pestaña **`diagram.json`** dentro del editor de Wokwi.
2. Borra el contenido por defecto y pega la siguiente configuración (incluye la placa **ESP32**, el sensor **DHT22** en `GPIO 15`, el **Servomotor** de válvula en `GPIO 26`, el **LED Azul de ventilación** en `GPIO 27` y el **LED Rojo de calefacción** en `GPIO 14` con sus resistencias de $1\text{ k}\Omega$):

La configuración completa ya se encuentra en [`Firmware/ESP32_Invernadero_Wokwi/diagram.json`](Web_Solucion_Monitoreo_Invernadero/Firmware/ESP32_Invernadero_Wokwi/diagram.json). Copia el contenido de ese archivo y pégalo en la pestaña **`diagram.json`** de Wokwi.

### Paso 4: Pegar el código (`sketch.ino`) y configurar ambas URLs de tu Dev Tunnel
1. Abre el archivo [`Firmware/ESP32_Invernadero_Wokwi/ESP32_Invernadero_Wokwi.ino`](Web_Solucion_Monitoreo_Invernadero/Firmware/ESP32_Invernadero_Wokwi/ESP32_Invernadero_Wokwi.ino), copia su contenido y pégalo en **`sketch.ino`** dentro de Wokwi. Este archivo es la versión principal del firmware y debe mantenerse actualizado.
2. **Verificación obligatoria del Dev Tunnel en las dos URLs:** Debes reemplazar el dominio del túnel por el de **TU propio Dev Tunnel** generado en Visual Studio en **ambas constantes**:
   * `serverUrl`: debe quedar como `"https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria"`
   * `confirmarComandoUrl`: debe quedar como `"https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria/comandos/confirmar"`
   *(Si falta actualizar cualquiera de las dos, el ESP32 no podrá enviar telemetría o no podrá confirmar al servidor cuando complete un riego).*
3. Si vas a simular más de un invernadero en paralelo, cambia también `CODIGO_DISPOSITIVO` (`"ESP32-ORQ-01"`, `"ESP32-ORQ-02"` o `"ESP32-ORQ-03"`).

El código completo no se repite en este documento para evitar que existan dos versiones diferentes. Antes de iniciar Wokwi, modifica solamente estas variables en el archivo del firmware:

```cpp
const char* serverUrl = "https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria";
const char* confirmarComandoUrl = "https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria/comandos/confirmar";
const char* CODIGO_DISPOSITIVO = "ESP32-ORQ-01";
```

### Paso 5: Iniciar la simulación
1. Haz clic en el botón verde **Start Simulation** (`Play`) en Wokwi.
2. Verifica en el **Monitor Serial** inferior el mensaje `[ESP32-ORQ-01] WiFi conectado.` seguido de las lecturas cada 2,5 segundos: `[ESP32-ORQ-01] H: xx.x% | T: xx.x C | Rango T: xx.x-xx.x C`.
3. Para probar cambios ambientales en vivo, haz clic sobre el sensor **DHT22** en el diagrama y mueve los deslizadores de **Humidity** y **Temperature** (observarás cómo gira el servomotor a $90^\circ$ y se encienden los LEDs azul o rojo según los umbrales de la orquídea).

---

## 7. Primer Acceso al Sistema Web

1. Abre en tu navegador `http://localhost:5034` (o ejecuta en Visual Studio con el Dev Tunnel Activado accediendo con la URL pública de tu Dev Tunnel).
2. El sistema redirigirá automáticamente a **`/Auth/Login`**.
3. Inicia sesión con el **correo electrónico** y **contraseña** de un usuario activo registrado en la tabla `usuarios` de tu base de datos para ingresar al **Monitor en Vivo**.
