// Learn more about F# at http://fsharp.org

open System
open FsBACnet

[<EntryPoint>]
let main argv =
    let client = FsBACnet.getBACnetClient FsBACnet.BACnetType.BACnetIP "192.168.1.11" 
                 |> FsBACnet.attachOnIAmToClient BACnetDeviceStoreStatic.handlerOnIam 
    // build the dev store
    BACnetDeviceStoreStatic.buildDeviceStore client 10                           
    // try to parse some points
    ["14 av-0";"dev 14 av-2";"dev 14, bv0"]
    |> List.map (BACnetPoint.parsePointStringStaticDevStore >> (fun x -> BACnetPointStoreStatic.putPoint x; BACnetPoint.readValue client x))
    |> List.iter (fun x -> printfn "`%s` value = %A" x.PointString x.Value)
    let rnd = Random()
    
    BACnetPointStoreStatic.getPoint "14, av-2" 
    |> Option.get
    |> (fun p -> BACnetPoint.writeDisplayResult client p (rnd.NextDouble() |> float32))
    |> Async.RunSynchronously
    Async.Sleep 1000 |> Async.RunSynchronously
    BACnetPointStoreStatic.updateValues client
    BACnetPointStoreStatic.getPoints()
    |> List.iter (fun x -> printfn "`%s` value = %A" x.PointString x.Value)

    0 // return an integer exit code
