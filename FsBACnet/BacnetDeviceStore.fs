namespace FsBACnet
open System.IO.BACnet

module BACnetDeviceStore = 
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
