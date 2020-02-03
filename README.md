# SmartMeterToMqtt

A simple console application runnable in docker that reads data from a smart electricity/gas meter (Dutch: Slimme meter) via the COM port and sends it to a MQTT broker.

In my case, i'm using Node-red to forward this data to InfluxDB, to visualize it in Grafana.

I am running this on an Odroid and Orange Pi computer, running ubuntu, hence the ARM64 in the dockerfile. For the serialport to function correctly, the libnserial.so files are include and copied to output dir.

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