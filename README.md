# Sistema Web e IoT de Monitoreo y Riego Adaptativo para Invernaderos de Orquídeas

Plataforma web desarrollada en **ASP.NET Core MVC (.NET 8)** conectada a nodos microcontroladores **ESP32** (simulados en **Wokwi**) y persistencia relacional en **MySQL 8.0** (local o en la nube con **Azure Database for MySQL**). El sistema captura humedad y temperatura cada **2,5 segundos**, evalúa los umbrales botánicos de cada especie de orquídea y controla automáticamente una válvula de riego mediante servomotor, climatización (ventilación y calefacción) y confirmación de comandos con persistencia en memoria no volátil (NVS).

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
   ```bash
   git clone [https://github.com/Miguel777png/Web_Solucion_Monitoreo_Invernadero.git](https://github.com/Miguel777png/Web_Solucion_Monitoreo_Invernadero.git)
   cd Web_Solucion_Monitoreo_Invernadero
   ```

2. Restaura los paquetes NuGet necesarios (incluyendo `Pomelo.EntityFrameworkCore.MySql`) desde la misma terminal:
   ```bash
   dotnet restore
   ```

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
   * Abre el archivo `.sql` del proyecto en tu gestor SQL.
   * Asegúrate de ejecutar el bloque completo desde la creación de la base de datos hasta los `INSERT` iniciales:
     ```sql
     CREATE DATABASE IF NOT EXISTS monitoreo_orquideas
       CHARACTER SET utf8mb4
       COLLATE utf8mb4_unicode_ci;

     USE monitoreo_orquideas;
     -- (Continúa con la creación de las tablas y los registros iniciales)
     ```
   * Presiona **Ejecutar** (`F9` en HeidiSQL o el ícono del rayo en MySQL Workbench).

4. **Verifica la estructura:**
   * Refresca el panel izquierdo y confirma que exista la base de datos **`monitoreo_orquideas`** con todas sus tablas (`usuarios`, `plantas`, `invernaderos`, `sensores`, `calendario_riego`, `registro_historico`, `bitacora_riego_alertas`, `comandos_riego` y `auditoria_cambios`).

### Opción B: Usar la Base de Datos en la Nube (Azure Database for MySQL)

Si utilizarás la instancia desplegada en Microsoft Azure (`orquideas-iot3.mysql.database.azure.com`), no necesitas ejecutar el script localmente; solo verifica en el portal de Azure (**Redes**) que tu dirección IP pública tenga permiso de acceso en el firewall del servidor.

---

## 4. Configuración de `appsettings.json` (Local vs. Nube)

Abre el archivo **`appsettings.json`** en la raíz del proyecto web y modifica la cadena **`DefaultConnection`** según el entorno que vayas a utilizar:

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

| Parámetro | Configuración Local (Laragon/MySQL) | Configuración Nube (Azure MySQL) |
| :--- | :--- | :--- |
| **`Server`** | `localhost` (o `127.0.0.1`) | `orquideas-iot3.mysql.database.azure.com` |
| **`Uid`** | `root` | `administrador` (usuario admin de Azure) |
| **`Pwd`** | *(vacío)* o tu clave local | Contraseña configurada en el servidor Azure |
| **`SslMode`** | `None` *(evita errores de certificado local)* | `Required` *(obligatorio por seguridad en Azure)* |

---

## 5. Ejecución y Creación del Dev Tunnel en Visual Studio

### ¿Por qué es necesario un Dev Tunnel?
Los microcontroladores ESP32 simulados en **Wokwi** se ejecutan en internet y **no pueden conectarse a `localhost:5034`** directamente. **Microsoft Dev Tunnels** genera una URL HTTPS pública que redirige tanto el envío de telemetría como la confirmación de comandos hacia tu servidor local.

### Pasos para crear el túnel y levantar el servidor:

