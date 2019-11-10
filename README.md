# FsBACnet
A simple F# wrapper for C# BACnet library

## INTRODUCTION
BACNet is a common and powerful protocol for HVAC. However, it can be overwhelming for someone who just starts to learn BACnet.
This repo is to simplifize [C# BACnet library](https://github.com/ela-compil/BACnet) to make it easy for getting started with BACnet.

## BACnet Point ``fsBacnet/BacnetPoint.fs``
In BACnet, a device is identified by **Device ID** and each device can contains multiple "points" with identified number and type such as AV-1 AO-0 ...
In this repo, a "global" *BACnet Point* is defined with both **Device ID** and point type and point number such as **26001 AV-0**, the defition is 
```fsharp
type ReadResult<'a> = 'a option
type BACnetPoint =
    {
        PointString  : string; // "105123 AV123"for AV point 123 at device 105123
        BacnetObj    : BacnetObjectId; // be parsed from string        
        DeviceAdr    : BacnetAddress;  // be parsed from string
        Value        : ReadResult<float>; // read from obj and adr
    }
```
To parse a string, e.g. "105123 AV-123" to ``BACnetPoint``, ``BACnetPoint.parsePointString`` can be used
```fsharp
BACnetPoint.parssePointString "105123 av-123"
```

## Device Store ``fsBacnet/BACnetDeviceStore.fs``
At startup, all the devices will need to be discovered, **Device Store** is used to stored all the **Device ID** and their addresses. In this repo, **MailboxProcessor** is used for this purpose.

## BACnetPoint Store ``fsBacnet/BAC``
Similar to **DeviceStore**, **MailboxProcess** is used. This is to store all pre-defined BACnetPoint.

## Examples
### Simple ReadWrite ``exampleWriteRead``
1. Create a BACnet/IP *BACnetClient* at ip address *192.168.1.11* and discover the network in 10s
```fsharp
    let client = FsBACnet.getBACnetClient FsBACnet.BACnetType.BACnetIP "192.168.1.11" 
                 |> FsBACnet.attachOnIAmToClient BACnetDeviceStore.handlerOnIam 
    BACnetDeviceStore.buildDeviceStore client 10      
```    
2. Parse **BACnetPoint** from string and read their values
```fsharp
    ["14 av-0";"dev 14 av-2";"dev 14, bv0"]
    |> List.map (BACnetPoint.parsePointString >> (fun x -> BACnetPointStore.putPoint x; BACnetPoint.readValue client x))
    |> List.iter (fun x -> printfn "`%s` value = %A" x.PointString x.Value)
```
3. Write random number to "Dev 14, av-2" and read all back
```fsharp
    let rnd = Random()
    BACnetPointStore.getPoint "14, av-2" 
    |> Option.get
    |> (fun p -> BACnetPoint.writeDisplayResult client p (rnd.NextDouble() |> float32))
    |> Async.RunSynchronously
    Async.Sleep 1000 |> Async.RunSynchronously
    BACnetPointStore.updateValues client
    BACnetPointStore.getPoints()
    |> List.iter (fun x -> printfn "`%s` value = %A" x.PointString x.Value)
```
