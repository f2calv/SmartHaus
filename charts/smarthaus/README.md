# smarthaus

Deploys SmartHaus as multiple single-responsibility workloads. Every alias consumes the public
`workload` chart and runs the same application image with feature-specific values.

## Dependency Graph

```mermaid
graph LR
  SmartHaus([smarthaus 0.2.0])
  Workload[workload 1.1.0]

  subgraph Aliases[Workload aliases]
    Buderus[buderus]
    DoorBird[doorbird]
    Fronius[fronius]
    Knx[knx]
    EdgeHardware[edgehardware]
    EdgeCpu[edge-cpu]
    EdgeGpu[edge-gpu]
    Ffmpeg[ffmpeg]
    SignalRHub[signalrhub]
    Comms[comms]
    Wiz[wiz]
    Shelly[shelly]
    Ubiquiti[ubiquiti]
    Sicce[sicce]
  end

  SmartHaus --> Buderus & DoorBird & Fronius & Knx
  SmartHaus --> EdgeHardware & EdgeCpu & EdgeGpu & Ffmpeg
  SmartHaus --> SignalRHub & Comms & Wiz & Shelly & Ubiquiti & Sicce
  Buderus & DoorBird & Fronius & Knx --> Workload
  EdgeHardware & EdgeCpu & EdgeGpu & Ffmpeg --> Workload
  SignalRHub & Comms & Wiz & Shelly & Ubiquiti & Sicce --> Workload
```
