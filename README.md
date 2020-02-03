# SmartMeterToMqtt

A simple console application runnable in docker that reads data from a smart electricity/gas meter (Dutch: Slimme meter) via the COM port and sends it to a MQTT broker.

In my case, i'm using Node-red to forward this data to InfluxDB, to visualize it in Grafana.


Docker-compose:
```
  SmartMeterToMqtt:
    container_name: SmartMeterToMqtt
    environment:
    - MqttIp=172.20.0.2
    - ComPort=/dev/smartMeter
    - MqttPort=1883
    devices:
    - "YOUR_COM_PORT:/dev/smartMeter" 
    image: woutersnl/smartmeter_to_mqtt:latest
    restart: unless-stopped

```