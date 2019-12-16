// Learn more about F# at http://fsharp.org

open System
open FsBACnet

let exFn = "../../../bacnetFile.xlsx"

[<EntryPoint>]
let main argv =

    if IO.File.Exists exFn |> not then
        IO.Path.GetFullPath exFn
        |> sprintf "[%s] is not exists"
        |> failwith

    // Create BACnetClient using command line argument 
    let client = FsBACnet.getBACnetClient FsBACnet.BACnetType.BACnetIP "192.168.222.181"
                 |> FsBACnet.attachOnIAmToClient BACnetDeviceStoreStatic.handlerOnIam 
    // Discover the network in 10s
    BACnetDeviceStoreStatic.buildDeviceStore client 10                           


    BACnetPoint.parsePointsFromExcel exFn
    |> BACnetPointStoreStatic.putPoints

    // update values in BACnetPointStore
    BACnetPointStoreStatic.updateValues client

    // display all points
    BACnetPointStoreStatic.getPoints()
    |> List.iter (fun p -> printfn "[%s][%s] = %A" p.Name p.PointString p.Value)

    printfn ""
    0 // return an integer exit code
