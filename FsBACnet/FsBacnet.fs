namespace FsBACnet
open System.IO.BACnet

module FsBACnet =
    type BacValueOpt = (string*BacnetValue) option
    type BacnetPriority = BacnetPriority of int // this is from 1 to 16
    type BACnetType =
    | BACnetIP
    | BACnetEthernet
    type BACnetPara = string
    type BACnetIamHandler = BacnetClient->BacnetAddress->uint32->uint32->BacnetSegmentations->uint16->unit
    /// - getBACnetClient
    let getBACnetClient (bacnetType:BACnetType) (bacnetPara:BACnetPara) =
        match bacnetType with
        | BACnetType.BACnetIP -> new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false, false, 1476, bacnetPara))
        | _ -> failwith (sprintf "Unsupported :`%A`" bacnetType); new BacnetClient()
    let attachOnIAmToClient (handlerOnIam:BACnetIamHandler)  (bacnetClient:BacnetClient)=
        bacnetClient.add_OnIam (new BacnetClient.IamHandler( handlerOnIam ))
        bacnetClient

    /// - READ BACNET
    let readAsyncFlow (bacnetClient:BacnetClient) (x:BACnetPoint) =
        async {
            try
                let! res = bacnetClient.ReadPropertyAsync(x.DeviceAdr,x.BacnetObj,BacnetPropertyIds.PROP_PRESENT_VALUE)
                           |> Async.AwaitTask
                return Some (res.[0])
            with
            | _ -> return None
        }
    let read (bacnetClient:BacnetClient) (x:BACnetPoint list) =
        x
        |> List.map (readAsyncFlow bacnetClient) |> Async.Parallel |> Async.RunSynchronously
        |> Array.filter Option.isSome |> Array.map (fun x -> x.Value)
        |> List.ofArray