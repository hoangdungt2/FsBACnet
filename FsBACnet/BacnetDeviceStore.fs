namespace FsBACnet
open System.IO.BACnet
open Newtonsoft.Json
open System

type FsBacnetAddress = 
    {
        net          : uint16;
        adr          : byte [];
        VMac         : byte [];
        ``type``     : BacnetAddressTypes;
        RoutedSource : FsBacnetAddress option;
    }
type FsBacnetDevice = 
    {
        DeviceID  : uint32;
        DeviceAdr : FsBacnetAddress;           
    }

module BACnetDeviceStore = 
    let rec convertFsAdr2BacnetAddr (adr:FsBacnetAddress) = 
        let tmp = BacnetAddress( BacnetAddressTypes.IP, uint16 0, [|10uy|] )
        tmp.adr <- adr.adr
        tmp.net <- adr.net
        tmp.VMac <- adr.VMac
        tmp.``type`` <- adr.``type``
        tmp.RoutedSource <- BacnetAddress( BacnetAddressTypes.IP, uint16 0, [|10uy|] ) // need to initialized first
        match adr.RoutedSource with
        | None -> tmp.RoutedSource <- null
        | Some rAdr -> tmp.RoutedSource <- convertFsAdr2BacnetAddr rAdr
        tmp  
    let private getDevListsFromJson (file:string) = 
        JsonConvert.DeserializeObject<FsBacnetDevice list>( IO.File.ReadAllText(file) )
        
    type private MsgStringBacnet = 
    | Put of string*BacnetAddress
    | GetAll of AsyncReplyChannel<Map<string,BacnetAddress>>
    | IsExist of string*AsyncReplyChannel<bool>
    | Get of string*AsyncReplyChannel<BacnetAddress option>
    type BacnetDeviceStoreInstance = 
        {
            putDevice : string -> BacnetAddress -> unit
            buildDeviceStore : int -> unit
            getDevice : string -> BacnetAddress option
            getDevices : unit -> Map<string,BacnetAddress>
            isDeviceExist: string -> bool
            loadDevicesFromFile : string -> unit
        }
    let initialize (client:BacnetClient) = 
        let agent = MailboxProcessor.Start(fun (inbox:MailboxProcessor<MsgStringBacnet>) -> 
            let rec handlerMsg (state:Map<string,BacnetAddress>) = 
                async{
                    let! msg = inbox.Receive()
                    let newState = match msg with
                                   | Put (x,y) -> 
                                        Map.add x y state
                                   | GetAll reply -> 
                                        reply.Reply(state)
                                        state
                                   | Get (name,reply) -> 
                                        Map.tryFind name state 
                                        |> reply.Reply 
                                        state
                                   | IsExist (name,reply) -> 
                                        state.ContainsKey name 
                                        |> reply.Reply
                                        state
                    return! handlerMsg newState                                              
                }
            handlerMsg Map.empty
        )
        let isDeviceExist (name:string) = 
            agent.PostAndReply (fun reply -> IsExist(name,reply))   
        let putDevice name addr = 
            agent.Post ((name,addr) |> Put)
        let handlerOnIam (_:BacnetClient) (adr:BacnetAddress) (deviceId:uint32) (maxApdu:uint32) (segmentation:BacnetSegmentations) vendorId =
            if deviceId.ToString() |> isDeviceExist |> not then
                printfn "[handlerOnIam]: adding %d to Device Store" deviceId    
                putDevice (deviceId.ToString()) adr
        client.add_OnIam (new BacnetClient.IamHandler( handlerOnIam ))
        let buildDeviceStore (timeToBuildInSecond:int) = 
            printfn "[buildDeviceStore]: build Device Store in %d seconds" timeToBuildInSecond
            client.Start()
            client.WhoIs()
            timeToBuildInSecond * 1000 |> Async.Sleep |> Async.RunSynchronously
        let loadDevicesFromFile (file:string) = 
            JsonConvert.DeserializeObject<FsBacnetDevice list>( IO.File.ReadAllText(file) )
            |> List.iter (fun dev -> putDevice (dev.DeviceID.ToString()) (dev.DeviceAdr|>convertFsAdr2BacnetAddr) ) 
        {
            buildDeviceStore = buildDeviceStore
            putDevice = putDevice
            getDevice = fun name -> agent.PostAndReply (fun reply -> Get(name,reply))
            getDevices = fun () -> agent.PostAndReply(GetAll)
            isDeviceExist = isDeviceExist
            loadDevicesFromFile = loadDevicesFromFile
        }

module BACnetDeviceStoreStatic = 
    /// - MAILBOXES
    type private MsgStringBacnet = 
    | Put of string*BacnetAddress
    | GetAll of AsyncReplyChannel<Map<string,BacnetAddress>>
    | IsExist of string*AsyncReplyChannel<bool>
    | Get of string*AsyncReplyChannel<BacnetAddress option>
    type private DeviceMailbox () = 
        static let mailBoxFunc (inbox:MailboxProcessor<MsgStringBacnet>) = 
            let rec handlerMsg (state:Map<string,BacnetAddress>) = 
                async{
                    let! msg = inbox.Receive()
                    let newState = match msg with
                                   | Put (x,y) -> Map.add x y state
                                   | GetAll reply -> reply.Reply(state); state
                                   | Get (name,reply) -> Map.tryFind name state |> reply.Reply ; state
                                   | IsExist (name,reply) -> state.ContainsKey name |> reply.Reply; state
                    return! handlerMsg newState                                              
                }
            handlerMsg Map.empty
        static let agent = MailboxProcessor<MsgStringBacnet>.Start(mailBoxFunc)                    
        static member Put (fn:string*BacnetAddress) = 
            agent.Post (Put fn)
        static member GetAll () = 
            agent.PostAndReply(GetAll)
        static member GetAllAsync () = 
            agent.PostAndAsyncReply(GetAll)
        static member IsExist (name:string) = 
            agent.PostAndReply (fun reply -> IsExist(name,reply))     
        static member Get (name:string) = 
            agent.PostAndReply (fun reply -> Get(name,reply))     
    let handlerOnIam (_:BacnetClient) (adr:BacnetAddress) (deviceId:uint32) (maxApdu:uint32) (segmentation:BacnetSegmentations) vendorId =
        if deviceId.ToString() |> DeviceMailbox.IsExist |> not then
            printfn "[handlerOnIam]: adding %d to Device Store" deviceId    
            DeviceMailbox.Put (deviceId.ToString(), adr) 
        ()
    let getDevices = 
        DeviceMailbox.GetAll
    let getDevice = 
        DeviceMailbox.Get
    let buildDeviceStore (client:BacnetClient) (timeToBuildInSecond:int) = 
        printfn "[buildDeviceStore]: build Device Store in %d seconds" timeToBuildInSecond
        client.Start()
        client.WhoIs()
        timeToBuildInSecond * 1000 |> Async.Sleep |> Async.RunSynchronously

