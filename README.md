# DIY Hardware-Bremswiderstand ("Poor Man's Brake Resistor")

Ein autonomer Hardware-Bremswiderstand für das [DIY Sim Racing FFB Pedal](https://github.com/ChrGri/DIY-Sim-Racing-FFB-Pedal) – gebaut aus einem günstigen 3D-Drucker-Heizbett-MOSFET-Board, einem 50 W Lastwiderstand und ein paar Kleinteilen. **Materialkosten: knapp 10 €.**

> **English summary:** A ~10 € standalone regen clamp for servo-driven FFB pedals. A 39 V zener diode drives the opto input of a cheap 3D-printer heated-bed MOSFET board. As soon as the supply rail exceeds ~40 V (back-EMF from braking servos), the MOSFET switches an 8 Ω / 50 W resistor across the rail and burns the energy off. A Schottky diode (SR5100) keeps the regenerated energy away from the power supply. No firmware or wiring to the ESP32 required.

![Gehäuse mit MOSFET-Board und Bremswiderstand](images/housing_render.png)

---

## Wozu?

Wenn die Servos der Pedale schnell abgebremst werden, arbeiten sie als Generator und speisen Energie zurück in die Versorgungsleitung. Die Spannung auf der 36 V Schiene steigt dadurch an. Wird sie zu hoch, gehen die Servos in den Überspannungsfehler bzw. das Netzteil schaltet ab.

Diese Schaltung begrenzt die Spannung rein in Hardware:

1. Die **SR5100 Schottky-Diode** sitzt zwischen Netzteil und Servos. Rückgespeiste Energie kann damit nicht ins Netzteil zurückfließen, sondern bleibt auf der Servo-Seite.
2. Die **39 V Zener-Diode** beginnt zu leiten, sobald die Servo-Spannung über ca. 39 V + Optokoppler-Schwelle steigt (≈ 40–41 V).
3. Der Strom durch die Zener-Diode steuert den Optokoppler-Eingang des MOSFET-Boards an. Der MOSFET schaltet durch und legt den **8 Ω / 50 W Widerstand** über die Versorgung.
4. Die überschüssige Energie wird im Widerstand in Wärme umgesetzt, die Spannung sinkt und der MOSFET schaltet wieder ab.
5. Der **10 kΩ Widerstand** liegt parallel zum Board-Eingang. Er leitet den kleinen Leckstrom der Zener-Diode unterhalb der Schwelle ab, damit der MOSFET nicht schon vorher halb aufsteuert.

Die Schaltung arbeitet komplett eigenständig, es ist **keine Verbindung zum ESP32** nötig.

![Servo-Spannung (gelb) bei schnellen Pedalbewegungen](images/voltage_plot.png)

*Live Plot aus dem FFB Pedal Dashboard: Servo-Spannung (gelb) bei schnellen, harten Pedalbewegungen. Die Spannung bewegt sich zwischen ca. 34,8 V und 44,3 V.*

---

## Teileliste

| Anzahl | Teil | Amazon | AliExpress |
|---|---|---|---|
| 1 | MOSFET-Board (3D-Drucker Heizbett) | [Link](https://link.amazon/B00ke56jL) | [Link](https://s.click.aliexpress.com/e/_c3aylBSN) |
| 1 | Widerstand 50 W / 8 Ω (Alu-Gehäuse) | [Link](https://link.amazon/B0ddH9qBs) | [Link](https://s.click.aliexpress.com/e/_c3Blbzrf) |
| 1 | Widerstand 10 kΩ | [Link](https://link.amazon/B01VS7jOm) | [Link](https://s.click.aliexpress.com/e/_c37xX64H) |
| 1 | Zener-Diode 39 V | [Link](https://link.amazon/B076pziGp) | [Link](https://s.click.aliexpress.com/e/_c3vdG0Rx) |
| 1 | Schottky-Diode SR5100 (5 A / 100 V) | – | – |
| 1 | Kurzes Kabel (Brücke zum DC IN −) | – | – |
| – | Kabel für den 8 Ω Widerstand (z. B. 16 AWG Silikon), Schrumpfschlauch | – | – |
| optional | JST 2,54 mm Stecker 2-polig | [Link](https://link.amazon/B0fwLvwqI) | [Link](https://s.click.aliexpress.com/e/_c3m81rrX) |
| optional | Crimpzange | [Link](https://link.amazon/B06MTvK9t) | – |

Werkzeug: Lötkolben, Seitenschneider, Abisolierzange, Schraubendreher, Heißluft/Feuerzeug für Schrumpfschlauch.

Optional: 3D-gedrucktes Gehäuse, siehe [3d_print/HardwareBrakeResistorHousing_V1.3mf](3d_print/HardwareBrakeResistorHousing_V1.3mf).

### Variante: Stecker oder Löten?

- **Mit JST-Stecker (oben auf dem Board):** Wer bereits eine Crimpzange und JST 2,54 mm Stecker hat, crimpt Zener-Diode, 10 kΩ und das kurze Kabel in einen 2-poligen JST-Stecker und steckt diesen oben in die Eingangsbuchse des MOSFET-Boards. Sauber und jederzeit lösbar. So ist es in den Fotos unten gebaut.
- **Ohne Stecker (unten verlöten):** Etwas geldschonender – die Bauteile werden einfach von unten direkt an die beiden Pins der Eingangsbuchse gelötet. Elektrisch identisch.

---

## Schaltplan

```
                  SR5100
 Netzteil +V ──────►|──────┬──────────────────────────► Servos +V
                (Ring zu   │
                 Servos)   ├── DC IN +  (MOSFET-Board)
                           │      │
                           │      └──|◄── Zener 39 V ──┬──► JST Eingang +
                           │       (Ring zu DC IN +)   │
                           │                         10 kΩ
                           │                           │
 Netzteil −V ──────────────┴── DC IN −  ── kurzes Kabel┴──► JST Eingang −
                                                       
 HOT BED + / − ── 8 Ω / 50 W Widerstand
```

- **Zener-Diode:** Kathode (Ring) an **DC IN +**, Anode an den **+ Pin** des Board-Eingangs.
- **10 kΩ:** zwischen den beiden Pins des Board-Eingangs.
- **Kurzes Kabel:** vom **− Pin** des Board-Eingangs an **DC IN −**.
- **SR5100:** Anode am Netzteil **+V**, Kathode (Ring) Richtung Servos und MOSFET-Board.
- **8 Ω / 50 W:** an die beiden **HOT BED** Klemmen. Die Polarität ist egal.

> Die Polarität des Board-Eingangs ist auf der Platine beschriftet. Ist sie vertauscht, passiert nichts (der Optokoppler sperrt einfach) – dann den Stecker andersherum anschließen.

---

## Bauanleitung

### 1. Teile bereitlegen

MOSFET-Board, JST-Gehäuse mit zwei Crimpkontakten, Zener-Diode 39 V (rot, Glas), 10 kΩ Widerstand (blau), kurzes Kabel (gelb) und die SR5100 Diode.

![Bauteile](images/dsc00306.jpg)

### 2. Zener-Diode, 10 kΩ und Kabel vorbereiten

Die Anode der Zener-Diode (Seite **ohne** Ring) und das kurze Kabel jeweils in einen Crimpkontakt crimpen. Den 10 kΩ Widerstand zwischen die beiden Kontakte setzen, sodass er die beiden Pins überbrückt.

Ohne Crimpzange: diesen Schritt überspringen und die Bauteile in Schritt 4 direkt von unten an die Eingangspins löten.

![Zener-Diode, 10k und Kabel gecrimpt](images/dsc00307.jpg)

### 3. In das JST-Gehäuse stecken

Beide Kontakte in das 2-polige JST-Gehäuse einrasten lassen. Die Zener-Diode muss auf dem **+ Pin** des Board-Eingangs landen.

![JST-Stecker fertig](images/dsc00308.jpg)

### 4. Auf das MOSFET-Board stecken und anschließen

- JST-Stecker in die Eingangsbuchse des Boards stecken (bzw. von unten anlöten).
- Freies Ende der Zener-Diode (Ring-Seite / Kathode) in die Klemme **DC IN +**.
- Kurzes Kabel in die Klemme **DC IN −**.

![Board mit Zener-Diode und Brücke](images/dsc00309.jpg)

### 5. Bremswiderstand anschließen

Den 8 Ω / 50 W Widerstand mit zwei Kabeln an die beiden **HOT BED** Klemmen anschließen. Die Lötstellen am Widerstand mit Schrumpfschlauch isolieren.

![8 Ohm Widerstand an HOT BED](images/dsc00310.jpg)

### 6. SR5100 in die Netzteil-Zuleitung setzen

Die SR5100 in die **+ Leitung** zwischen Netzteil und Servos löten. Die Kathode (Ring) zeigt zum Kabel Richtung Servos, die Anode kommt ans Netzteil.

Schrumpfschlauch **vor** dem Löten über das Kabel schieben.

![SR5100 an das Kabel gelötet](images/dsc00311.jpg)

Lötstelle mit Schrumpfschlauch isolieren, sodass nur noch das Anodenbein herausschaut:

![SR5100 eingeschrumpft](images/dsc00312.jpg)

### 7. Am Netzteil anschließen

Das Anodenbein der SR5100 in die **+V** Klemme des Netzteils schrauben, die Masse an **−V**. Das MOSFET-Board (DC IN + / −) wird auf der **Servo-Seite** der Diode angeschlossen.

![Am Netzteil angeschlossen](images/dsc00313.jpg)

### 8. Firmware-Einstellung

Im SimHub-Plugin unter den Pedal-Einstellungen für den Bremswiderstand **"Hardware brake resistor circuit only"** auswählen. Die Firmware lässt dann ihren eigenen Bremswiderstand-Pin aus und begrenzt die Rückspeiseleistung des Pedals nicht mehr künstlich.

### 9. Gehäuse (optional)

Board und Widerstand in das 3D-gedruckte Gehäuse schrauben: [3d_print/HardwareBrakeResistorHousing_V1.3mf](3d_print/HardwareBrakeResistorHousing_V1.3mf). Der Alu-Widerstand wird heiß – für etwas Luft im Gehäuse sorgen und ihn nicht direkt an dünnes PLA schrauben.

---

## Wichtige Hinweise

- **Netzteilspannung muss unter der Schaltschwelle liegen.** Die Schaltung ist für ein 36 V Netzteil (z. B. Mean Well LRS-350-36) ausgelegt. Viele Netzteile haben ein Poti zur Spannungseinstellung – **nicht über ca. 37 V drehen**. Liegt die Netzteilspannung über der Zener-Schwelle, schaltet der Widerstand dauerhaft: 36 V an 8 Ω sind 4,5 A bzw. ~160 W – weit über den 50 W des Widerstands.
- **SR5100 Strombelastbarkeit:** Die SR5100 ist für 5 A ausgelegt. Durch sie fließt der komplette Servostrom. Für sehr hohe Dauerströme eine stärkere Schottky-Diode verwenden (z. B. 10 A).
- **Der Widerstand wird heiß.** Er sollte nach normalem Fahren nur warm werden. Wird er sehr heiß, Netzteilspannung und Verdrahtung prüfen.
- **Polarität prüfen** (Zener-Diode, SR5100) bevor das Netzteil eingeschaltet wird.
- Arbeiten am Netzteil nur im **stromlosen Zustand**. Die 230 V Seite nicht berühren.

---

## Affiliate-Hinweis

Die Links in der Teileliste sind Affiliate-Links. Als Amazon-Partner verdiene ich an qualifizierten Verkäufen. Für dich ändert sich am Preis nichts – du unterstützt damit aber die Weiterentwicklung des Projekts. Danke!
