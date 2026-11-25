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
const char* serverUrl = "https://lb0ldwqg-5034.brs.devtunnels.ms/api/telemetria";
const char* confirmarComandoUrl = "https://lb0ldwqg-5034.brs.devtunnels.ms/api/telemetria/comandos/confirmar";
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

  WiFi.begin(ssid, password);
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
