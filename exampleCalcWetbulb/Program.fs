// Learn more about F# at http://fsharp.org

open System
open FsBACnet

let samplingTime = 10    // 10 seconds

/// wetbulb formula from "DOI: 10.1016/j.measurement.2018.06.042"
let Pa = 1013.246
let y  = 6.46e-4
let es t = 2.796413e-8*(t**5.) + 2.671942e-6*(t**4.) + 2.73199e-4*(t**3.0)
           + 1.41951e-2*(t**2.) + 0.444226*t + 6.1078
let x rh db = 0.01*rh*(es db) + y*Pa*db
let calcWb x = 7.438995e-10*(x**5.) - 4.063282e-7*(x**4.) + 9.16616e-5*(x**3.)
               - 1.15133e-2*(x**2.) + 1.02533*x - 5.85331

/// - DEFINITION OF db,rh,wb
let dbStr = "dev 126291, ai-4"
let rhStr = "dev 126291, ai-3"
let wbStr = "dev 105123, av-28"

// may throw error
let getPresentValue (x:BACnetPoint) = 
    x.Value |> Option.get

/// - usage: run on ip 10.3.22.111
///         dotnet run -- 10.3.22.111
///         exampleCalcWetbulb.exe 10.3.22.111

[<EntryPoint>]
let main argv =
    
    // Create BACnetClient using command line argument 
    let client = FsBACnet.getBACnetClient FsBACnet.BACnetType.BACnetIP argv.[0]
                 |> FsBACnet.attachOnIAmToClient BACnetDeviceStoreStatic.handlerOnIam 
    
    // Discover the network in 10s
    BACnetDeviceStoreStatic.buildDeviceStore client 10                           

    // Build BACnetPointStore
    [
        dbStr
        rhStr
        wbStr
    ]
    |> List.iter ( BACnetPoint.parsePointStringStaticDevStore >> BACnetPointStoreStatic.putPoint )

    // loop forever to keep on updating wetbulb
    while true do
        // update value of all BACnetPoints in BACnetPointStore
        BACnetPointStoreStatic.updateValues client
               
        // read rh and drybulb then calculate wetbulb
        let drybulb = BACnetPointStoreStatic.getPoint dbStr |> Option.get |> getPresentValue
        let rh      = BACnetPointStoreStatic.getPoint rhStr |> Option.get |> getPresentValue
        let wbPoint = BACnetPointStoreStatic.getPoint wbStr |> Option.get
        
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


    0 // return an integer exit code
