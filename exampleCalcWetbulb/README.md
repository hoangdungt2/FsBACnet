# Example Calculate Wetbulb from Drybulb and Relative Humidity
This example to read drybulb temperature (°C) and relative humidity (%) from weather station to calculate wetbulb temperature (°C) and write it back to BACnet

The formula is from [DOI:10.1016/j.measurement.2018.06.042](https://doi.org/10.1016/j.measurement.2018.06.042)

### Create *Bacnet Client* using ipaddress entered from command line argument 
```fsharp
// Create BACnetClient using command line argument 
let client = FsBACnet.getBACnetClient FsBACnet.BACnetType.BACnetIP argv.[0]
             |> FsBACnet.attachOnIAmToClient BACnetDeviceStore.handlerOnIam 
```
### Discover BACnet network and build BACnetPointStore
```fsharp
// Discover the network in 10s
BACnetDeviceStore.buildDeviceStore client 10                           

// Build BACnetPointStore
[
    dbStr
    rhStr
    wbStr
]
|> List.iter ( BACnetPoint.parsePointString >> BACnetPointStore.putPoint )
```

### loop forever to calculate wetbulb and write it to bacnet
```fsharp
while true do
    // update value of all BACnetPoints in BACnetPointStore
    BACnetPointStore.updateValues client

    // read rh and drybulb then calculate wetbulb
    let drybulb = BACnetPointStore.getPoint dbStr |> Option.get |> getPresentValue
    let rh      = BACnetPointStore.getPoint rhStr |> Option.get |> getPresentValue
    let wbPoint = BACnetPointStore.getPoint wbStr |> Option.get

    // calculate wb
    let wb = x rh drybulb |> calcWb
    printfn "drybulb = %.2f, rh = %.1f -> wb = %.2f" drybulb rh wb

    // write wb to wbPoint
    wb
    |> float32
    |> BACnetPoint.writeDisplayResult client wbPoint  
    |> Async.RunSynchronously

    // sleep 
    samplingTime * 1000 |> Async.Sleep |> Async.RunSynchronously
```

### Result
Result should look something like this

    [buildDeviceStore]: build Device Store in 10 seconds
    [handlerOnIam]: adding 105021 to Device Store
    [handlerOnIam]: adding 105011 to Device Store
    [handlerOnIam]: adding 105123 to Device Store
    [handlerOnIam]: adding 126291 to Device Store
    drybulb = 28.10, rh = 84.8 -> wb = 26.02
    Write 26.02 to `105123 AV-28` ... true
    drybulb = 28.10, rh = 84.9 -> wb = 26.04
    Write 26.04 to `105123 AV-28` ... true
    drybulb = 28.10, rh = 84.9 -> wb = 26.04
    Write 26.04 to `105123 AV-28` ... true
    drybulb = 28.10, rh = 84.8 -> wb = 26.02
    Write 26.02 to `105123 AV-28` ... true