1. Abre la solución **`Web_Solucion_Monitoreo_Invernadero.sln`** en **Visual Studio**.
2. En la barra superior, haz clic en la flecha desplegable junto al botón verde de ejecución (**`http`**).
3. Selecciona **Túneles de desarrollo** $\rightarrow$ **Crear un túnel...**.
4. Inicia sesión con tu cuenta Microsoft / institucional y configura los parámetros:
   * **Nombre:** `tunel-orquideas`
   * **Tipo de túnel:** **Persistente** *(para conservar la misma URL al reiniciar)*
   * **Acceso:** **Público** *(indispensable para que el ESP32 pueda enviar peticiones `POST` sin iniciar sesión)*
5. Presiona **`F5`** (o clic en el botón **http**) para ejecutar el proyecto. Visual Studio abrirá el navegador y mostrará la URL pública generada (ejemplo: `https://lb0ldwqg-5034.brs.devtunnels.ms`). **Copia esta URL base**, ya que deberás colocarla en **dos variables** dentro del código del ESP32 en Wokwi.

---

## 6. Instalación y Configuración del Nodo ESP32 en Wokwi

Sigue estos pasos para montar el microcontrolador, el sensor DHT22, el servomotor de la válvula y los LEDs de climatización en el simulador:

### Paso 1: Crear el proyecto en Wokwi
1. Ingresa a [wokwi.com](https://wokwi.com/) e inicia sesión.
2. En el menú principal, haz clic en **New Project** $\rightarrow$ selecciona la placa **ESP32**.

### Paso 2: Instalar las librerías necesarias (`libraries.txt`)
1. En el editor de Wokwi, haz clic en la pestaña **`Library Manager`** (o abre el archivo **`libraries.txt`** en la barra superior de pestañas).
2. Agrega las siguientes librerías externas (las librerías `WiFi.h`, `HTTPClient.h`, `WiFiClientSecure.h` y `Preferences.h` ya vienen integradas en el núcleo del ESP32):

```text
# Wokwi Library List
# See [https://docs.wokwi.com/guides/libraries](https://docs.wokwi.com/guides/libraries)

DHT sensor library
ArduinoJson
ESP32Servo
```

### Paso 3: Cargar el circuito y conexiones (`diagram.json`)
1. Haz clic en la pestaña **`diagram.json`** dentro del editor de Wokwi.
2. Borra el contenido por defecto y pega la siguiente configuración (incluye la placa **ESP32**, el sensor **DHT22** en `GPIO 15`, el **Servomotor** de válvula en `GPIO 26`, el **LED Azul de ventilación** en `GPIO 27` y el **LED Rojo de calefacción** en `GPIO 14` con sus resistencias de $1\text{ k}\Omega$):

```json
{
  "version": 1,
  "author": "Anonymous maker",
  "editor": "wokwi",
  "parts": [
    { "type": "board-esp32-devkit-c-v4", "id": "esp", "top": -105.6, "left": -4.76, "attrs": {} },
    {
      "type": "wokwi-dht22",
      "id": "dht1",
      "top": -278.1,
      "left": 23.4,
      "attrs": { "humidity": "82.5", "temperature": "32.5" }
    },
    {
      "type": "wokwi-servo",
      "id": "servo1",
      "top": 178.6,
      "left": -37.4,
      "rotate": 90,
      "attrs": { "hornColor": "blue" }
    },
    {
      "type": "wokwi-led",
      "id": "led1",
      "top": -109.2,
      "left": -111.4,
      "attrs": { "color": "red" }
    },
    {
      "type": "wokwi-led",
      "id": "led2",
      "top": -109.2,
      "left": -159.4,
      "attrs": { "color": "blue" }
    },
    {
      "type": "wokwi-resistor",
      "id": "r1",
      "top": -24,
      "left": -163.75,
      "rotate": 90,
      "attrs": { "value": "1000" }
    },
    {
      "type": "wokwi-resistor",
      "id": "r2",
      "top": -24,
      "left": -115.75,
      "rotate": 90,
      "attrs": { "value": "1000" }
    }
  ],
  "connections": [
    [ "esp:TX", "$serialMonitor:RX", "", [] ],
    [ "esp:RX", "$serialMonitor:TX", "", [] ],
    [ "dht1:VCC", "esp:3V3", "red", [ "h-38.4", "v86.4" ] ],
    [ "dht1:SDA", "esp:15", "green", [ "v28.8", "h67.3", "v201.6" ] ],
    [ "dht1:GND", "esp:GND.2", "black", [ "h28.8" ] ],
    [ "servo1:PWM", "esp:26", "green", [ "h-67.4", "v-172.8" ] ],
    [ "servo1:V+", "esp:5V", "red", [ "v-28.8", "h-48.25" ] ],
    [ "servo1:GND", "esp:GND.2", "black", [ "h76.8", "v-259.2" ] ],
    [ "led2:A", "r1:1", "red", [ "v0" ] ],
    [ "led1:A", "r2:1", "red", [ "v0" ] ],
    [ "r1:2", "esp:27", "red", [ "v8.4", "h134.25" ] ],
    [ "r2:2", "esp:14", "red", [ "v18", "h86.25" ] ],
    [ "led1:C", "esp:GND.1", "black", [ "v0" ] ],
    [ "led2:C", "esp:GND.1", "black", [ "v0" ] ]
  ],
  "dependencies": {}
}
```

### Paso 4: Pegar el código (`sketch.ino`) y configurar ambas URLs de tu Dev Tunnel
1. Regresa a la pestaña **`sketch.ino`**, borra el código inicial y pega el siguiente firmware.
2. **Verificación obligatoria del Dev Tunnel en las dos URLs:** Debes reemplazar el dominio del túnel por el de **TU propio Dev Tunnel** generado en Visual Studio en **ambas constantes**:
   * `serverUrl`: debe quedar como `"https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria"`
   * `confirmarComandoUrl`: debe quedar como `"https://TU-TUNEL-5034.brs.devtunnels.ms/api/telemetria/comandos/confirmar"`
   *(Si falta actualizar cualquiera de las dos, el ESP32 no podrá enviar telemetría o no podrá confirmar al servidor cuando complete un riego).*
3. Si vas a simular más de un invernadero en paralelo, cambia también `CODIGO_DISPOSITIVO` (`"ESP32-ORQ-01"`, `"ESP32-ORQ-02"` o `"ESP32-ORQ-03"`).

```cpp
#include <WiFi.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <ESP32Servo.h>
#include <Preferences.h>
#include "DHT.h"

#define DHT_PIN 15
#define DHT_TYPE DHT22
#define SERVO_PIN 26
#define LED_VENTILACION_PIN 27
#define LED_CALEFACCION_PIN 14

const int VALVULA_CERRADA = 0;
const int VALVULA_ABIERTA = 90;
const int MAX_RIEGO_DEMO_SEG = 10;

const char* ssid = "Wokwi-GUEST";
const char* password = "";
// VERIFICA Y REEMPLAZA EL DOMINIO EN AMBAS URLs POR EL DE TU PROPIO DEV TUNNEL:
// ServerURL DEBE APUNTAR con la ruta /api/telemetria
// ServerURL DEBE APUNTAR con la ruta /api/telemetria/comandos/confirmar

const char* serverUrl = "[https://lb0ldwqg-5034.brs.devtunnels.ms/api/telemetria";
const char* confirmarComandoUrl = "[https://lb0ldwqg-5034.brs.devtunnels.ms/api/telemetria/comandos/confirmar";
const char* CODIGO_DISPOSITIVO = "ESP32-ORQ-01";

DHT dht(DHT_PIN, DHT_TYPE);
Servo servoValvula;
Preferences preferencias;

unsigned long ultimoEnvio = 0;
const unsigned long INTERVALO_ENVIO_MS = 2500;
const unsigned long TIMEOUT_COMANDO_CLIMA_MS = 10000;
const unsigned long TIMEOUT_RIEGO_EMERGENCIA_MS = 10000;

bool regaderaActiva = false;
bool riegoEmergenciaContinuo = false;
bool ventilacionActiva = false;
bool calefaccionActiva = false;
unsigned long tiempoFinRegadera = 0;
unsigned long ultimoTickVisual = 0;
unsigned long ultimoComandoClima = 0;
unsigned long ultimoComandoRiegoEmergencia = 0;
unsigned long ultimoIntentoConfirmacion = 0;

int idComandoActual = 0;
int idUltimoComandoCompletado = 0;
int idConfirmacionPendiente = 0;
String estadoConfirmacionPendiente = "";

void setup() {
  Serial.begin(115200);

  pinMode(LED_VENTILACION_PIN, OUTPUT);
  pinMode(LED_CALEFACCION_PIN, OUTPUT);
  digitalWrite(LED_VENTILACION_PIN, LOW);
  digitalWrite(LED_CALEFACCION_PIN, LOW);

  servoValvula.setPeriodHertz(50);
  servoValvula.attach(SERVO_PIN, 500, 2400);
  servoValvula.write(VALVULA_CERRADA);

  dht.begin();
  preferencias.begin("riego", false);
  idUltimoComandoCompletado = preferencias.getInt("ultimoOk", 0);
  int idComandoInterrumpido = preferencias.getInt("enCurso", 0);

  WiFi.begin(ssid, password, 6);
  Serial.print("Conectando WiFi");

  while (WiFi.status() != WL_CONNECTED) {
    delay(300);
    Serial.print(".");
  }

  Serial.printf("\n[%s] WiFi conectado.\n", CODIGO_DISPOSITIVO);

  if (idComandoInterrumpido > 0) {
    preferencias.remove("enCurso");
    programarConfirmacion(idComandoInterrumpido, "Fallido");
  }

  if (String(CODIGO_DISPOSITIVO).endsWith("02")) delay(800);
  if (String(CODIGO_DISPOSITIVO).endsWith("03")) delay(1600);
}

void loop() {
  unsigned long ahora = millis();

  if (riegoEmergenciaContinuo &&
      ahora - ultimoComandoRiegoEmergencia >= TIMEOUT_RIEGO_EMERGENCIA_MS) {
    riegoEmergenciaContinuo = false;
    regaderaActiva = false;
    servoValvula.write(VALVULA_CERRADA);
    Serial.println("VALVULA CERRADA: se perdio la confirmacion del servidor.");
  }

  actualizarRegadera(ahora);

  if (idConfirmacionPendiente > 0 &&
      ahora - ultimoIntentoConfirmacion >= 3000) {
    intentarConfirmacionPendiente();
  }

  if (ultimoComandoClima != 0 &&
      ahora - ultimoComandoClima >= TIMEOUT_COMANDO_CLIMA_MS) {
    actualizarClimatizacion(false, false);
    ultimoComandoClima = 0;
    Serial.println("Climatizacion apagada: sin respuesta reciente del servidor.");
  }

  if (ahora - ultimoEnvio >= INTERVALO_ENVIO_MS) {
    ultimoEnvio = ahora;
    enviarTelemetria();
  }
}

void actualizarClimatizacion(bool activarVentilacion, bool activarCalefaccion) {
  // Estado imposible: apaga ambos actuadores en vez de ejecutar una orden
  // contradictoria.
  if (activarVentilacion && activarCalefaccion) {
    activarVentilacion = false;
    activarCalefaccion = false;
    Serial.println("Respuesta invalida: ventilacion y calefaccion simultaneas.");
  }

  if (activarVentilacion != ventilacionActiva) {
    ventilacionActiva = activarVentilacion;
    digitalWrite(LED_VENTILACION_PIN, ventilacionActiva ? HIGH : LOW);
    Serial.printf("VENTILACION: %s\n", ventilacionActiva ? "ACTIVADA" : "DESACTIVADA");
  }

  if (activarCalefaccion != calefaccionActiva) {
    calefaccionActiva = activarCalefaccion;
    digitalWrite(LED_CALEFACCION_PIN, calefaccionActiva ? HIGH : LOW);
    Serial.printf("CALEFACCION: %s\n", calefaccionActiva ? "ACTIVADA" : "DESACTIVADA");
  }
}

void programarConfirmacion(int idComando, const String& estado) {
  if (idComando <= 0) {
    return;
  }

  idConfirmacionPendiente = idComando;
  estadoConfirmacionPendiente = estado;
  intentarConfirmacionPendiente();
}

void intentarConfirmacionPendiente() {
  if (idConfirmacionPendiente <= 0 || WiFi.status() != WL_CONNECTED) {
    return;
  }

  ultimoIntentoConfirmacion = millis();

  WiFiClientSecure client;
  client.setInsecure();
  client.setTimeout(5000);

  HTTPClient http;
  http.begin(client, confirmarComandoUrl);
  http.addHeader("Content-Type", "application/json");

  JsonDocument documento;
  documento["codigoDispositivo"] = CODIGO_DISPOSITIVO;
  documento["idComandoRiego"] = idConfirmacionPendiente;
  documento["estado"] = estadoConfirmacionPendiente;

  String payload;
  serializeJson(documento, payload);

  int httpCode = http.POST(payload);

  if (httpCode == HTTP_CODE_OK) {
    Serial.printf(
      "Orden #%d confirmada como %s.\n",
      idConfirmacionPendiente,
      estadoConfirmacionPendiente.c_str()
    );
    idConfirmacionPendiente = 0;
    estadoConfirmacionPendiente = "";
  } else {
    Serial.printf(
      "No se pudo confirmar la orden. HTTP %d; se reintentara.\n",
      httpCode
    );
  }

  http.end();
}

void activarRegadera(int tiempoOrdenSeg, int idComando) {
  if (regaderaActiva || tiempoOrdenSeg <= 0) {
    return;
  }

  int tiempoDemoSeg = min(tiempoOrdenSeg, MAX_RIEGO_DEMO_SEG);

  regaderaActiva = true;
  idComandoActual = idComando;
  ultimoTickVisual = millis();
  tiempoFinRegadera = millis() + (tiempoDemoSeg * 1000UL);
  servoValvula.write(VALVULA_ABIERTA);

  if (idComandoActual > 0) {
    preferencias.putInt("enCurso", idComandoActual);
    programarConfirmacion(idComandoActual, "Confirmado");
  }

  Serial.printf(
    "VALVULA ABIERTA A 90 GRADOS. Orden: %ds | Demo: %ds\n",
    tiempoOrdenSeg,
    tiempoDemoSeg
  );
}

void actualizarRiegoEmergencia(bool activar) {
  if (activar) {
    ultimoComandoRiegoEmergencia = millis();

    if (!riegoEmergenciaContinuo) {
      if (idComandoActual > 0) {
        preferencias.remove("enCurso");
        programarConfirmacion(idComandoActual, "Fallido");
      }
      riegoEmergenciaContinuo = true;
      regaderaActiva = true;
      idComandoActual = 0;
      servoValvula.write(VALVULA_ABIERTA);
      Serial.println("VALVULA ABIERTA: humedad bajo el minimo permitido.");
    }
    return;
  }

  if (riegoEmergenciaContinuo) {
    riegoEmergenciaContinuo = false;
    regaderaActiva = false;
    servoValvula.write(VALVULA_CERRADA);
    Serial.println("VALVULA CERRADA: humedad recupero el rango permitido.");
  }
}

void actualizarRegadera(unsigned long ahora) {
  if (!regaderaActiva || riegoEmergenciaContinuo) {
    return;
  }

  if (static_cast<long>(ahora - tiempoFinRegadera) >= 0) {
    regaderaActiva = false;
    servoValvula.write(VALVULA_CERRADA);
    Serial.println("VALVULA CERRADA. RIEGO FINALIZADO.");

    if (idComandoActual > 0) {
      idUltimoComandoCompletado = idComandoActual;
      preferencias.putInt("ultimoOk", idUltimoComandoCompletado);
      preferencias.remove("enCurso");
      programarConfirmacion(idComandoActual, "Completado");
      idComandoActual = 0;
    }

    return;
  }

  if (ahora - ultimoTickVisual >= 1000) {
    ultimoTickVisual = ahora;
    unsigned long restantes = (tiempoFinRegadera - ahora + 999UL) / 1000UL;
    Serial.printf("REGANDO... %lu segundos restantes\n", restantes);
  }
}

void enviarTelemetria() {
  float humedad = dht.readHumidity();
  float temperatura = dht.readTemperature();

  if (isnan(humedad) || isnan(temperatura)) {
    Serial.println("Error leyendo el DHT22.");
    return;
  }

  if (WiFi.status() != WL_CONNECTED) {
    WiFi.reconnect();
    return;
  }

  WiFiClientSecure client;
  client.setInsecure();
  client.setTimeout(5000);

  HTTPClient http;
  http.setReuse(false);
  http.begin(client, serverUrl);
  http.addHeader("Content-Type", "application/json");

  JsonDocument documento;
  documento["codigoDispositivo"] = CODIGO_DISPOSITIVO;
  documento["humedad"] = round(humedad * 100.0) / 100.0;
  documento["temperatura"] = round(temperatura * 100.0) / 100.0;

  String payload;
  serializeJson(documento, payload);

  int httpCode = http.POST(payload);

  if (httpCode <= 0) {
    http.end();
    delay(350);
    http.begin(client, serverUrl);
    http.addHeader("Content-Type", "application/json");
    httpCode = http.POST(payload);
  }

  if (httpCode == HTTP_CODE_OK) {
    String respuesta = http.getString();
    JsonDocument respuestaJson;
    DeserializationError error = deserializeJson(respuestaJson, respuesta);

    if (error) {
      Serial.printf("Respuesta JSON invalida: %s\n", error.c_str());
    } else {
      bool activarRiego = respuestaJson["activarRegadera"] | false;
      int tiempoSeg = respuestaJson["tiempoSeg"] | 0;
      bool riegoEmergencia = respuestaJson["riegoEmergenciaContinuo"] | false;
      bool activarVentilacion = respuestaJson["activarVentilacion"] | false;
      bool activarCalefaccion = respuestaJson["activarCalefaccion"] | false;
      float temperaturaMinima = respuestaJson["temperaturaMinima"] | 0.0;
      float temperaturaMaxima = respuestaJson["temperaturaMaxima"] | 0.0;
      int idComandoRiego = respuestaJson["idComandoRiego"] | 0;

      // Los LED obedecen al servidor. Un false apaga el actuador cuando la
      // temperatura vuelve al rango configurado para la planta.
      actualizarClimatizacion(activarVentilacion, activarCalefaccion);
      ultimoComandoClima = millis();
      actualizarRiegoEmergencia(riegoEmergencia);

      if (!riegoEmergencia && activarRiego && tiempoSeg > 0) {
        if (idComandoRiego > 0 && idComandoRiego == idUltimoComandoCompletado) {
          // La respuesta anterior pudo perderse. No vuelve a abrir la válvula;
          // solamente reitera la confirmación final.
          programarConfirmacion(idComandoRiego, "Completado");
        } else if (idComandoRiego > 0 && idComandoRiego == idComandoActual) {
          programarConfirmacion(idComandoRiego, "Confirmado");
        } else if (!regaderaActiva) {
          activarRegadera(tiempoSeg, idComandoRiego);
        } else {
          Serial.printf(
            "Orden #%d esperando: la valvula ya esta ocupada.\n",
            idComandoRiego
          );
        }
      }

      Serial.printf(
        "[%s] H: %.1f%% | T: %.1f C | Rango T: %.1f-%.1f C\n",
        CODIGO_DISPOSITIVO,
        humedad,
        temperatura,
        temperaturaMinima,
        temperaturaMaxima
      );
    }
  } else {
    Serial.printf(
      "[%s] Error HTTP %d: %s\n",
      CODIGO_DISPOSITIVO,
      httpCode,
      http.errorToString(httpCode).c_str()
    );
  }

  http.end();
}
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
