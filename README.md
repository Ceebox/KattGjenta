# KattGjenta

A simple OTLP trace generator, which can be useful for debugging collectors.  
<img src="./Assets/README/main-image.png" alt="Image of the program running" width="400" />

## Features

- Generate parent and child spans recursively
- Configurable trace time, trace depth, and number of children per node
- Supports OTLP export over:
  - gRPC
  - HTTP/Protobuf